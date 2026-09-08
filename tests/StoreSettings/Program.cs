using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Panaderia.Models.Entities;
using Panaderia.MVC.Controllers;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
    checks++;
}

foreach (var (minimum, total, expected) in new[] { (0m, 1m, 0m), (100m, 99.99m, 0.01m), (100m, 100m, 0m), (100m, 101m, 0m) })
{
    var cart = new CarritoViewModel { Configuracion = new() { MontoMinimoPedido = minimum }, Items = [new() { Cantidad = 1, PrecioUnitario = total }] };
    Check(cart.FaltaParaMinimo == expected, $"Minimum {minimum}, total {total}: remaining {expected}");
}
foreach (var (value, valid) in new[] { (-1m, false), (0m, true), (100.25m, true), (100.001m, false), (1000000000m, false) })
{
    var model = new ConfiguracionTiendaViewModel { MontoMinimoPedido = value };
    Check(Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true) == valid,
        $"Settings amount validation: {value}");
}

var settings = new ConfiguracionTienda { MontoMinimoPedido = 100m };
var writes = 0;
var notifications = 0;
Pedido? lastOrder = null;
var configReads = 0;
var product = new Producto { Id = 1, Nombre = "Prueba", PrecioFinal = 100m, PrecioReventa = 80m, PorEncargo = true };
var configService = Stub.Make<IConfiguracionTiendaService>((m, _) => m == "GetAsync"
    ? ReadSettings() : throw new Exception("Unexpected settings mutation"));
Task<ConfiguracionTienda> ReadSettings() { configReads++; return Task.FromResult(settings); }
var products = Stub.Make<IProductoService>((m, _) => m == "GetAllAsync"
    ? Task.FromResult<IEnumerable<Producto>>([product]) : throw new Exception(m));
var clients = Stub.Make<IClienteService>((m, _) => m == "GetByTelefonoAsync"
    ? Task.FromResult<Cliente?>(new Cliente { Id = 1, Nombre = "Prueba", Telefono = "123456", Direccion = "Calle 123" }) : throw new Exception("Unexpected client write"));
var orders = Stub.Make<IPedidoService>((m, arguments) => { if (m != "CreateAsync") throw new Exception(m); lastOrder = (Pedido)arguments![0]!; writes++; return Task.CompletedTask; });
var push = Stub.Make<IPushNotificationService>((m, _) => { notifications++; return Task.CompletedTask; });

TiendaController Controller(bool reseller = false, string? remembered = null)
{
    var context = new DefaultHttpContext();
    context.Request.Headers.Cookie = "mv_carrito=" + Uri.EscapeDataString("{\"1\":1}")
        + (remembered == null ? "" : "; mv_datos_cliente=" + Uri.EscapeDataString(remembered));
    if (reseller) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Revendedor"), new Claim(ClaimTypes.NameIdentifier, "test-reseller")], "test"));
    return new TiendaController(products, clients, orders, push, new ConfigurationBuilder().Build(), configService,
        Stub.Make<IAccesoTiendaService>((_, _) => Task.FromResult<Cliente?>(new Cliente { Id = 9, Nombre = "Revendedor", Revendedor = true, Direccion = "Calle 123" })))
    {
        ControllerContext = new ControllerContext { HttpContext = context },
        TempData = new TempDataDictionary(context, Stub.Make<ITempDataProvider>((m, _) => m == "LoadTempData" ? new Dictionary<string, object>() : null))
    };
}
CheckoutViewModel Order(string delivery = "delivery") => new() { Nombre = "Prueba", Telefono = "123456", Direccion = "Calle 123", Entrega = delivery };

settings.MontoMinimoPedido = 101m;
var under = Controller();
var tampered = Order();
tampered.Carrito.Configuracion.MontoMinimoPedido = 0;
Check(await under.Confirmar(tampered) is ViewResult && !under.ModelState.IsValid && writes == 0 && notifications == 0,
    "Below minimum rejected server-side, ignores posted settings, no order or push");
