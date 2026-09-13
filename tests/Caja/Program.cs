using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;

// Base local nueva por ejecución. Nunca lee secretos ni configuración de producción.
var database = "caja_checks_" + Guid.NewGuid().ToString("N");
var port = int.Parse(Environment.GetEnvironmentVariable("CAJA_TEST_PORT") ?? "55439");
var connection = $"Host=127.0.0.1;Port={port};Username=postgres;Database={database};Timeout=5";
var options = new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options;
PanaderiaContext Db() => new(options);
var checks = 0;
void Check(bool ok, string what) { if (!ok) throw new Exception(what); checks++; Console.WriteLine("PASS: " + what); }
async Task Reject(Func<Task> f, string what) { try { await f(); } catch (InvalidOperationException) { Check(true, what); return; } throw new Exception("No se rechazó: " + what); }
var semana = CalendarioCaja.Lunes(CalendarioCaja.Hoy).AddDays(-7);
var inicio = CalendarioCaja.InicioUtc(semana);
int pedidoId, insumoCompraId, unidadId, proveedorId;
await using (var db = Db())
{
    await db.GetService<IMigrator>().MigrateAsync("20260911041228_AgregarClientePrecioDeCosto");
    await db.Database.ExecuteSqlRawAsync("""
        INSERT INTO "ReportesCaja" ("Fecha", "Tipo", "Categoria", "Monto", "FechaInicioPeriodo", "FechaFinPeriodo", "TotalVendidoInformativo")
        VALUES ('2025-01-13T12:00:00Z',1,3,125,'2025-01-06T00:00:00Z','2025-01-13T00:00:00Z',500),
               ('2025-01-13T12:00:01Z',1,3,25,'2025-01-06T00:00:00Z','2025-01-13T00:00:00Z',500)
        """);
    await db.Database.MigrateAsync();
    Check(!db.Database.HasPendingModelChanges(), "El modelo y la migración coinciden");
    var historicos = await db.CierresCaja.ToListAsync();
    Check(historicos.Count == 2 && historicos.All(c => c.EsHistorico && c.PorcentajeReserva == null) && historicos.Sum(c => c.RetiroHistorico) == 150,
        "Migra cierres históricos incluso duplicados, sin inventar porcentaje/reparto");
    Check(await db.ReportesCaja.CountAsync() == 2 && await db.ReportesCaja.SumAsync(r=>r.Monto) == 150, "La migración no repite retiros ni cambia importes antiguos");
    var proveedor = new Proveedor { Nombre = "Proveedor pruebas" };
    var harina = new Insumo { Nombre = "Harina", PrecioCompra = 1000m, CantidadRendimiento = 1, FechaCreacion = inicio };
    var producto = new Producto { Nombre = "Pan prueba", Categoria = new() { Nombre = "Panes" }, PorEncargo = true };
    var receta = new Receta { Producto = producto, TamanioLote = 1, PesoUnitario = 100, FechaCreacion = inicio, Detalles = [new() { Insumo = harina, PorcentajePanadero = 100 }] };
    var comprado = new Insumo { Nombre = "Compra independiente", CantidadRendimiento = 1, FechaCreacion = inicio, TipoInsumo = TipoInsumo.Consumible };
    var unidad = new UnidadCompra { Insumo = comprado, Nombre = "Unidad", FactorConversion = 1 };
    var pedido = new Pedido { Cliente = new() { Nombre = "Cliente caja" }, MontoTotal = 400000, FechaCreacion = inicio, FechaEntrega = inicio,
        Detalles = [new() { Producto = producto, Cantidad = 1, PrecioUnitario = 400000, CostoEmpaque = 10000 }] };
    db.AddRange(proveedor, receta, unidad, pedido); await db.SaveChangesAsync();
    pedidoId = pedido.Id; insumoCompraId = comprado.Id; unidadId = unidad.Id; proveedorId = proveedor.Id;
}
await using (var db = Db())
{
    await new CierreCajaService(db).ConfigurarAsync(30, inicio, 0, 0, 150000, "prueba");
    await new PedidoService(db).RegistrarCobroAsync(pedidoId, 120000, CuentaCaja.Efectivo, inicio.AddHours(12));
    await new PedidoService(db).RegistrarCobroAsync(pedidoId, 280000, CuentaCaja.Banco, inicio.AddHours(13));
    var pedido = await db.Pedidos.FindAsync(pedidoId);
    pedido!.Estado = EstadoPedido.Entregado;
    await db.SaveChangesAsync();
    Check(pedido.MontoCobrado == 400000 && await db.ReportesCaja.CountAsync(r=>r.IdPedido == pedidoId) == 2, "Cobro combinado mantiene dos cuentas y un saldo de pedido consistente");
    var compraKey = Guid.NewGuid();
    CompraProveedor Compra() => new() { IdProveedor = proveedorId, Fecha = inicio.AddDays(2), CuentaPago = CuentaCaja.ReservaMercadoPago, ClaveOperacion = compraKey,
        Detalles = [new() { IdInsumo = insumoCompraId, IdUnidadCompra = unidadId, Cantidad = 1, PrecioUnitario = 80000 }] };
    await new CompraService(db).CreateAsync(Compra());
    await new CompraService(db).CreateAsync(Compra());
    Check(await db.ComprasProveedor.CountAsync() == 1 && (await db.Insumos.FindAsync(insumoCompraId))!.StockActual == 1, "Reintentar compra no duplica stock ni pago");
    Check((await db.ReportesCaja.SingleAsync(r=>r.Categoria == CategoriaMovimiento.Proveedor)).IdCompra.HasValue, "Compra y pago quedan vinculados");
}
int cierreId;
await using (var db = Db())
{
    var service = new CierreCajaService(db);
    var r = await service.ResumenAsync(semana);
    Check(r.Cierre.BaseReparto == 400000 && r.Cierre.CostoInsumos == 110000, "Compra de reserva y costo informativo no reducen la base de 400000");
    Check(r.Cierre.Reserva == 120000 && r.Cierre.Ani == 140000 && r.Cierre.Maxi == 140000, "Reserva 30% y reparto por mitades exactos");
    Check(r.SaldosActuales.Single(s=>s.Cuenta == CuentaCaja.ReservaMercadoPago).Saldo == 70000, "La compra sí reduce el fondo MP a 70000");
    cierreId = await service.CerrarAsync(semana, 30, "Prueba", "prueba", false);
    Check(await db.ReportesCaja.CountAsync(r=>r.IdCierreDestino == cierreId) == 0, "Guardar cierre no inventa transferencias ni retiros realizados");
}
await using (var db = Db())
{
    await Reject(()=>new CierreCajaService(db).CerrarAsync(semana, 30, null, null, false), "Duplicado de cierre rechazado");
    await Reject(()=>new PedidoService(db).RegistrarCobroAsync(pedidoId, 1, CuentaCaja.Banco, inicio.AddHours(15)), "No se alteran cobros de un período cerrado");
}
await using (var db = Db())
{
    var harina = await db.Insumos.SingleAsync(i=>i.Nombre == "Harina"); harina.PrecioCompra *= 2; await db.SaveChangesAsync();
    var service = new CierreCajaService(db);
    await service.ConfigurarAsync(40, null, 0, 0, 0, "prueba");
    var cerrado = await service.DetalleAsync(cierreId);
    Check(cerrado.Cierre.CostoInsumos == 110000 && cerrado.Cierre.PorcentajeReserva == 30 && cerrado.Cierre.Reserva == 120000,
        "Cambiar precio y porcentaje no modifica el cierre guardado");
}
var clave = Guid.NewGuid();
async Task Pagar(DestinoCierre destino, CuentaCaja cuenta, decimal monto, Guid? key = null) {
    await using var db = Db(); await new CierreCajaService(db).RegistrarPagoAsync(cierreId, destino, cuenta, monto, DateTime.UtcNow, key ?? Guid.NewGuid());
}
await Task.WhenAll(Pagar(DestinoCierre.Reserva, CuentaCaja.Banco, 120000, clave), Pagar(DestinoCierre.Reserva, CuentaCaja.Banco, 120000, clave));
await using (var db = Db()) {
    Check(await db.ReportesCaja.CountAsync(r=>r.IdTransferencia == clave) == 2, "Transferencia concurrente crea un solo par entrada/salida");
    var r = await new CierreCajaService(db).DetalleAsync(cierreId);
    Check(r.Pendiente(DestinoCierre.Reserva) == 0 && r.SaldosActuales.Single(s=>s.Cuenta == CuentaCaja.ReservaMercadoPago).Saldo == 190000, "Reserva conciliada: 150000 - 80000 + 120000 = 190000");
}
await Pagar(DestinoCierre.Ani, CuentaCaja.Efectivo, 60000);
await Pagar(DestinoCierre.Ani, CuentaCaja.Banco, 80000);
await Pagar(DestinoCierre.Maxi, CuentaCaja.Efectivo, 60000);
await Pagar(DestinoCierre.Maxi, CuentaCaja.Banco, 80000);
await using (var db = Db())
{
    var r = await new CierreCajaService(db).DetalleAsync(cierreId);
    Check(r.Pendiente(DestinoCierre.Ani) == 0 && r.Pendiente(DestinoCierre.Maxi) == 0 && r.SaldosActuales.Sum(s=>s.Saldo) == 190000, "Pagos parciales a Ani/Maxi y saldos finales sin doble descuento");
    await Reject(()=>new CierreCajaService(db).TransferirAsync(CuentaCaja.Banco, CuentaCaja.Efectivo, 1, DateTime.UtcNow, Guid.NewGuid()), "No permite transferir dinero inexistente en banco");
    var vacio = await new CierreCajaService(db).CerrarAsync(semana.AddDays(-14), 30, null, "prueba", false);
    Check((await db.CierresCaja.FindAsync(vacio))!.Reserva == 0, "Se puede cerrar sin retiro ni ingresos");
}
await using (var db = Db())
{
    var pendiente = new Pedido { Cliente = new() { Nombre = "Devolución" }, MontoTotal = 100, FechaCreacion = DateTime.UtcNow };
    db.Add(pendiente); await db.SaveChangesAsync();
    var pedidos = new PedidoService(db);
    var cobroKey = Guid.NewGuid();
    await pedidos.RegistrarCobroAsync(pendiente.Id, 40, CuentaCaja.Efectivo, clave: cobroKey);
    await pedidos.RegistrarCobroAsync(pendiente.Id, 40, CuentaCaja.Efectivo, clave: cobroKey);
    Check(pendiente.MontoCobrado == 40 && await db.ReportesCaja.CountAsync(r=>r.ClaveOperacion == cobroKey) == 1, "Reintentar un cobro parcial no vuelve a cobrar");
    await pedidos.RegistrarCobroAsync(pendiente.Id, 60, CuentaCaja.Efectivo);
    await pedidos.RegistrarDevolucionAsync(pendiente.Id, 30, CuentaCaja.Efectivo, DateTime.UtcNow, Guid.NewGuid(), "Corrección prueba");
    Check(pendiente.MontoCobrado == 70 && await db.ReportesCaja.CountAsync(r=>r.IdPedido == pendiente.Id) == 3, "Devolución conserva ingreso original y ajusta monto cobrado");
    var cobro = await db.ReportesCaja.FirstAsync(r=>r.IdPedido == pendiente.Id && r.Tipo == TipoMovimiento.Ingreso);
    await Reject(()=>new ReporteCajaService(db).DeleteAsync(cobro.Id), "No se borra un cobro desligándolo del pedido");
    await Reject(()=>pedidos.AnularAsync(pendiente.Id), "No se anula un pedido con dinero sin devolver");
}
await using (var db = Db())
{
    var costoFijo = new Receta { TamanioLote = 2, Detalles = [new() { CantidadFija = 3, Insumo = new() { PrecioCompra = 100, CantidadRendimiento = 10 } }] };
    Check(costoFijo.CostoIngredientesPorUnidad == 30, "Receta de cantidades fijas no pierde su costo");
    var service = new CierreCajaService(db);
    var semanaAbierta = semana.AddDays(-7);
    var desconocido = new ReporteCaja { Fecha = CalendarioCaja.InicioUtc(semanaAbierta).AddHours(12), Tipo = TipoMovimiento.Ingreso, Categoria = CategoriaMovimiento.Venta, Monto = 10 };
    db.Add(desconocido); await db.SaveChangesAsync();
    await Reject(()=>service.CerrarAsync(semanaAbierta,30,null,null,false), "Movimiento antiguo sin cuenta impide guardar un cierre incompleto");
    await service.ClasificarAsync(desconocido.Id, CuentaCaja.Banco);
    Check((await service.ResumenAsync(semanaAbierta)).Cierre.CobrosBanco == 10, "Clasificar cobro histórico lo asigna a la cuenta correcta");
    await Reject(()=>service.ConfigurarAsync(30,inicio,0,0,150000,null), "No se duplica la apertura de saldos");
}
Check(CalendarioCaja.Lunes(new DateOnly(2026,9,13)) == new DateOnly(2026,9,7)
    && CalendarioCaja.InicioUtc(new DateOnly(2026,9,14)) == new DateTime(2026,9,14,3,0,0,DateTimeKind.Utc), "Calendario lunes-domingo con límite de Argentina");
