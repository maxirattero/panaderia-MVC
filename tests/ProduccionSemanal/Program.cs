using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;
using Panaderia.MVC.Controllers;

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
bool MismosTotales(TotalesPedidosSemana a, TotalesPedidosSemana b) =>
    (a with { Panes = b.Panes }) == b && a.Panes.SequenceEqual(b.Panes);
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
var bread = new Producto { Nombre = "Pan", Masa = (Masa)0, Categoria = new() { Nombre = "Panes" }, Formato = new() { Descripcion = " molde " } };
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
var withoutDate = Order(monday, 100);
withoutDate.FechaEntrega = null;
db.Add(recipe);
db.AddRange(current);
db.AddRange(outside);
db.Add(withoutDate);
db.AddRange(Order(monday, 100, EstadoPedido.Entregado), Order(monday, 100, EstadoPedido.EnProduccion), Order(monday, 100, cancelled: true));
await db.SaveChangesAsync();

async Task<List<Pedido>> Printed(bool details)
{
    // Otro contexto evita que el tracking del fixture oculte un Include faltante.
    await using var readDb = new PanaderiaContext(new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options);
    var controller = new PedidoController(new PedidoService(readDb, clock), null!, null!, null!, null!);
    var result = (ViewResult)await controller.Imprimir(details);
    Check((bool)controller.ViewBag.ConDetalles == details, "Impresión conserva la opción de detalle de productos");
    return ((IEnumerable<Pedido>)result.Model!).ToList();
}
foreach (var details in new[] { false, true })
{
    var printed = await Printed(details);
    Check(printed.Select(p => p.Id).SequenceEqual(current.Select(p => p.Id)),
        "Impresión solo incluye pendientes con entrega esta semana; excluye futuros, anteriores, sin fecha, entregados y anulados");
    Check(printed.All(p => p.Detalles.Single().Empaque?.Nombre == bag.Nombre), "Impresión carga la bolsa asignada a cada producto");
}
Check((await service.GetByEstadoAsync(EstadoPedido.Pendiente)).Any(p => p.Id == outside[1].Id),
    "El listado del admin sigue mostrando pedidos futuros");
await service.AgregarProduccionStockAsync(bread.Id, 1);
var totales = await service.GetTotalesSemanaAsync();
Check(totales.InicioSemana == DateOnly.FromDateTime(monday) && totales.FinSemana == DateOnly.FromDateTime(monday.AddDays(6)), "Totales muestran el rango de lunes a domingo");
Check(totales.CantidadPedidos == 4 && totales.Moldes == 111 && totales.Campos == 0 && totales.Bolsas == 0 && totales.BolsasPapel == 111,
    "Totales incluyen cantidades completas de pendientes y en producción; excluyen entregados, anulados, otras semanas, sin fecha y stock adicional");
Check(totales.Panes.Single() is { NombreProducto: "Pan", Formato: "molde", CantidadTotal: 111 },
    "Desglose agrupa el mismo pan de varios pedidos pendientes y en producción, sin descontar unidades confirmadas");
await using (var readDb = new PanaderiaContext(new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options))
{
    var controller = new PedidoController(new PedidoService(readDb, clock), null!, null!, null!, null!);
    await controller.Index();
    Check(controller.ViewBag.TotalesSemana is TotalesPedidosSemana model && MismosTotales(model, totales),
        "Pedidos carga los totales para la ventana flotante desde un contexto nuevo");
}
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
Check(MismosTotales(await service.GetTotalesSemanaAsync(), totales), "Totales conservan la semana hasta la medianoche argentina");
Check((await Printed(false)).Select(p => p.Id).SequenceEqual(current.Select(p => p.Id)), "Impresión conserva la semana durante el domingo argentino");
Check((await service.GetResumenProduccionAsync()).PorProducto.Single().CantidadTotal == 9, "Domingo argentino sigue en la semana actual aunque sea lunes UTC");
clock.Now = DateTimeOffset.Parse("2026-10-05T03:00:00Z");
var totalesSiguienteSemana = await service.GetTotalesSemanaAsync();
Check(totalesSiguienteSemana.CantidadPedidos == 3 && totalesSiguienteSemana.Moldes == 50 && totalesSiguienteSemana.BolsasPapel == 57,
    "Totales cambian al lunes argentino y cuentan empaques de productos sin formato");
Check((await Printed(true)).Select(p => p.Id).ToHashSet().SetEquals(outside.Skip(1).Select(p => p.Id)), "Impresión cambia de semana al comenzar el lunes argentino");
summary = await service.GetResumenProduccionAsync();
Check(summary.PorProducto.Sum(p => p.CantidadTotal) == 57 && summary.PorProducto.Count == 2, "Al comenzar el lunes argentino aparecen los pedidos de la nueva semana");
clock.Now = DateTimeOffset.Parse("2026-10-02T12:00:00-03:00");
var confirmacion = new List<Panaderia.Models.DTOs.ItemProduccionSeleccionable> { new() { IdProducto = bread.Id, IdReceta = recipe.Id, CantidadAProducir = 9 } };
await service.PrepararConfirmacionAsync(confirmacion);
var warnings = await service.ConfirmarProduccionAsync(confirmacion);
Check(warnings.Count == 0, "Se puede confirmar la semana sin producir pedidos futuros");
Check(MismosTotales(await service.GetTotalesSemanaAsync(), totales), "Confirmar producción conserva moldes, bolsas y desglose de panes completos en Pedidos");
Check(current.All(p => p.Estado == EstadoPedido.EnProduccion && p.Detalles.All(d => d.CantidadProducida == d.Cantidad)), "Confirmación completa solo los pedidos de esta semana");
Check(outside.All(p => p.Estado == EstadoPedido.Pendiente && p.Detalles.All(d => d.CantidadProducida == 0)), "Entregas pasadas y futuras permanecen pendientes e intactas");
Check(water.StockActual == 99550 && flour.StockActual == 99550, "Descuento de insumos corresponde a las nueve unidades confirmadas");
Check((await service.GetResumenProduccionAsync()).PorProducto.Count == 0
    && (await service.GetProduccionCombinadaResumenAsync()).Single().CantidadTotal == 1, "Al confirmar desaparecen pedidos actuales y se conserva el stock adicional");