settings.MontoMinimoPedido = 100m;
var exact = Controller();
Check(await exact.Confirmar(Order()) is RedirectToActionResult { ActionName: "Confirmacion" } && writes == 1,
    "Exact minimum can confirm");
var resellerController = Controller(true);
Check(await resellerController.Confirmar(Order()) is ViewResult && writes == 1,
    "Reseller minimum uses reseller price, not retail price");
settings.MontoMinimoPedido = 0m;
settings.RetiroHabilitado = false;
var closed = Controller();
Check(await closed.Confirmar(Order("retiro")) is ViewResult && !closed.ModelState.IsValid && writes == 1,
    "Closed pickup rejected even when posted directly");
var invalid = Controller();
Check(await invalid.Confirmar(Order("inventado")) is ViewResult && writes == 1, "Unknown delivery method rejected");
var deliveryController = Controller();
Check(await deliveryController.Confirmar(Order()) is RedirectToActionResult && writes == 2,
    "Delivery still works while pickup is closed and minimum is zero");
settings.RetiroHabilitado = true;
var pickup = Controller();
Check(await pickup.Confirmar(Order("retiro")) is RedirectToActionResult && writes == 3, "Re-enabled pickup works");

settings.RetiroHabilitado = false;
var remembered = Controller(remembered: "{\"Nombre\":\"Prueba\",\"Telefono\":\"123456\",\"Entrega\":\"retiro\"}");
var checkout = (CheckoutViewModel)((ViewResult)await remembered.Checkout()).Model!;
Check(checkout.Entrega == "delivery" && !checkout.Carrito.Configuracion.RetiroHabilitado,
    "Remembered pickup is replaced with delivery when closed");
Check(configReads >= 8, "Settings read again on each request, no stale cache");

var saved = 0;
var adminSettings = Stub.Make<IConfiguracionTiendaService>((method, values) =>
{
    if (method == "GetAsync") return Task.FromResult(settings);
    if (method != "GuardarAsync") throw new Exception(method);
    saved++;
    settings.RetiroHabilitado = (bool)values![0]!;
    settings.MontoMinimoPedido = (decimal)values[1]!;
    return Task.CompletedTask;
});
using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
var admin = new ConfiguracionController(
    Stub.Make<ICategoriaService>((_, _) => Task.FromResult<IEnumerable<CategoriaProducto>>([])),
    Stub.Make<IFormatoService>((_, _) => Task.FromResult<IEnumerable<Formato>>([])),
    Stub.Make<ITamanoService>((_, _) => Task.FromResult<IEnumerable<Tamano>>([])),
    Stub.Make<IEtiquetaService>((_, _) => Task.FromResult<IEnumerable<Etiqueta>>([])),
    Stub.Make<IHttpClientFactory>((_, _) => throw new Exception("No external requests expected")), cache, adminSettings)
{
    TempData = new TempDataDictionary(new DefaultHttpContext(), Stub.Make<ITempDataProvider>((m, _) => m == "LoadTempData" ? new Dictionary<string, object>() : null))
};
Check(await admin.GuardarTienda(new() { RetiroHabilitado = false, MontoMinimoPedido = 2500.50m }) is RedirectToActionResult
    && saved == 1 && !settings.RetiroHabilitado && settings.MontoMinimoPedido == 2500.50m, "Admin saves both options together");
var loaded = (ConfiguracionViewModel)((ViewResult)await admin.Index()).Model!;
Check(!loaded.Tienda.RetiroHabilitado && loaded.Tienda.MontoMinimoPedido == 2500.50m, "Admin reloads saved settings");
admin.ModelState.AddModelError("Tienda.MontoMinimoPedido", "Invalid amount");
Check(await admin.GuardarTienda(new() { MontoMinimoPedido = -1 }) is ViewResult && saved == 1,
    "Invalid admin form does not change settings");
Console.WriteLine($"{checks} checks passed. No database or production services used.");