var impar = new CierreCaja { CobrosEfectivo = .01m }; CierreCajaService.Repartir(impar,0);
Check(impar.Reserva + impar.Ani + impar.Maxi == .01m, "El reparto conserva el centavo indivisible");
await using (var db = Db())
{
    var productoId = await db.Productos.Select(p => p.Id).FirstAsync();
    var nuevo = new Pedido { Cliente = new() { Nombre = "Costo congelado" }, MontoTotal = 200, MontoCobrado = 200, FechaCreacion = DateTime.UtcNow,
        Detalles = [new() { IdProducto = productoId, Cantidad = 1, PrecioUnitario = 200, CostoEmpaque = 15 }] };
    db.Add(nuevo); await db.SaveChangesAsync();
    await new PedidoService(db).MarcarEntregadoAsync(nuevo.Id);
    var costo = nuevo.Detalles.Single().CostoIngredientes;
    Check(costo == 200000 && nuevo.FechaEntregaReal.HasValue && nuevo.Detalles.Single().FechaCosto.HasValue, "La entrega nueva congela fecha y costo de ingredientes");
    var harina = await db.Insumos.SingleAsync(i => i.Nombre == "Harina"); harina.PrecioCompra *= 2; await db.SaveChangesAsync();
    var resumen = await new CierreCajaService(db).ResumenAsync(CalendarioCaja.Lunes(CalendarioCaja.Hoy));
    Check(resumen.Cierre.CostoInsumos == costo + 15, "Cambio de precio posterior no revaloriza una entrega nueva");
    var gasto = new ReporteCaja { Fecha = DateTime.UtcNow, Cuenta = CuentaCaja.Banco, Tipo = TipoMovimiento.Egreso, Categoria = CategoriaMovimiento.Gasto,
        Monto = 10, Descripcion = "Gasto elegido", DescontarDelReparto = true };
    await new ReporteCajaService(db).CreateAsync(gasto);
    var conGasto = await new CierreCajaService(db).ResumenAsync(CalendarioCaja.Lunes(CalendarioCaja.Hoy));
    Check(conGasto.Cierre.BaseReparto == resumen.Cierre.BaseReparto - 10, "Solo el gasto elegido reduce la base de reparto");
}
Directory.CreateDirectory(".artifacts/caja-checks");
await File.WriteAllTextAsync(".artifacts/caja-checks/connection.txt", connection);
Console.WriteLine($"{checks} comprobaciones correctas. Base local: {database}");
