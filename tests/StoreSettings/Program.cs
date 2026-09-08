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
var configReads = 0;
var product = new Producto { Id = 1, Nombre = "Prueba", PrecioFinal = 100m, PrecioReventa = 80m, PorEncargo = true };
var configService = Stub.Make<IConfiguracionTiendaService>((m, _) => m == "GetAsync"
    ? ReadSettings() : throw new Exception("Unexpected settings mutation"));
Task<ConfiguracionTienda> ReadSettings() { configReads++; return Task.FromResult(settings); }
var products = Stub.Make<IProductoService>((m, _) => m == "GetAllAsync"
    ? Task.FromResult<IEnumerable<Producto>>([product]) : throw new Exception(m));
var clients = Stub.Make<IClienteService>((m, _) => m == "GetByTelefonoAsync"
    ? Task.FromResult<Cliente?>(new Cliente { Id = 1, Nombre = "Prueba", Telefono = "123456", Direccion = "Calle 123" }) : throw new Exception("Unexpected client write"));
var orders = Stub.Make<IPedidoService>((m, _) => { if (m != "CreateAsync") throw new Exception(m); writes++; return Task.CompletedTask; });
var push = Stub.Make<IPushNotificationService>((m, _) => { notifications++; return Task.CompletedTask; });

TiendaController Controller(bool reseller = false, string? remembered = null)
{
    var context = new DefaultHttpContext();
    context.Request.Headers.Cookie = "mv_carrito=" + Uri.EscapeDataString("{\"1\":1}")
        + (remembered == null ? "" : "; mv_datos_cliente=" + Uri.EscapeDataString(remembered));
    if (reseller) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Revendedor")], "test"));
    return new TiendaController(products, clients, orders, push, new ConfigurationBuilder().Build(), configService)
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
