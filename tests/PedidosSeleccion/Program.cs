using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.MVC.Models;
using Panaderia.Services.Implementations;

// PostgreSQL local de pruebas; crea una base nueva y nunca usa configuración de producción.
var database = "pedidos_checks_" + Guid.NewGuid().ToString("N");
var port = int.Parse(Environment.GetEnvironmentVariable("PEDIDOS_TEST_PORT") ?? "55439");
var connection = $"Host=127.0.0.1;Port={port};Username=postgres;Database={database}";
// Identity configura convenciones adicionales en el host MVC; este ejecutable
// usa las migraciones existentes y no genera un modelo nuevo.
var options = new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection)
    .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)).Options;
PanaderiaContext Db() => new(options);
var checks = 0;
void Check(bool result, string description)
{
    if (!result) throw new Exception(description);
    Console.WriteLine("PASS: " + description);
    checks++;
}
async Task Reject(Func<Task> action, string description)
{
    try { await action(); }
    catch (InvalidOperationException) { Check(true, description); return; }
    throw new Exception("Expected rejection: " + description);
}
async Task Batch(int[] ids, bool cobrar, bool entregar)
{
    await using var db = Db();
    await new PedidoService(db).ActualizarSeleccionAsync(ids, cobrar, entregar);
}
await using (var db = Db())
{
    await db.Database.MigrateAsync();
    db.Pedidos.AddRange(
        new Pedido { Id = 1, Cliente = new Cliente { Nombre = "Ángela Prueba" }, MontoTotal = 100, MontoCobrado = 25 },
        new Pedido { Id = 2, Cliente = new Cliente { Nombre = "Bruno Prueba" }, MontoTotal = 200, MontoCobrado = 200 },
        new Pedido { Id = 3, Cliente = new Cliente { Nombre = "Carla Prueba" }, MontoTotal = 300 },
        new Pedido { Id = 4, Cliente = new Cliente { Nombre = "Anulado Prueba" }, MontoTotal = 400, Anulado = true });
    await db.SaveChangesAsync();
}
await Reject(() => Batch([], true, false), "Empty selection rejected");
await Reject(() => Batch([1], false, false), "Missing action rejected");
await Reject(() => Batch([0], true, false), "Invalid id rejected");
await Reject(() => Batch([2, 1], false, true), "Unpaid delivery rejects the entire selection");
await Reject(() => Batch([1, 4], true, true), "Cancelled order rejects the entire selection");
await Reject(() => Batch([1, 999], true, true), "Missing order rejects the entire selection");
await using (var db = Db())
{
    Check(await db.ReportesCaja.CountAsync() == 0 && await db.Pedidos.AllAsync(p => p.Estado != EstadoPedido.Entregado),
        "Rejected selections produce no payments or deliveries");
}
await Batch([1, 1, 2], true, false);
await using (var db = Db())
{
    var payment = await db.ReportesCaja.SingleAsync();
    Check(payment.Monto == 75 && payment.IdPedido == 1 && payment.Tipo == TipoMovimiento.Ingreso && payment.Categoria == CategoriaMovimiento.Venta,
        "Partial balance produces exactly one sale income; duplicate ids and paid orders ignored");
    Check((await db.Pedidos.FindAsync(1))!.MontoCobrado == 100 && await db.Pedidos.AllAsync(p => p.Estado != EstadoPedido.Entregado),
        "Charge only preserves delivery status");
}
await Batch([1, 2], false, true);
await Batch([3], true, true);
await Batch([1, 2, 3], true, true);
await using (var db = Db())
{
    Check(await db.Pedidos.AllAsync(p => p.Estado == EstadoPedido.Entregado && p.MontoCobrado == p.MontoTotal),
        "Deliver paid and charge-and-deliver both complete correctly");
    Check(await db.ReportesCaja.CountAsync() == 2 && await db.ReportesCaja.SumAsync(r => r.Monto) == 375,
        "Repeated submission creates no duplicate cash entries");
    db.Pedidos.Add(new Pedido { Id = 5, Cliente = new Cliente { Nombre = "Concurrente Prueba" }, MontoTotal = 500 });
    await db.SaveChangesAsync();
}
await Task.WhenAll(Batch([5], true, true), Batch([5], true, true));
await using (var db = Db())
{
    Check(await db.ReportesCaja.CountAsync(r => r.IdPedido == 5) == 1 && (await db.Pedidos.FindAsync(5))!.MontoCobrado == 500,
        "Concurrent batch submissions charge once");
    await Reject(() => new PedidoService(db).RegistrarCobroAsync(5, 1), "Individual charge cannot overcharge a batch payment");
}
foreach (var action in new[] { "cobrar", "entregar", "cobrar-entregar", "inventado", "" })
{
    var vm = new SeleccionPedidosViewModel { Ids = [1], Accion = action };
    Check(Validator.TryValidateObject(vm, new ValidationContext(vm), [], true) == (action != "inventado" && action != ""),
        "Action validation: " + action);
}
// Fixtures disponibles para revisar la interfaz sin usar pedidos reales.
await using (var db = Db())
{
    foreach (var pedido in await db.Pedidos.Where(p => p.Id <= 3).ToListAsync())
    {
        pedido.Estado = EstadoPedido.Pendiente;
        pedido.FechaEntrega = DateTime.UtcNow.Date;
    }
    await db.SaveChangesAsync();
}
Directory.CreateDirectory(".artifacts/pedidos-checks");
await File.WriteAllTextAsync(".artifacts/pedidos-checks/connection.txt", connection);
Console.WriteLine($"{checks} checks passed. Local test database: {database}");
