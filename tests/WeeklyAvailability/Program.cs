using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.MVC.Controllers;
using Panaderia.MVC.Models;
using Panaderia.Services.Implementations;
using Panaderia.Services.Interfaces;

var checks = 0;
void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
    checks++;
}
async Task Reject(Func<Task> action, string message)
{
    try { await action(); }
    catch (InvalidOperationException ex) when (ex.Message == DisponibilidadSemanal.MensajeCarrito)
    { Check(true, message); return; }
    throw new Exception("Not rejected: " + message);
}
DateTimeOffset Local(string time) => DateTimeOffset.Parse(time + "-03:00");
var reloj = new TestClock(Local("2026-09-25T10:00:00"));
var pan = new Producto { Id = 1, Nombre = "Pan", TieneDisponibilidadSemanal = true, PorEncargo = true, PrecioFinal = 100 };
var pizza = new Producto { Id = 2, Nombre = "Pizza", PorEncargo = true, PrecioFinal = 50 };
var fixture = new List<Producto> { pan, pizza };

foreach (var (time, closed) in new[] {
    ("2026-09-24T23:59:59.9999999", false), ("2026-09-25T00:00:00", true),
    ("2026-09-25T23:59:59", true), ("2026-09-26T00:00:00", true),
    ("2026-09-26T11:59:59.9999999", true), ("2026-09-26T12:00:00", false),
    ("2026-09-27T00:00:00", false), ("2026-09-28T00:00:00", false),
    ("2026-09-29T00:00:00", false), ("2026-09-30T00:00:00", false) })
{
    Check(DisponibilidadSemanal.EstaBloqueado(pan, Local(time).ToUniversalTime()) == closed, "Argentina boundary " + time);
    Check(!DisponibilidadSemanal.EstaBloqueado(pizza, Local(time)), "Unflagged product available " + time);
}
pan.PorEncargo = false; pan.Stock = 10;
Check(DisponibilidadSemanal.EstaBloqueado(pan, reloj.GetUtcNow()), "Stock does not bypass weekly closure");
pan.PorEncargo = true;
var writes = 0;
var clientCalls = 0;
var notifications = 0;
var cerrarAlGuardar = false;
var products = Stub.Make<IProductoService>((method, values) => method switch {
    "GetAllAsync" => Task.FromResult<IEnumerable<Producto>>(fixture),
    "GetByIdAsync" => Task.FromResult(fixture.FirstOrDefault(p => p.Id == (int)values![0]!)),
    _ => throw new Exception(method)
});
var clients = Stub.Make<IClienteService>((method, _) => {
    clientCalls++;
    if (method != "GetByTelefonoAsync") throw new Exception("Unexpected client mutation");
    return Task.FromResult<Cliente?>(new() { Id = 1, Nombre = "Prueba", Telefono = "123456", Direccion = "Calle 123" });
});
var orders = Stub.Make<IPedidoService>((method, values) => {
    if (method != "CrearOAmpliarDesdeTiendaAsync") throw new Exception(method);
    if (cerrarAlGuardar)
    {
        reloj.Now = Local("2026-09-25T00:00:00");
        throw new InvalidOperationException(DisponibilidadSemanal.MensajeCarrito);
    }
    writes++; return Task.FromResult((Pedido)values![0]!);
});
var push = Stub.Make<IPushNotificationService>((_, _) => { notifications++; return Task.CompletedTask; });
var settings = Stub.Make<IConfiguracionTiendaService>((_, _) => Task.FromResult(new ConfiguracionTienda()));
if (args.Contains("--preview"))
{
    await PreviewHost.RunAsync(reloj, products, clients, orders, push, settings);
    return;
}
TiendaController Controller(Dictionary<int, int>? cart = null)
{
    var context = new DefaultHttpContext();
    context.Request.Headers.Cookie = "mv_carrito=" + Uri.EscapeDataString(JsonSerializer.Serialize(cart ?? new() { [1] = 2, [2] = 1 }));
    return new(products, clients, orders, push, new ConfigurationBuilder().Build(), settings,
        Stub.Make<IAccesoTiendaService>((_, _) => Task.FromResult<Cliente?>(null)), reloj)
    {
        ControllerContext = new() { HttpContext = context },
        TempData = new TempDataDictionary(context, Stub.Make<ITempDataProvider>((m, _) => m == "LoadTempData" ? new Dictionary<string, object>() : null))
    };
}
CheckoutViewModel Form() => new() { Nombre = "Prueba", Telefono = "123456", Direccion = "Calle 123", Entrega = "delivery" };
bool CartWritten(TiendaController c) => c.Response.Headers.SetCookie.Any(v => v?.StartsWith("mv_carrito=") == true);
var catalog = (TiendaIndexViewModel)((ViewResult)await Controller().Index(null, null, null)).Model!;
Check(catalog.Productos.Count == 2 && catalog.PedidosSemanalesCerrados, "Closed breads remain in catalog alongside other products");
var detail = Controller(); await detail.Detalle(1);
Check(detail.ViewBag.BloqueadoPorCierreSemanal is true, "Detail receives closed state");
var add = Controller(); await add.Agregar(1);
Check(!CartWritten(add) && add.TempData["TiendaMsg"]?.ToString() == DisponibilidadSemanal.MensajeCierre, "Direct add rejected server-side");
var bulk = Controller(); await bulk.AgregarVarios(new() { [1] = 1, [2] = 1 });
Check(!CartWritten(bulk), "Stale mixed catalog submission is rejected without partial additions");
var update = Controller(); await update.Actualizar(1, 3);
Check(!CartWritten(update), "Direct quantity increase rejected");
var reduce = Controller(); await reduce.Actualizar(1, 1);
Check(CartWritten(reduce), "Closed bread quantity can be reduced");
var remove = Controller(); remove.Quitar(1);
Check(CartWritten(remove), "Closed bread can be removed");
var cartController = Controller();
var cartModel = (CarritoViewModel)((ViewResult)await cartController.Carrito()).Model!;
Check(cartModel.Items.Count == 2 && cartModel.Items.Single(i => i.Producto.Id == 1).BloqueadoPorCierreSemanal
    && !cartModel.Items.Single(i => i.Producto.Id == 2).BloqueadoPorCierreSemanal && !CartWritten(cartController),
    "Old mixed cart retains both items and identifies blocked bread");