await ConfirmacionChecks.RunAsync(db, service, clock, monday, bread, recipe, customer, water, Check);
clock.Now = DateTimeOffset.Parse("2026-11-04T12:00:00-03:00");
var vacio = await service.GetTotalesSemanaAsync();
Check(vacio.CantidadPedidos == 0 && vacio.Moldes == 0 && vacio.Campos == 0 && vacio.Bolsas == 0 && vacio.BolsasPapel == 0,
    "Semana sin pedidos devuelve los cuatro totales en cero");
Check(vacio.Panes.Count == 0, "Semana vacía no muestra panes");
var campo = new Producto { Nombre = "Campo", Categoria = bread.Categoria, Formato = new() { Descripcion = "CAMPO" } };
var bolsaSellada = new Insumo { Nombre = "Bolsa sellada", TipoInsumo = TipoInsumo.Empaque };
var pedidoEmpaques = new Pedido { Cliente = customer, FechaEntrega = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
    Detalles = [new() { Producto = campo, Cantidad = 3, Empaque = bolsaSellada, CantidadProducida = 1 },
        new() { Producto = campo, Cantidad = 2, Empaque = bag }, new() { Producto = campo, Cantidad = 4 },
        new() { Producto = futureOnlyProduct, Cantidad = 6, Empaque = bolsaSellada, ReservaStock = true }] };
db.Add(pedidoEmpaques);
await db.SaveChangesAsync();
var empaques = await service.GetTotalesSemanaAsync();
Check(empaques.CantidadPedidos == 1 && empaques.Campos == 9 && empaques.Moldes == 0 && empaques.Bolsas == 9 && empaques.BolsasPapel == 2,
    "Distingue bolsas y papel, no cuenta empaques ausentes y conserva cantidades producidas o reservadas");
Check(empaques.Panes.Single(p => p.IdProducto == campo.Id).CantidadTotal == 9
    && empaques.Panes.Single(p => p.IdProducto == futureOnlyProduct.Id).Formato == "Sin formato",
    "Agrupa renglones del mismo pan con empaques distintos y muestra panes sin formato");
var otroFormato = new Producto { Nombre = "Campo", Categoria = new() { Nombre = " Pan " },
    Formato = new() { Descripcion = "Molde" }, Tamano = new() { Descripcion = "Grande" } };
var sinNombre = new Producto { Categoria = bread.Categoria, Masa = (Masa)0, Formato = campo.Formato };
pedidoEmpaques.Detalles.Add(new() { Producto = otroFormato, Cantidad = 2 });
pedidoEmpaques.Detalles.Add(new() { Producto = sinNombre, Cantidad = 1 });
pedidoEmpaques.Detalles.Add(new() { Producto = new() { Nombre = "Prepizza", Categoria = new() { Nombre = "Prepizzas" } }, Cantidad = 5 });
await db.SaveChangesAsync();
empaques = await service.GetTotalesSemanaAsync();
Check(empaques.Panes.Count == 4 && empaques.Panes.Sum(p => p.CantidadTotal) == 18
    && empaques.Panes.Single(p => p.IdProducto == otroFormato.Id) is { Formato: "Molde", Tamano: "Grande", CantidadTotal: 2 }
    && empaques.Panes.Single(p => p.IdProducto == sinNombre.Id).NombreProducto == sinNombre.NombreVisible,
    "Desglose distingue formatos de panes con el mismo nombre, incluye tamaños y nombres generados y excluye otros productos");
pedidoEmpaques.Estado = EstadoPedido.Entregado;
await db.SaveChangesAsync();
Check(MismosTotales(await service.GetTotalesSemanaAsync(), vacio), "Entregar un pedido retira sus cantidades de los totales y del desglose");
pedidoEmpaques.Estado = EstadoPedido.EnProduccion;
await db.SaveChangesAsync();
Check(MismosTotales(await service.GetTotalesSemanaAsync(), empaques), "Un pedido en producción vuelve a sumar sus cantidades completas");
pedidoEmpaques.Anulado = true;
await db.SaveChangesAsync();
Check(MismosTotales(await service.GetTotalesSemanaAsync(), vacio), "Anular un pedido retira sus cantidades del resumen y del desglose");
await ConfirmacionConcurrente.RunAsync(port, Check, args.Contains("--preview"));
Console.WriteLine($"{checks} verificaciones de producción semanal correctas.");

sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}
