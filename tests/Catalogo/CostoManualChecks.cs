using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;

static class CostoManualChecks
{
    public static async Task RunAsync(PanaderiaContext db, Action<bool, string> check)
    {
        db.ChangeTracker.Clear();
        check(!db.Database.HasPendingModelChanges(), "Modelo y migraciones coinciden");
        check(await db.Productos.AllAsync(p => !p.OcultoEnTienda && p.CostoManual == null),
            "La migración conserva visibilidad y deja vacío el costo de productos existentes");
        var productos = new ProductoService(db);
        var pedidos = new PedidoService(db);
        var hummus = new Producto { Nombre = "Hummus", IdCategoria = 104, PorEncargo = true,
            CostoManual = 1234.5678m, PrecioFinal = 3000, FechaCreacion = DateTime.UtcNow, OcultoEnTienda = false };
        await productos.CreateAsync(hummus);
        db.ChangeTracker.Clear();
        var guardado = (await productos.GetByIdAsync(hummus.Id))!;
        check(guardado.OcultoEnTienda && guardado.CostoManual == 1234.5678m,
            "Crear oculta el producto y guarda cuatro decimales de costo");
        await productos.ToggleOcultoEnTiendaAsync(hummus.Id);
        db.ChangeTracker.Clear();
        guardado.CostoManual = 1500.25m;
        await productos.UpdateAsync(guardado);
        db.ChangeTracker.Clear();
        check((await productos.GetByIdAsync(hummus.Id)) is { OcultoEnTienda: false, CostoManual: 1500.25m },
            "Editar actualiza el costo y conserva la publicación del producto");
        var copia = (await productos.DuplicarAsync(hummus.Id))!;
        check(copia.OcultoEnTienda && copia.CostoManual == 1500.25m, "Duplicar conserva el costo y oculta la copia");
        var vacio = new Producto { Nombre = "Sin costo", IdCategoria = 104, FechaCreacion = DateTime.UtcNow };
        await productos.CreateAsync(vacio);
        check(vacio.CostoManual == null && vacio.OcultoEnTienda, "Crear sin costo es válido y queda oculto");
        var negativo = new Producto { CostoManual = -1 };
        check(!Validator.TryValidateProperty(negativo.CostoManual, new ValidationContext(negativo) { MemberName = nameof(Producto.CostoManual) }, []),
            "Validación rechaza costos negativos");
        await db.Productos.Where(p => p.Id == 101).ExecuteUpdateAsync(s => s.SetProperty(p => p.CostoManual, 9999m));
        var costos = await pedidos.GetPreciosCostoAsync([hummus.Id, vacio.Id, 101]);
        check(costos[hummus.Id] == 1500.25m && costos[101] == 150m && !costos.ContainsKey(vacio.Id),
            "Usa costo manual sin receta, prioriza receta y distingue costo ausente");
        var cliente = new Cliente { Nombre = "Cliente a costo", PrecioDeCosto = true, FechaCreacion = DateTime.UtcNow };
        db.Clientes.Add(cliente); await db.SaveChangesAsync();
        var fecha = CalendarioCaja.InicioUtc(CalendarioCaja.Lunes(CalendarioCaja.Hoy)).AddDays(1);
        var pedido = new Pedido { IdCliente = cliente.Id, FechaCreacion = fecha, FechaEntrega = fecha,
            Detalles = [new() { IdProducto = hummus.Id, Cantidad = 2, PrecioUnitario = 3000, IdEmpaque = 101 }] };
        await pedidos.CreateAsync(pedido);
        check(pedido.Detalles.Single().PrecioUnitario == 1500.25m, "Venta a precio de costo admite productos sin receta");
        pedido.MontoCobrado = pedido.MontoTotal;
        await db.SaveChangesAsync();
        await pedidos.MarcarEntregadoAsync(pedido.Id);
        db.ChangeTracker.Clear();
        var detalle = await db.DetallesPedido.SingleAsync(d => d.IdPedido == pedido.Id);
        check(detalle.CostoIngredientes == 1500.25m && detalle.FechaCosto.HasValue, "Entrega congela el costo manual");
        await db.Productos.Where(p => p.Id == hummus.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.CostoManual, 2000m));
        db.ChangeTracker.Clear();
        var caja = await new CierreCajaService(db).ResumenAsync(CalendarioCaja.Lunes(CalendarioCaja.Hoy));
        var costoCaja = caja.Cierre.Costos.Single(c => c.IdProducto == hummus.Id);
        check(costoCaja.Ingredientes == 3000.5m && costoCaja.Empaque == 40m && !costoCaja.CostoPendiente,
            "Caja conserva costo al entregar y suma el empaque por separado");
        await db.DetallesPedido.Where(d => d.IdPedido == pedido.Id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.CostoIngredientes, (decimal?)null).SetProperty(d => d.FechaCosto, (DateTime?)null));
        caja = await new CierreCajaService(db).ResumenAsync(CalendarioCaja.Lunes(CalendarioCaja.Hoy));
        check(caja.Cierre.Costos.Single(c => c.IdProducto == hummus.Id) is { Ingredientes: 4000m, Reconstruido: true, CostoPendiente: false },
            "Caja reconstruye costos antiguos sin receta desde el costo manual");
        var semanal = await pedidos.GetResumenCierreSemanalAsync(fecha);
        check(semanal.DetallesCosto.Single(c => c.NombreProducto == "Hummus").CostoUnitario == 2000m,
            "Resumen semanal contempla costos manuales");
        db.ChangeTracker.Clear();
        guardado.CostoManual = null;
        await productos.UpdateAsync(guardado);
        db.ChangeTracker.Clear();
        check((await productos.GetByIdAsync(hummus.Id))!.CostoManual == null,
            "Vaciar el campo elimina el costo manual");
    }
}