Check(await Controller().Checkout() is RedirectToActionResult { ActionName: "Carrito" }, "Checkout GET blocked for old cart");
Check(await Controller().Confirmar(Form()) is RedirectToActionResult { ActionName: "Carrito" }
    && writes == 0 && clientCalls == 0 && notifications == 0, "Forged confirmation causes no order, customer, or notification writes");
var onlyPizza = Controller(new() { [2] = 1 });
Check(await onlyPizza.Confirmar(Form()) is RedirectToActionResult { ActionName: "Confirmacion" } && writes == 1,
    "Other products can confirm on Friday after removing bread");
var addPizza = Controller(); await addPizza.Agregar(2);
Check(CartWritten(addPizza), "Other products can be added during closure");
reloj.Now = Local("2026-09-26T12:00:00");
var reopened = Controller(); await reopened.Agregar(1);
Check(CartWritten(reopened), "Bread add reopens exactly at noon");
Check(await Controller().Confirmar(Form()) is RedirectToActionResult { ActionName: "Confirmacion" } && writes == 2,
    "Mixed cart confirms at reopening");
reloj.Now = Local("2026-09-24T23:59:59");
cerrarAlGuardar = true;
var beforeClosingNotifications = notifications;
Check(await Controller().Confirmar(Form()) is RedirectToActionResult { ActionName: "Carrito" }
    && writes == 2 && notifications == beforeClosingNotifications, "Closure during save returns to cart without confirming or notifying");

// Integration against a unique disposable local PostgreSQL database. No production settings.
var database = "weekly_checks_" + Guid.NewGuid().ToString("N");
var port = int.Parse(Environment.GetEnvironmentVariable("WEEKLY_TEST_PORT") ?? "55449");
var options = new DbContextOptionsBuilder<PanaderiaContext>()
    .UseNpgsql($"Host=127.0.0.1;Port={port};Username=postgres;Database={database};Timeout=5").Options;
