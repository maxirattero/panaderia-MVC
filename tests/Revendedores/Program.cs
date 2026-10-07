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
var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    ApplicationName = typeof(TiendaController).Assembly.GetName().Name,
    ContentRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Panaderia.MVC")), EnvironmentName = "Production"
});
builder.WebHost.UseUrls("http://127.0.0.1:5096");
builder.Logging.SetMinimumLevel(LogLevel.Error);
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireRole("Admin").Build())))
    .AddApplicationPart(typeof(TiendaController).Assembly);
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
builder.Services.AddScoped<IPedidoService, PedidoService>();
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
        db.Productos.AddRange(Enumerable.Range(1,4).Select(i => new Producto { Id = i, IdCategoria = i, Nombre = new[] {"Pan de prueba","Crackers de prueba","Prepizza de prueba","Hummus de prueba"}[i-1], PrecioFinal = 100, PrecioReventa = 80, Stock = i == 2 ? 1 : i == 4 ? 3 : 0 }));
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
    var detail = await reseller.GetStringAsync("/Tienda/Detalle/1");
    Check(detail.Contains("Por encargo") && !detail.Contains("temporalmente sin stock"), "Zero-stock bread detail permits ordering");
    page = await Post(reseller, "/Tienda/AgregarVarios", new() { ["cantidades[1]"] = "2", ["cantidades[2]"] = "2", ["cantidades[3]"] = "2", ["cantidades[4]"] = "1" }, catalog);
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
        var service = sp.GetRequiredService<IPedidoService>(); var order = (await service.GetByIdAsync(orderId))!;
        var edit = new Pedido { Id = order.Id, IdCliente = order.IdCliente, FechaEntrega = order.FechaEntrega, MontoTotal = order.MontoTotal + 80,
            Detalles = order.Detalles.Select(d => new DetallePedido { IdProducto = d.IdProducto, Cantidad = d.Cantidad + (d.IdProducto == 1 ? 1 : 0), PrecioUnitario = d.PrecioUnitario }).ToList() };
        await service.UpdateAsync(edit);
    });
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        Check((await db.Productos.OrderBy(p => p.Id).Select(p => p.Stock).ToListAsync()).SequenceEqual(new[] {0,1,0,2}), "Admin editing reseller order preserves stock policy");
        Check(await db.DetallesPedido.CountAsync() == 4 && await db.DetallesPedido.SumAsync(d => d.Cantidad) == 8, "Admin edit keeps every order line");
        await sp.GetRequiredService<IPedidoService>().AnularAsync(orderId);
    });
    await Scope(async sp => {
        var db = sp.GetRequiredService<PanaderiaContext>();
        Check((await db.Productos.OrderBy(p => p.Id).Select(p => p.Stock).ToListAsync()).SequenceEqual(new[] {0,1,0,3}), "Cancellation restores only actually reserved stock");
    });
    Console.WriteLine($"{checks} reseller integration checks passed.");
    if (args.Contains("--preview")) { Console.WriteLine("PREVIEW http://127.0.0.1:5096 — admin@example.test / fixture-admin-123"); await app.WaitForShutdownAsync(); }
}
finally { await app.StopAsync(); await using var cleanup = new PanaderiaContext(options); await cleanup.Database.EnsureDeletedAsync(); }
sealed class Clock : TimeProvider { private readonly global::System.Diagnostics.Stopwatch elapsed = global::System.Diagnostics.Stopwatch.StartNew(); public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-07T15:00:00Z") + elapsed.Elapsed; }
sealed class FakePush : IPushNotificationService {
    public Task GuardarSuscripcionAsync(string userId, SuscripcionPushDto suscripcion) => Task.CompletedTask;
    public Task NotificarNuevoPedidoAsync(Pedido pedido, bool esRevendedor) => Task.CompletedTask;
}
