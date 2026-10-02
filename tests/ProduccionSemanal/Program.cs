using Microsoft.EntityFrameworkCore;
using Npgsql;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;

// Solo PostgreSQL local y tablas temporales: no lee configuración ni datos de producción.
var port = int.Parse(Environment.GetEnvironmentVariable("PEDIDOS_TEST_PORT") ?? "55439");
await using var connection = new NpgsqlConnection($"Host=127.0.0.1;Port={port};Username=postgres;Database=postgres");
await connection.OpenAsync();
await new NpgsqlCommand("SET search_path TO pg_temp", connection).ExecuteNonQueryAsync();
await using var db = new PanaderiaContext(new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options);
var schema = db.Database.GenerateCreateScript().Replace("CREATE TABLE ", "CREATE TEMP TABLE ");
if (schema.Contains("public.") || schema.Contains("CREATE SCHEMA")) throw new Exception("Esquema no aislado");
await new NpgsqlCommand(schema, connection).ExecuteNonQueryAsync();
var checks = 0;
void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
    checks++;
}
var monday = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
var clock = new TestClock(DateTimeOffset.Parse("2026-10-02T12:00:00-03:00"));
var service = new PedidoService(db, clock);
var water = new Insumo { Nombre = "Agua", StockActual = 100000, CantidadRendimiento = 1 };
var flour = new Insumo { Nombre = "Harina", StockActual = 100000, CantidadRendimiento = 1 };
var bag = new Insumo { Nombre = "Bolsa", TipoInsumo = TipoInsumo.Empaque, EsBolsaPapel = true };
var starter = new SubReceta { Nombre = "Base", Detalles = [new() { Insumo = flour, PorcentajePanadero = 100 }] };
var bread = new Producto { Nombre = "Pan", Masa = (Masa)0, Categoria = new() { Nombre = "Panes" } };
var recipe = new Receta { Producto = bread, TamanioLote = 1, PesoUnitario = 100,
    Detalles = [new() { Insumo = water, PorcentajePanadero = 50 }, new() { SubReceta = starter, PorcentajePanadero = 50 }] };
var futureOnlyProduct = new Producto { Nombre = "Solo otra semana", Categoria = bread.Categoria };
var customer = new Cliente { Nombre = "Prueba" };
Pedido Order(DateTime date, int quantity, EstadoPedido state = EstadoPedido.Pendiente, bool cancelled = false, int produced = 0, Producto? product = null) => new()
{
    Cliente = customer, FechaEntrega = date, Estado = state, Anulado = cancelled,
    Detalles = [new() { Producto = product ?? bread, Cantidad = quantity, CantidadProducida = produced, Empaque = bag }]
};
var current = new[] { Order(monday, 2), Order(monday.AddDays(5), 5, produced: 2), Order(monday.AddDays(7).AddTicks(-10), 4) };
var outside = new[] { Order(monday.AddTicks(-10), 11), Order(monday.AddDays(7), 20), Order(monday.AddDays(12), 30),
    Order(monday.AddDays(7), 7, product: futureOnlyProduct) };
db.Add(recipe);
db.AddRange(current);
db.AddRange(outside);
db.AddRange(Order(monday, 100, EstadoPedido.Entregado), Order(monday, 100, EstadoPedido.EnProduccion), Order(monday, 100, cancelled: true));
await db.SaveChangesAsync();
await service.AgregarProduccionStockAsync(bread.Id, 1);
var summary = await service.GetResumenProduccionAsync();
Check(summary.PorProducto.Single().CantidadTotal == 9, "Solo entregas de lunes a domingo, descontando unidades ya producidas");
Check(summary.PorBolsa.Single().CantidadTotal == 9, "Bolsas excluyen entregas de otras semanas");
Check(summary.TotalAgua == 500 && summary.PorSubReceta.Single().TotalGramos == 500, "Agua y subrecetas incluyen semana actual y stock adicional");
Check((await service.GetProduccionCombinadaResumenAsync()).Single().CantidadTotal == 10, "Impresión combina semana actual y stock");
var ingredients = (await service.GetIngredientesProduccionAsync()).Single();
Check(ingredients.CantidadUnidades == 10 && ingredients.PesoMasaTotal == 1000, "Planificador usa las mismas cantidades semanales");
Check((await service.GetResumenProduccionAsync([bread.Id])).PorProducto.Count == 0
    && (await service.GetIngredientesProduccionAsync([bread.Id])).Count == 0, "Se conserva la exclusión manual de productos");
clock.Now = DateTimeOffset.Parse("2026-10-05T02:59:59Z");
Check((await service.GetResumenProduccionAsync()).PorProducto.Single().CantidadTotal == 9, "Domingo argentino sigue en la semana actual aunque sea lunes UTC");
clock.Now = DateTimeOffset.Parse("2026-10-05T03:00:00Z");
summary = await service.GetResumenProduccionAsync();
Check(summary.PorProducto.Sum(p => p.CantidadTotal) == 57 && summary.PorProducto.Count == 2, "Al comenzar el lunes argentino aparecen los pedidos de la nueva semana");
clock.Now = DateTimeOffset.Parse("2026-10-02T12:00:00-03:00");
var warnings = await service.ConfirmarProduccionAsync([new() { IdProducto = bread.Id, IdReceta = recipe.Id, CantidadAProducir = 9 }]);
Check(warnings.Count == 0, "Se puede confirmar la semana sin producir pedidos futuros");
Check(current.All(p => p.Estado == EstadoPedido.EnProduccion && p.Detalles.All(d => d.CantidadProducida == d.Cantidad)), "Confirmación completa solo los pedidos de esta semana");
Check(outside.All(p => p.Estado == EstadoPedido.Pendiente && p.Detalles.All(d => d.CantidadProducida == 0)), "Entregas pasadas y futuras permanecen pendientes e intactas");
Check(water.StockActual == 99550 && flour.StockActual == 99550, "Descuento de insumos corresponde a las nueve unidades confirmadas");
Check((await service.GetResumenProduccionAsync()).PorProducto.Count == 0
    && (await service.GetProduccionCombinadaResumenAsync()).Single().CantidadTotal == 1, "Al confirmar desaparecen pedidos actuales y se conserva el stock adicional");
Console.WriteLine($"{checks} verificaciones de producción semanal correctas.");

sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}