PanaderiaContext Db() => new(options);
try
{
    await using (var db = Db())
    {
        await db.GetService<IMigrator>().MigrateAsync("20260913190057_OrganizarCierreCajaPorCuentas");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "CategoriasProducto" ("Id","Nombre","FechaCreacion")
            VALUES (101,'Panes',now()), (102,'Pizza',now()), (103,'Pan dulce',now()), (104,' PAN ',now());
            INSERT INTO "Productos" ("Id","IdCategoria","Nombre","PrecioFinal","PrecioReventa","Stock","FechaCreacion","PorEncargo")
            VALUES (101,101,'Pan',100,80,10,now(),true), (102,102,'Pizza',50,40,10,now(),false),
                   (103,103,'Pan dulce',80,60,10,now(),true), (104,104,'Pan integral',100,80,10,now(),true);
            """);
        await db.Database.MigrateAsync();
        Check(!db.Database.HasPendingModelChanges(), "Migration and EF model match");
        var migrated = await db.Productos.OrderBy(p => p.Id).ToListAsync();
        Check(migrated.Select(p => p.TieneDisponibilidadSemanal).SequenceEqual(new[] { true, false, false, true }),
            "Migration flags only Pan/Panes categories, not pizza or pan dulce");
        Check(migrated.All(p => p.Stock == 10), "Migration preserves stock");
        db.Clientes.Add(new() { Id = 101, Nombre = "Weekly test" });
        await db.SaveChangesAsync();
    }
    Pedido Order(params int[] ids) => new() { IdCliente = 101, FechaCreacion = DateTime.UtcNow,
        FechaEntrega = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc), MontoTotal = ids.Length * 100,
        Detalles = ids.Select(id => new DetallePedido { IdProducto = id, Cantidad = 1, PrecioUnitario = 100 }).ToList() };
    reloj.Now = Local("2026-09-25T00:00:00");
    await using (var db = Db())
    {
        await Reject(() => new PedidoService(db, reloj).CrearOAmpliarDesdeTiendaAsync(Order(101, 102)), "Service rejects mixed order using persisted flags");
        Check(!await db.Pedidos.AnyAsync() && !await db.DetallesPedido.AnyAsync() && (await db.Productos.FindAsync(102))!.Stock == 10,
            "Rejected service order preserves orders and stock");
    }
    await using (var db = Db())
        await new PedidoService(db, reloj).CrearOAmpliarDesdeTiendaAsync(Order(102));
    await using (var db = Db())
    {
        await Reject(() => new PedidoService(db, reloj).CrearOAmpliarDesdeTiendaAsync(Order(101)), "Service prevents adding bread to an existing Saturday order");
        Check(await db.Pedidos.CountAsync() == 1 && await db.DetallesPedido.CountAsync() == 1 && (await db.Productos.FindAsync(102))!.Stock == 9,
            "Other products reserve stock normally; rejected merge changes nothing");
    }
    reloj.Now = Local("2026-09-26T12:00:00");
    await using (var db = Db())
        await new PedidoService(db, reloj).CrearOAmpliarDesdeTiendaAsync(Order(101));
    await using (var db = Db())
    {
        Check(await db.Pedidos.CountAsync() == 1 && await db.DetallesPedido.CountAsync() == 2, "Service allows bread and existing-order merge at noon");
        var service = new ProductoService(db);
        var copy = await service.DuplicarAsync(101);
        Check(copy!.TieneDisponibilidadSemanal, "Duplicating bread preserves weekly flag");
        await service.UpdateAsync(new Producto { Id = 101, IdCategoria = 101, Nombre = "Pan editado", PorEncargo = true, TieneDisponibilidadSemanal = false });
        db.ChangeTracker.Clear();
        Check(!(await service.GetByIdAsync(101))!.TieneDisponibilidadSemanal, "Admin can persist flag changes");
    }
    // A request starts on Thursday and reaches persistence on Friday.
    var crossingClock = new CrossingClock(Local("2026-09-24T23:59:59"), Local("2026-09-25T00:00:00"));
    await using (var db = Db())
        await Reject(() => new PedidoService(db, crossingClock).CrearOAmpliarDesdeTiendaAsync(Order(104, 102)), "Service rechecks closure before saving");
    await using (var db = Db())
        Check((await db.Productos.FindAsync(102))!.Stock == 9 && await db.DetallesPedido.CountAsync() == 2, "Cross-boundary failure rolls back stock and order changes");
}
finally
{
    await using var cleanup = Db();
    await cleanup.Database.EnsureDeletedAsync();
}
Console.WriteLine($"{checks} weekly availability checks passed.");

public class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}
public class CrossingClock(DateTimeOffset before, DateTimeOffset after) : TimeProvider
{
    private int calls;
    public override DateTimeOffset GetUtcNow() => (++calls == 1 ? before : after).ToUniversalTime();
}
public class Stub : DispatchProxy
{
    public Func<string, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
    public static T Make<T>(Func<string, object?[]?, object?> handler) where T : class
    {
        var instance = Create<T, Stub>(); ((Stub)(object)instance).Handler = handler; return instance;
    }
}