// Alta de accesos y redirección: usa los controladores reales con servicios aislados.
var accountCreates = 0;
var resellerClient = new Cliente { Id = 9, Nombre = "Yuca de prueba", Revendedor = true };
var existingEmail = (string?)null;
var accessService = Stub.Make<IAccesoTiendaService>((method, arguments) =>
{
    if (method == "ObtenerEmailAsync") return Task.FromResult(existingEmail);
    if (method == "CrearAsync")
    {
        Check((int)arguments![0]! == resellerClient.Id, "Account linked to route client ID, not posted ID");
        accountCreates++;
        return Task.FromResult(Microsoft.AspNetCore.Identity.IdentityResult.Success);
    }
    throw new Exception(method);
});
var clientAdmin = new ClienteController(
    Stub.Make<IClienteService>((_, _) => Task.FromResult<Cliente?>(resellerClient)), accessService)
{
    TempData = new TempDataDictionary(new DefaultHttpContext(), Stub.Make<ITempDataProvider>((m, _) => m == "LoadTempData" ? new Dictionary<string, object>() : null))
};
var createPage = (CrearAccesoTiendaViewModel)((ViewResult)await clientAdmin.CrearAcceso(9)).Model!;
Check(createPage.IdCliente == 9 && createPage.NombreCliente == resellerClient.Nombre, "Access page uses existing customer");
Check(await clientAdmin.CrearAcceso(9, new CrearAccesoTiendaViewModel { IdCliente = 999, Email = "test@example.com", Password = "fixture-only-123", ConfirmarPassword = "fixture-only-123" }) is RedirectToActionResult && accountCreates == 1,
    "Successful account creation returns to client details");
Check(!clientAdmin.TempData.Values.Any(v => v?.ToString()?.Contains("fixture-only-123") == true), "Password never copied into TempData");
existingEmail = "existing@example.com";
Check(await clientAdmin.CrearAcceso(9) is RedirectToActionResult, "Existing access cannot open a duplicate creation form");
existingEmail = null;
resellerClient.Revendedor = false;
var rejectedModel = new CrearAccesoTiendaViewModel { Password = "fixture-only-123", ConfirmarPassword = "fixture-only-123" };
Check(await clientAdmin.CrearAcceso(9, rejectedModel) is ViewResult && accountCreates == 1 && rejectedModel.Password == "",
    "Retail customer cannot create reseller access and failed form clears password");
Check(typeof(ClienteController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()?.Roles == "Admin",
    "Customer access actions restricted to Admin");
Check(typeof(ClienteController).GetMethod("CrearAcceso", [typeof(int), typeof(CrearAccesoTiendaViewModel)])!
    .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() != null, "Account creation requires antiforgery");
var account = new AccountController(null!, null!)
{
    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
};
account.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Revendedor")], "test"));
Check(account.Login("/Home/Index") is RedirectToActionResult { ControllerName: "Tienda" },
    "Authenticated reseller goes to store even with an admin return URL");
var invalidPassword = new CrearAccesoTiendaViewModel { Email = "test@example.com", Password = "short", ConfirmarPassword = "different" };
Check(!Validator.TryValidateObject(invalidPassword, new ValidationContext(invalidPassword), new List<ValidationResult>(), true),
    "Account form validates password strength and confirmation");
Console.WriteLine($"{checks} total checks passed.");

settings.MontoMinimoPedido = 0;
var linkedStore = Controller(true);
var linkedForm = (CheckoutViewModel)((ViewResult)await linkedStore.Checkout()).Model!;
Check(linkedForm.Nombre == "Revendedor", "Checkout prefills the linked reseller customer");
var linkedOrder = Order();
linkedOrder.Telefono = "999999";
Check(await linkedStore.Confirmar(linkedOrder) is RedirectToActionResult && lastOrder?.IdCliente == 9 && lastOrder.MontoTotal == 80m,
    "Reseller order uses linked customer and reseller price even with a changed contact phone");
Console.WriteLine($"{checks} final checks passed.");

public class Stub : DispatchProxy
{
    public Func<string, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!.Name, args);
    public static T Make<T>(Func<string, object?[]?, object?> handler) where T : class
    {
        var instance = Create<T, Stub>();
        ((Stub)(object)instance).Handler = handler;
        return instance;
    }
}
