using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.MVC.Controllers;
using Panaderia.Services.Implementations;
using Panaderia.Services.Interfaces;

// Base descartable exclusivamente local; sin configuración ni credenciales de producción.
var database = "revendedores_checks_" + Guid.NewGuid().ToString("N");
var options = new DbContextOptionsBuilder<PanaderiaContext>()
    .UseNpgsql($"Host=127.0.0.1;Port=55459;Username=postgres;Database={database}").Options;
var storeClock = new StoreClock(DateTimeOffset.Parse(args.Contains("--sabado")
    ? "2026-10-31T09:00:00-03:00" : "2026-10-30T10:00:00-03:00"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    ApplicationName = typeof(TiendaController).Assembly.GetName().Name,
    ContentRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Panaderia.MVC")), EnvironmentName = "Production"
});
builder.WebHost.UseUrls("http://127.0.0.1:5096");
builder.Logging.SetMinimumLevel(LogLevel.Error);
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireRole("Admin").Build())))
    .AddApplicationPart(typeof(TiendaController).Assembly).AddControllersAsServices();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddScoped(_ => new PanaderiaContext(options));
builder.Services.AddDefaultIdentity<ApplicationUser>(o => {
    o.Password.RequiredLength = 6; o.Password.RequireUppercase = false; o.Password.RequireNonAlphanumeric = false;
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<PanaderiaContext>();
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
builder.Services.ConfigureApplicationCookie(o => { o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/Login"; });
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IAccesoTiendaService, AccesoTiendaService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IPedidoService>(sp => new PedidoService(sp.GetRequiredService<PanaderiaContext>(), storeClock));
builder.Services.AddTransient<TiendaController>(sp => new TiendaController(
    sp.GetRequiredService<IProductoService>(), sp.GetRequiredService<IClienteService>(),
    sp.GetRequiredService<IPedidoService>(), sp.GetRequiredService<IPushNotificationService>(),
    sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IConfiguracionTiendaService>(),
    sp.GetRequiredService<IAccesoTiendaService>(), storeClock));
builder.Services.AddScoped<IRecetaService, RecetaService>();
builder.Services.AddScoped<IInsumoService, InsumoService>();
builder.Services.AddScoped<IConfiguracionTiendaService, ConfiguracionTiendaService>();
builder.Services.AddSingleton<IPushNotificationService, FakePush>();
builder.Services.AddSingleton<TimeProvider>(new Clock());
await using var app = builder.Build();
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Tienda}/{action=Index}/{id?}");
int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); checks++; }
async Task Scope(Func<IServiceProvider, Task> action) { await using var scope = app.Services.CreateAsyncScope(); await action(scope.ServiceProvider); }
HttpClient Browser() => new(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = new Uri("http://127.0.0.1:5096") };
string Token(string html) => WebUtility.HtmlDecode(Regex.Match(Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value, "value=\"([^\"]+)\"").Groups[1].Value);
async Task<string> Post(HttpClient client, string path, Dictionary<string,string> fields, string tokenPage) {
    fields["__RequestVerificationToken"] = Token(tokenPage);
    var response = await client.PostAsync(path, new FormUrlEncodedContent(fields));
    response.EnsureSuccessStatusCode(); return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
}
async Task<bool> Login(HttpClient client, string email, string password) {
    var form = await client.GetStringAsync("/Account/Login");
    var html = await Post(client, "/Account/Login?returnUrl=/Cliente", new() { ["Email"] = email, ["Password"] = password }, form);
    return html.Contains("action=\"/Account/Logout\"");
}
Dictionary<string,string> Edit(bool enabled = true, string email = "reseller@example.test", string password = "", string name = "Revendedor de prueba") => new() {
    ["Id"] = "1", ["Nombre"] = name, ["Revendedor"] = "true", ["AccesoTienda"] = enabled.ToString(),
    ["EmailAcceso"] = email, ["NuevaPassword"] = password, ["ConfirmarPassword"] = password
};
try {
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>(); await db.Database.MigrateAsync();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new("Admin")); await roles.CreateAsync(new("Revendedor"));
        db.Clientes.AddRange(new Cliente { Id = 1, Nombre = "Revendedor de prueba", Revendedor = true }, new Cliente { Id = 2, Nombre = "Minorista" });
        db.CategoriasProducto.AddRange(new CategoriaProducto { Id = 1, Nombre = "Pan" }, new CategoriaProducto { Id = 2, Nombre = "Crackers" }, new CategoriaProducto { Id = 3, Nombre = "Prepizza" }, new CategoriaProducto { Id = 4, Nombre = "Untables" });
        db.Productos.AddRange(Enumerable.Range(1,4).Select(i => new Producto { Id = i, IdCategoria = i, Nombre = new[] {"Pan de prueba","Crackers de prueba","Prepizza de prueba","Hummus de prueba"}[i-1], TieneDisponibilidadSemanal = i < 4, PrecioFinal = 100, PrecioReventa = 80, Stock = i == 2 ? 1 : i == 4 ? 3 : 0 }));
        await db.SaveChangesAsync();
        await sp.GetRequiredService<IConfiguracionTiendaService>().GuardarAsync(false, 10000);
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = "admin@example.test", Email = "admin@example.test" };
        Check((await users.CreateAsync(admin, "fixture-admin-123")).Succeeded, "Local admin created"); await users.AddToRoleAsync(admin, "Admin");
        var other = new ApplicationUser { UserName = "used@example.test", Email = "used@example.test" };
        await users.CreateAsync(other, "fixture-other-123");
    });
    await app.StartAsync();
    using var adminBrowser = Browser();
    Check(await Login(adminBrowser, "admin@example.test", "fixture-admin-123"), "Admin login");
    var editPage = await adminBrowser.GetStringAsync("/Cliente/Edit/1");
    Check(editPage.Contains("name=\"AccesoTienda\"") && editPage.Contains("name=\"NuevaPassword\""), "Client edit renders access controls");
    var page = await Post(adminBrowser, "/Cliente/Edit/1", Edit(password: "fixture-reseller-123"), editPage);
    Check(!page.Contains("name=\"NuevaPassword\""), "Client edit creates account successfully");
    using var reseller = Browser();
    Check(await Login(reseller, "reseller@example.test", "fixture-reseller-123"), "New reseller can log in");
    Check((await reseller.GetStringAsync("/Cliente/Edit/1")).Contains("Tienda - Masa Viva"), "Reseller cannot edit clients");
    var catalog = WebUtility.HtmlDecode(await reseller.GetStringAsync("/"));
    Check(!catalog.Contains("Última unidad") && !catalog.Contains("tc-tag agotado"), "Own products available without stock or scarcity notices");
    Check(!catalog.Contains("Cerramos la toma") && !catalog.Contains("hasta los jueves"), "Logged-in reseller sees no retail closure notice");
    var detail = await reseller.GetStringAsync("/Tienda/Detalle/1");
    Check(detail.Contains("Por encargo") && !detail.Contains("temporalmente sin stock"), "Zero-stock bread detail permits ordering");
    page = await Post(reseller, "/Tienda/Agregar", new() { ["id"] = "1", ["cantidad"] = "1" }, detail);
    Check(page.Contains("Pan de prueba") && page.Contains("Confirmar pedido"), "Reseller can add zero-stock bread during closure");
    page = await Post(reseller, "/Tienda/Actualizar", new() { ["id"] = "1", ["cantidad"] = "2" }, page);
    Check(!page.Contains("pedidos semanales cerrados"), "Reseller can increase bread quantity during closure");
    page = await Post(reseller, "/Tienda/AgregarVarios", new() { ["cantidades[2]"] = "2", ["cantidades[3]"] = "2", ["cantidades[4]"] = "1" }, catalog);
    Check(page.Contains("Confirmar pedido") && !page.Contains("mínimo") && !page.Contains("delivery o retiro") && !page.Contains(">Continuar"), "Single order summary, no minimum or delivery step");
    Check((await reseller.GetStringAsync("/Tienda/Checkout")).Contains("Confirmar pedido"), "Old checkout URL redirects to summary");
    page = await Post(reseller, "/Tienda/Confirmar", new() { ["Nombre"] = "Another customer", ["Telefono"] = "invalid", ["Entrega"] = "inventado", ["Carrito.Total"] = "1" }, page);
    Check(page.Contains("¡Pedido confirmado!") && !page.Contains("wa.me") && !page.Contains("Kiosco") && !page.Contains("Te lo llevamos"), "Direct confirmation ignores forged retail data and does not open WhatsApp");
    int orderId = 0;
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>(); var order = await db.Pedidos.Include(p => p.Detalles).SingleAsync(); orderId = order.Id;
        Check(order.IdCliente == 1 && order.MontoTotal == 560 && order.Detalles.Sum(d => d.Cantidad) == 7, "Persisted order uses linked client, wholesale prices and all quantities");
        Check(order.Detalles.Where(d => d.IdProducto < 4).All(d => !d.ReservaStock) && order.Detalles.Single(d => d.IdProducto == 4).ReservaStock, "Only resold product reserves stock");
        Check((await db.Productos.OrderBy(p => p.Id).Select(p => p.Stock).ToListAsync()).SequenceEqual(new[] {0,1,0,2}), "Own stock preserved; hummus deducted exactly once");
    });
    Check((await reseller.GetStringAsync("/Tienda/Carrito")).Contains("carrito-vacio"), "Confirmed cart is emptied");
    // El horario cerrado no se puede eludir con datos de formulario ni usando
    // el teléfono de un cliente revendedor sin haber iniciado sesión.
    using (var guest = Browser())
    {
        var closedCatalog = WebUtility.HtmlDecode(await guest.GetStringAsync("/"));
        Check(closedCatalog.Contains("Cerramos la toma") && closedCatalog.Contains("No disponible"), "Retail still sees the closed catalog");
        var refused = await Post(guest, "/Tienda/Agregar", new() { ["id"] = "1", ["esRevendedorAutenticado"] = "true" }, closedCatalog);
        Check(refused.Contains("Pedidos cerrados"), "Guest cannot bypass closure by posting reseller flag");
        guest.DefaultRequestHeaders.Add("Cookie", "mv_carrito=" + Uri.EscapeDataString("{\"2\":1}"));
        var oldCart = WebUtility.HtmlDecode(await guest.GetStringAsync("/Tienda/Carrito"));
        Check(oldCart.Contains("pedidos semanales cerrados"), "Guest stale cart remains blocked");
        refused = await Post(guest, "/Tienda/Confirmar", new() { ["esRevendedorAutenticado"] = "true", ["Nombre"] = "Invitado", ["Telefono"] = "123456" }, oldCart);
        Check(refused.Contains("pedidos semanales cerrados"), "Forged guest confirmation stays blocked");
    }
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        var request = new Pedido { IdCliente = 1, FechaCreacion = DateTime.UtcNow,
            Detalles = [new DetallePedido { IdProducto = 1, Cantidad = 1, PrecioUnitario = 80 }] };
        try { await sp.GetRequiredService<IPedidoService>().CrearOAmpliarDesdeTiendaAsync(request); throw new Exception("Unauthenticated reseller bypassed closure"); }
        catch (InvalidOperationException ex) { Check(ex.Message == DisponibilidadSemanal.MensajeCarrito, "Service requires authenticated reseller context, not just a wholesale client ID"); }
        Check(await db.Pedidos.CountAsync() == 1, "Blocked retail requests did not create orders");
    });
    // Continuar las regresiones de stock minorista y admin en un día abierto.
    storeClock.Now = DateTimeOffset.Parse("2026-10-28T10:00:00-03:00");
    // A normal customer still has the retail flow and cannot buy zero-stock products.
    using var retail = Browser();
    var retailCatalog = await retail.GetStringAsync("/");
    page = await Post(retail, "/Tienda/Agregar", new() { ["id"] = "4" }, retailCatalog);
    Check(!page.Contains("Confirmar pedido") && page.Contains("Continuar") && page.Contains("mínimo"), "Retail keeps minimum and multi-step checkout");
    var noCsrf = await adminBrowser.PostAsync("/Cliente/Edit/1", new FormUrlEncodedContent(Edit()));
    Check(noCsrf.StatusCode == HttpStatusCode.BadRequest, "Credential mutation requires antiforgery");
    // Duplicate address must roll back customer edits and must never echo a password.
    editPage = await adminBrowser.GetStringAsync("/Cliente/Edit/1");
    page = await Post(adminBrowser, "/Cliente/Edit/1", Edit(email: "used@example.test", password: "fixture-secret-456", name: "Must not persist"), editPage);
    Check(page.Contains("asociado a otra cuenta") && !page.Contains("fixture-secret-456"), "Duplicate email rejected; password excluded from error HTML");
    await Scope(async sp => Check((await sp.GetRequiredService<PanaderiaContext>().Clientes.FindAsync(1))!.Nombre == "Revendedor de prueba", "Invalid credentials preserve client data"));
    page = await Post(adminBrowser, "/Cliente/Edit/1", Edit(email: "tést@example.test", name: "Must roll back"), editPage);
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        Check((await db.Clientes.FindAsync(1))!.Nombre == "Revendedor de prueba" && (await db.Users.SingleAsync(u => u.IdCliente == 1)).Email == "reseller@example.test",
            "Identity validation failure rolls back both client and account writes");
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("reseller@example.test");
        await users.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(5));
        Check(await sp.GetRequiredService<IAccesoTiendaService>().TieneAccesoAsync(1), "Temporary login lockout is not mistaken for disabled access");
        await users.SetLockoutEndDateAsync(user!, null);
    });
    // Blank password preserves the hash; changing username invalidates the previous cookie.
    editPage = await adminBrowser.GetStringAsync("/Cliente/Edit/1");
    await Post(adminBrowser, "/Cliente/Edit/1", Edit(email: "renamed@example.test"), editPage);
    Check(!(await reseller.GetStringAsync("/")).Contains("action=\"/Account/Logout\""), "Email change invalidates active session immediately");
    using var renamed = Browser();
    Check(await Login(renamed, "renamed@example.test", "fixture-reseller-123"), "New username works with unchanged password");
    editPage = await adminBrowser.GetStringAsync("/Cliente/Edit/1");
    await Post(adminBrowser, "/Cliente/Edit/1", Edit(email: "renamed@example.test", password: "fixture-new-password-456"), editPage);
    Check(!(await renamed.GetStringAsync("/")).Contains("action=\"/Account/Logout\""), "Password change revokes active session");
    using var oldPassword = Browser(); Check(!await Login(oldPassword, "renamed@example.test", "fixture-reseller-123"), "Old password rejected");
    using var current = Browser(); Check(await Login(current, "renamed@example.test", "fixture-new-password-456"), "New password works");
    editPage = await adminBrowser.GetStringAsync("/Cliente/Edit/1");
    await Post(adminBrowser, "/Cliente/Edit/1", Edit(enabled: false, email: "renamed@example.test"), editPage);
    Check(!(await current.GetStringAsync("/")).Contains("action=\"/Account/Logout\""), "Disabling access revokes the cookie");
    using var disabled = Browser(); Check(!await Login(disabled, "renamed@example.test", "fixture-new-password-456"), "Disabled account cannot log in");
    editPage = await adminBrowser.GetStringAsync("/Cliente/Edit/1");
    await Post(adminBrowser, "/Cliente/Edit/1", Edit(email: "renamed@example.test"), editPage);
    using var enabled = Browser(); Check(await Login(enabled, "renamed@example.test", "fixture-new-password-456"), "Re-enabled account keeps its password");
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        Check(await db.Users.CountAsync(u => u.IdCliente == 1) == 1 && await db.Pedidos.CountAsync() == 1, "Access toggles preserve account and order history");
        (await db.DetallesPedido.SingleAsync(d => d.IdProducto == 2)).CantidadProducida = 1;
        await db.SaveChangesAsync();
    });
    async Task<Dictionary<string,string>> StoredOrderForm(string date = "2026-11-02") {
        var form = new Dictionary<string,string> { ["Id"] = orderId.ToString(), ["IdCliente"] = "1", ["FechaEntrega"] = date };
        await Scope(async sp => {
            var order = await sp.GetRequiredService<PanaderiaContext>().Pedidos.Include(p => p.Detalles).SingleAsync(p => p.Id == orderId);
            form["Notas"] = order.Notas ?? "";
            int i = 0;
            foreach (var d in order.Detalles.OrderBy(d => d.Id)) {
                form[$"Detalles[{i}].Id"] = d.Id.ToString();
                form[$"Detalles[{i}].IdProducto"] = d.IdProducto.ToString();
                form[$"Detalles[{i}].Cantidad"] = d.Cantidad.ToString();
                i++;
            }
        });
        return form;
    }
    editPage = await adminBrowser.GetStringAsync($"/Pedido/Edit/{orderId}");
    Check(editPage.Contains("const encargosOriginales = {") && !editPage.Contains("const encargosOriginales = {};"), "Editor receives persisted made-to-order line identities");
    var datedForm = await StoredOrderForm();
    page = await Post(adminBrowser, $"/Pedido/Edit/{orderId}", datedForm, editPage);
    Check(!page.Contains("No hay stock suficiente") && !page.Contains("id=\"detallesBody\""), "Changing only the date saves a wholesale store order without stock");
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>(); var order = await db.Pedidos.Include(p => p.Detalles).SingleAsync();
        Check(order.FechaEntrega == new DateTime(2026,11,2,0,0,0,DateTimeKind.Utc) && order.MontoTotal == 560 && order.Detalles.Sum(d => d.Cantidad) == 7, "Date change preserves total and all quantities");
        Check(order.Detalles.Single(d => d.IdProducto == 2).CantidadProducida == 1 && order.Detalles.Where(d => d.IdProducto < 4).All(d => !d.ReservaStock), "Date change preserves production progress and made-to-order lines");
        Check((await db.Productos.OrderBy(p => p.Id).Select(p => p.Stock).ToListAsync()).SequenceEqual(new[] {0,1,0,2}), "Date change leaves every stock unchanged");
    });
    page = await Post(adminBrowser, $"/Pedido/Edit/{orderId}", datedForm, editPage);
    Check(page.Contains("Recargá la página"), "Stale line identities rejected without reapplying stock changes");
    var increasedForm = await StoredOrderForm();
    var crackerKey = increasedForm.Single(x => x.Key.EndsWith(".IdProducto") && x.Value == "2").Key.Replace(".IdProducto", ".Cantidad");
    increasedForm[crackerKey] = "3";
    page = await Post(adminBrowser, $"/Pedido/Edit/{orderId}", increasedForm, await adminBrowser.GetStringAsync($"/Pedido/Edit/{orderId}"));
    Check(!page.Contains("No hay stock suficiente"), "Increasing an existing made-to-order line still does not require stock");
    var mixedForm = await StoredOrderForm();
    mixedForm["Detalles[4].IdProducto"] = "2"; mixedForm["Detalles[4].Cantidad"] = "1";
    await Post(adminBrowser, $"/Pedido/Edit/{orderId}", mixedForm, await adminBrowser.GetStringAsync($"/Pedido/Edit/{orderId}"));
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        var crackers = await db.DetallesPedido.Where(d => d.IdProducto == 2).ToListAsync();
        Check(crackers.Count == 2 && crackers.Single(d => !d.ReservaStock).Cantidad == 3 && crackers.Single(d => d.ReservaStock).Cantidad == 1,
            "Mixed order preserves made-to-order crackers separately from a new admin stock line");
        Check((await db.Productos.FindAsync(2))!.Stock == 0, "Only the new stock line reserves the last cracker");
    });
    await Post(adminBrowser, $"/Pedido/Edit/{orderId}", await StoredOrderForm("2026-11-03"), await adminBrowser.GetStringAsync($"/Pedido/Edit/{orderId}"));
    var wrongProduct = await StoredOrderForm();
    var breadKey = wrongProduct.Single(x => x.Key.EndsWith(".IdProducto") && x.Value == "1").Key;
    wrongProduct[breadKey] = "4"; wrongProduct[breadKey.Replace(".IdProducto", ".Cantidad")] = "10";
    page = await Post(adminBrowser, $"/Pedido/Edit/{orderId}", wrongProduct, await adminBrowser.GetStringAsync($"/Pedido/Edit/{orderId}"));
    Check(page.Contains("No hay stock suficiente"), "Replacing an old made-to-order product cannot bypass stock of another product");
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        Check((await db.Productos.OrderBy(p => p.Id).Select(p => p.Stock).ToListAsync()).SequenceEqual(new[] {0,0,0,2}), "Mixed date edit and failed product replacement preserve reservations");
        await sp.GetRequiredService<IPedidoService>().AnularAsync(orderId);
    });
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        Check((await db.Productos.OrderBy(p => p.Id).Select(p => p.Stock).ToListAsync()).SequenceEqual(new[] {0,1,0,3}), "Cancellation restores only actual reservations, never made-to-order quantities");
        var pizza = (await db.Productos.FindAsync(3))!; pizza.Stock = 1; pizza.SinStock = false;
        var bread = (await db.Productos.FindAsync(1))!; bread.PorEncargo = true; bread.Stock = 0;
        await db.SaveChangesAsync();
    });

    Dictionary<string,string> AdminOrder(int id = 0, int pizza = 1) => new() {
        ["Id"] = id.ToString(), ["IdCliente"] = "1", ["FechaEntrega"] = "2026-10-10",
        ["Detalles[0].IdProducto"] = "1", ["Detalles[0].Cantidad"] = "2",
        ["Detalles[1].IdProducto"] = "3", ["Detalles[1].Cantidad"] = pizza.ToString()
    };
    page = await Post(adminBrowser, "/Pedido/Create", AdminOrder(), await adminBrowser.GetStringAsync("/Pedido/Create"));
    Check(!page.Contains("No hay stock suficiente"), "Admin creates reseller order with the last prepizza");
    int adminOrderId = 0;
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        var order = await db.Pedidos.Include(p => p.Detalles).SingleAsync(); adminOrderId = order.Id;
        var pizza = (await db.Productos.FindAsync(3))!;
        Check(pizza.Stock == 0 && pizza.SinStock && pizza.EstaSinStockEnTienda, "Last unit is deducted and becomes unavailable for retail");
        Check(order.Detalles.Single(d => d.IdProducto == 3).ReservaStock && !order.Detalles.Single(d => d.IdProducto == 1).ReservaStock,
            "Admin respects product configuration: prepizza uses stock and made-to-order bread does not");
        Check(order.MontoTotal == 240 && order.Detalles.All(d => d.PrecioUnitario == 80), "Admin retains wholesale prices independently of stock policy");
    });
    Check((await retail.GetStringAsync("/Tienda/Detalle/3")).Contains("temporalmente sin stock"), "Retail storefront no longer offers the last prepizza");
    Check((await enabled.GetStringAsync("/Tienda/Detalle/3")).Contains("Por encargo"), "Reseller storefront still permits ordering prepizza without stock");
    editPage = await adminBrowser.GetStringAsync($"/Pedido/Edit/{adminOrderId}");
    Check(editPage.Contains("const reservasIniciales = {\"3\":1};"), "Editor counts only actual reservations as available units");
    await Post(adminBrowser, $"/Pedido/Edit/{adminOrderId}", AdminOrder(adminOrderId), editPage);
    page = await Post(adminBrowser, $"/Pedido/Edit/{adminOrderId}", AdminOrder(adminOrderId, 2), editPage);
    Check(page.Contains("No hay stock suficiente"), "Admin cannot increase reseller order beyond available stock");
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        Check((await db.Productos.FindAsync(3))!.Stock == 0 && (await db.DetallesPedido.SingleAsync(d => d.IdProducto == 3)).Cantidad == 1,
            "Unchanged edit and rejected increase do not double-deduct or release the last unit");
    });
    page = await Post(adminBrowser, "/Pedido/Create", AdminOrder(), await adminBrowser.GetStringAsync("/Pedido/Create"));
    Check(page.Contains("No hay stock suficiente"), "Another admin order cannot reserve the same last unit");
    var removePizza = AdminOrder(adminOrderId); removePizza.Remove("Detalles[1].IdProducto"); removePizza.Remove("Detalles[1].Cantidad");
    await Post(adminBrowser, $"/Pedido/Edit/{adminOrderId}", removePizza, editPage);
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>(); var pizza = (await db.Productos.FindAsync(3))!;
        Check(pizza.Stock == 1 && !pizza.SinStock, "Removing prepizza returns its reservation to the store");
    });
    await Post(adminBrowser, $"/Pedido/Edit/{adminOrderId}", AdminOrder(adminOrderId), editPage);
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        Check((await db.Productos.FindAsync(3))!.Stock == 0, "Adding prepizza to an existing reseller order reserves the last unit");
        await sp.GetRequiredService<IPedidoService>().AnularAsync(adminOrderId);
    });
    Console.WriteLine($"{checks} reseller integration checks passed.");
    if (args.Contains("--preview")) { Console.WriteLine("PREVIEW http://127.0.0.1:5096 — admin@example.test / fixture-admin-123"); await app.WaitForShutdownAsync(); }
}
finally { await app.StopAsync(); await using var cleanup = new PanaderiaContext(options); await cleanup.Database.EnsureDeletedAsync(); }
// Las cookies de autenticación deben tener una vigencia real para el navegador HTTP.
sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow; }
sealed class FakePush : IPushNotificationService {
    public Task GuardarSuscripcionAsync(string userId, SuscripcionPushDto suscripcion) => Task.CompletedTask;
    public Task NotificarNuevoPedidoAsync(Pedido pedido, bool esRevendedor) => Task.CompletedTask;
}

sealed class StoreClock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime(); }
