using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;

static class ConfirmacionConcurrente
{
    public static async Task RunAsync(int port, Action<bool, string> check, bool preview)
    {
        // Base desechable local: dos conexiones reales compiten por la misma confirmación.
        var database = "produccion_checks_" + Guid.NewGuid().ToString("N");
        var connection = $"Host=127.0.0.1;Port={port};Username=postgres;Database={database}";
        var options = new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options;
        await using var db = new PanaderiaContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var water = new Insumo { Nombre = "Agua", StockActual = 10000, UnidadBase = UnidadMedida.Mililitros };
            var bread = new Producto { Nombre = "Pan de campo", Categoria = new() { Nombre = "Panes" }, Masa = (Masa)0 };
            var recipe = new Receta { Producto = bread, TamanioLote = 1, PesoUnitario = 100, Detalles = [new() { Insumo = water, PorcentajePanadero = 100 }] };
            var date = DateTime.SpecifyKind(CalendarioCaja.Hoy.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var order = new Pedido { Cliente = new() { Nombre = "Cliente de prueba" }, FechaEntrega = date,
                Detalles = [new() { Producto = bread, Cantidad = 4 }] };
            db.AddRange(recipe, order);
            await db.SaveChangesAsync();
            var service = new PedidoService(db);
            await service.AgregarProduccionStockAsync(bread.Id, 2);
            var buffer = await db.ProduccionStock.SingleAsync();
            var items = new List<ItemProduccionSeleccionable> {
                new() { IdProducto = bread.Id, IdReceta = recipe.Id, CantidadAProducir = 2 },
                new() { IdProducto = bread.Id, IdReceta = recipe.Id, CantidadAProducir = 1, EsStock = true, IdProduccionStock = buffer.Id }
            };
            await service.PrepararConfirmacionAsync(items);
            async Task<List<string>> Confirm()
            {
                await using var otherDb = new PanaderiaContext(options);
                return await new PedidoService(otherDb).ConfirmarProduccionAsync(items);
            }
            var results = await Task.WhenAll(Confirm(), Confirm());
            check(results.Count(r => r.Count == 0) == 1 && results.Count(r => r.Count > 0) == 1,
                "Dos confirmaciones simultáneas: solo una modifica producción");
            db.ChangeTracker.Clear();
            check((await db.Insumos.SingleAsync()).StockActual == 9700
                && (await db.DetallesPedido.SingleAsync()).CantidadProducida == 2
                && (await db.Productos.SingleAsync()).Stock == 1
                && (await db.ProduccionStock.SingleAsync()).Cantidad == 1,
                "Concurrencia conserva cantidades parciales y descuenta insumos una sola vez");
            check((await new DashboardService(db).GetResumenDashboardAsync()).Produccion.PorProducto.Single().CantidadTotal == 2,
                "El resumen de inicio también muestra solo las unidades pendientes");
            if (preview)
            {
                var other = new Producto { Nombre = "Prepizza", Categoria = new() { Nombre = "Prepizzas" }, Masa = (Masa)0 };
                var previewWater = await db.Insumos.SingleAsync();
                db.Add(new Receta { Producto = other, TamanioLote = 1, PesoUnitario = 100,
                    Detalles = [new() { Insumo = previewWater, PorcentajePanadero = 100 }] });
                db.Add(new Pedido { Cliente = await db.Clientes.SingleAsync(), FechaEntrega = date,
                    Detalles = [new() { Producto = other, Cantidad = 3 }] });
                await db.SaveChangesAsync();
                await Preview.RunAsync(connection);
            }
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
