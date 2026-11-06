using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;

// Una base local nueva por ejecución. Nunca lee conexiones ni secretos de producción.
var database = "financiacion_checks_" + Guid.NewGuid().ToString("N");
var port = int.Parse(Environment.GetEnvironmentVariable("CAJA_TEST_PORT") ?? "55439");
var connection = $"Host=127.0.0.1;Port={port};Username=postgres;Database={database};Timeout=5";
var options = new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options;
PanaderiaContext Db() => new(options);
int checks = 0;
void Check(bool ok, string mensaje) { if (!ok) throw new Exception(mensaje); checks++; Console.WriteLine("PASS: " + mensaje); }
async Task Reject(Func<Task> f, string mensaje) { try { await f(); } catch (InvalidOperationException) { Check(true, mensaje); return; } throw new Exception("No se rechazó: " + mensaje); }
var semana = CalendarioCaja.Lunes(CalendarioCaja.Hoy).AddDays(-21);
var inicio = CalendarioCaja.InicioUtc(semana);
int proveedorId, insumoId, unidadId, productoId;
await using (var db = Db())
{
    await db.GetService<IMigrator>().MigrateAsync("20260930161804_AgregarCostoManualProducto");
    // Simula los grants automáticos de un proyecto Supabase anterior al 30/10/2026.
    await db.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon; END IF;
          IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated; END IF;
          IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='service_role') THEN CREATE ROLE service_role BYPASSRLS; END IF;
        END $$;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO anon, authenticated, service_role;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO anon, authenticated, service_role;
        """);
    var proveedor = new Proveedor { Nombre = "Proveedor de prueba" };
    var insumo = new Insumo { Nombre = "Harina", FechaCreacion = inicio };
    var unidad = new UnidadCompra { Insumo = insumo, Nombre = "Bolsa", FactorConversion = 1000 };
    var producto = new Producto { Nombre = "Pan dulce", Categoria = new() { Nombre = "Temporada" }, CostoManual = 100 };
    db.AddRange(proveedor, unidad, producto); await db.SaveChangesAsync();
    proveedorId = proveedor.Id; insumoId = insumo.Id; unidadId = unidad.Id; productoId = producto.Id;
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "ComprasProveedor" ("IdProveedor","Fecha","MontoTotal") VALUES ({proveedorId},{inicio.AddDays(-1)},50);
        INSERT INTO "ReportesCaja" ("Fecha","Tipo","Categoria","Monto","Cuenta","DescontarDelReparto","IdCompra")
        SELECT {inicio.AddDays(-1)},1,1,50,3,false,"Id" FROM "ComprasProveedor";
        """);
    await db.Database.MigrateAsync();
    Check(!db.Database.HasPendingModelChanges(), "Modelo y migración coinciden");
    Check(await db.AportesCompra.CountAsync() == 1 && await db.AportesCompra.AllAsync(x => x.Origen == OrigenFondos.MasaViva), "Migración conserva compra conocida sin inventar deuda personal");
    var conn = db.Database.GetDbConnection(); await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT count(*) FROM pg_tables WHERE schemaname='public' AND rowsecurity AND tablename IN
        ('AportesCompra','LotesFinanciados','AsignacionesCompraLote','VentasLote','ReservasReintegro','PagosReintegro')
        """;
    Check(Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 6, "RLS habilitado en las seis tablas internas");
    cmd.CommandText = """
        SELECT count(*) FROM information_schema.role_table_grants WHERE table_schema='public'
        AND table_name IN ('AportesCompra','LotesFinanciados','AsignacionesCompraLote','VentasLote','ReservasReintegro','PagosReintegro')
        AND grantee IN ('anon','authenticated','service_role','PUBLIC')
        """;
    Check(Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0, "Grants heredados revocados: no se expone el libro personal por Data API");
    cmd.CommandText = "SET ROLE authenticated; SELECT count(*) FROM public.\"AportesCompra\"";
    try { await cmd.ExecuteScalarAsync(); throw new Exception("authenticated pudo leer aportes"); }
    catch (PostgresException ex) when (ex.SqlState == "42501") { Check(true, "authenticated no puede leer aportes aunque exista el rol"); }
    finally { cmd.CommandText = "RESET ROLE"; await cmd.ExecuteNonQueryAsync(); }
    cmd.CommandText = "SELECT count(*) FROM pg_policies WHERE schemaname='public' AND tablename='AportesCompra'";
    Check(Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0, "RLS no confunde usuarios Supabase con administradores MVC");
    await conn.CloseAsync();
    await db.GetService<IMigrator>().MigrateAsync("20260930161804_AgregarCostoManualProducto");
    await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
    Check(await db.AportesCompra.CountAsync() == 1 && await db.ComprasProveedor.CountAsync() == 1, "Migración reversible y reaplicable conserva la compra anterior");
    await new CierreCajaService(db).ConfigurarAsync(30, inicio, 0, 10000, 1000, "prueba");
}

CompraProveedor NuevaCompra(decimal precio, decimal envio, List<AporteCompra> aportes, Guid? clave = null) => new() {
    IdProveedor = proveedorId, Fecha = inicio.AddHours(1), ClaveOperacion = clave ?? Guid.NewGuid(), CuentaPago = CuentaCaja.ReservaMercadoPago,
    Detalles = [new() { IdInsumo = insumoId, IdUnidadCompra = unidadId, Cantidad = 1, PrecioUnitario = precio, CostoEnvio = envio }], Aportes = aportes };
int compraId, personalId, maxiId, aniId, maxiOtroId, loteId, lote2Id;
var compraClave = Guid.NewGuid();
var personalClave = Guid.NewGuid();
await using (var db = Db())
{
    var compra = NuevaCompra(900,100,[new() { Origen=OrigenFondos.MasaViva, Cuenta=CuentaCaja.ReservaMercadoPago,Monto=600 },new() { Origen=OrigenFondos.Maxi,Monto=300 },new() { Origen=OrigenFondos.Ani,Monto=100 }], compraClave);
    await new CompraService(db).CreateAsync(compra);
    compraId = compra.Id; maxiId = compra.Aportes.Single(x=>x.Origen==OrigenFondos.Maxi).Id; aniId = compra.Aportes.Single(x=>x.Origen==OrigenFondos.Ani).Id;
    var personal = NuevaCompra(200,0,[new() { Origen=OrigenFondos.Maxi,Monto=200 }],personalClave);
    await new CompraService(db).CreateAsync(personal); personalId=personal.Id; maxiOtroId=personal.Aportes.Single().Id;
    Check(await db.ReportesCaja.Where(x=>x.IdCompra==compraId).SumAsync(x=>x.Monto)==600 && !await db.ReportesCaja.AnyAsync(x=>x.IdCompra==personalId), "Compra dividida solo egresa 600 de Masa Viva; compra personal no inventa egreso");
    Check((await db.Insumos.FindAsync(insumoId))!.StockActual==2000 && (await db.Insumos.FindAsync(insumoId))!.PrecioCompra==200, "Compras conservan stock y costo con envío");
    var f = new FinanciacionService(db);
    var lote = new LoteFinanciado { Nombre="Lote 1",IdProducto=productoId,Unidades=10,DesdeUtc=inicio.AddDays(1),ClaveOperacion=Guid.NewGuid() };
    await f.CrearLoteAsync(lote); loteId=lote.Id;
    var lote2 = new LoteFinanciado { Nombre="Lote 2",IdProducto=productoId,Unidades=10,DesdeUtc=inicio.AddDays(1),ClaveOperacion=Guid.NewGuid() };
    await f.CrearLoteAsync(lote2); lote2Id=lote2.Id;
    var clave=Guid.NewGuid(); await f.AsignarAsync(compraId,loteId,800,clave); await f.AsignarAsync(compraId,loteId,800,clave);
    await f.AsignarAsync(personalId,loteId,200,Guid.NewGuid()); await f.AsignarAsync(compraId,lote2Id,200,Guid.NewGuid());
    Check(await db.AsignacionesCompraLote.CountAsync()==3, "Varias compras forman un lote, una compra abastece dos lotes y reintentar no duplica");
    await f.PagarAsync(maxiOtroId,null,CuentaCaja.Banco,100,Guid.NewGuid(),"prueba",inicio.AddDays(2));
    Check((await f.ResumenAsync()).Deudas.Sum(x=>x.Deuda)==500, "Adelanto confirmado reduce una única deuda incluso sin ventas");
}
async Task ReintentarPersonal() { await using var db=Db(); await new CompraService(db).CreateAsync(NuevaCompra(200,0,[new(){Origen=OrigenFondos.Maxi,Monto=200}],personalClave)); }
await Task.WhenAll(ReintentarPersonal(),ReintentarPersonal());
await using(var db=Db()) { Check(await db.ComprasProveedor.CountAsync()==3 && (await db.Insumos.FindAsync(insumoId))!.StockActual==2000,"Reintentos concurrentes de compra completamente personal no duplican deuda ni stock"); }
await using (var db=Db()) { await Reject(()=>new FinanciacionService(db).AsignarAsync(compraId,lote2Id,1,Guid.NewGuid()),"Rechaza asignar dos veces una compra al mismo lote"); }
await using (var db=Db()) {
    var l=new LoteFinanciado { Nombre="Exceso",IdProducto=productoId,Unidades=10,DesdeUtc=inicio.AddDays(1),ClaveOperacion=Guid.NewGuid() };
    await new FinanciacionService(db).CrearLoteAsync(l);
    await Reject(()=>new FinanciacionService(db).AsignarAsync(compraId,l.Id,1,Guid.NewGuid()),"No permite asignar más costo que el total comprado");
}
await using (var db=Db()) { await Reject(()=>new CompraService(db).CreateAsync(NuevaCompra(50,0,[new(){Origen=OrigenFondos.Maxi,Monto=51}])) ,"Rechaza fondos que no coinciden con el total"); }
await using (var db=Db()) { await Reject(()=>new CompraService(db).CreateAsync(NuevaCompra(50,0,[new(){Origen=OrigenFondos.Ani,Monto=50,Cuenta=CuentaCaja.Banco}])) ,"Un aporte personal no usa una cuenta de Masa Viva"); }
async Task Venta(int cantidad, DateTime entrega, bool pagado=true, bool entregado=true)
{
    await using var db=Db();
    var p=new Pedido { Cliente=new(){Nombre="Cliente prueba"},FechaCreacion=entrega,FechaEntrega=entrega,
        FechaEntregaReal=entregado?entrega:null,Estado=entregado?EstadoPedido.Entregado:EstadoPedido.Pendiente,MontoTotal=cantidad*500,MontoCobrado=pagado?cantidad*500:0,
        Detalles=[new(){IdProducto=productoId,Cantidad=cantidad,PrecioUnitario=500,CostoIngredientes=100}] };
    db.Add(p); await db.SaveChangesAsync();
    if(pagado) { db.ReportesCaja.Add(new(){IdPedido=p.Id,Fecha=entrega,Tipo=TipoMovimiento.Ingreso,Categoria=CategoriaMovimiento.Venta,Cuenta=CuentaCaja.Banco,Monto=p.MontoTotal}); await db.SaveChangesAsync(); }
}
await Venta(4,inicio.AddDays(3)); await Venta(8,inicio.AddDays(10)); await Venta(8,inicio.AddDays(17));
await Venta(50,inicio.AddDays(3),false); await Venta(50,inicio.AddDays(3),true,false);
int cierreId;
await using(var db=Db())
{
    var f=new FinanciacionService(db); var r=await f.ResumenAsync(inicio.AddDays(7),inicio);
    Check(r.VentasNuevas.Sum(x=>x.Unidades)==4 && r.GeneradoNuevo==208 && r.Deudas.Single(x=>x.Aporte.Id==maxiId).Generado==96, "Cuatro entregas cobradas generan 208; anticipos y entregas impagas no generan reintegro");
    // El anticipo cobrado permanece en la caja y el reparto existente; no genera reintegro de lote.
    var service=new CierreCajaService(db); var previo=await service.ResumenAsync(semana);
    Check(previo.Cierre.ReintegrosProvisionados==108 && previo.Cierre.AdelantosPersonales==100, "Adelanto descuenta exactamente los próximos 100 sugeridos por ventas");
    Check(previo.Cierre.BaseReparto==26792 && previo.Cierre.Reserva+previo.Cierre.Ani+previo.Cierre.Maxi==26792, "Cierre descuenta adelanto y provisión una sola vez y conserva el reparto");
    Check(await db.ReservasReintegro.CountAsync()==0 && await db.VentasLote.CountAsync()==0, "Vista previa no guarda pagos, reservas ni consumos de lotes");
    cierreId=await service.CerrarAsync(semana,30,null,"prueba",false);
    Check((await f.ResumenAsync()).Deudas.Sum(x=>x.Deuda)==500 && await db.PagosReintegro.CountAsync()==1, "Guardar cierre separa reintegros sin marcar deuda como pagada");
    Check(await db.VentasLote.SumAsync(x=>x.Unidades)==4, "Cierre congela las cuatro unidades una sola vez");
}
await using(var db=Db()) { await Reject(()=>new FinanciacionService(db).AsignarAsync(personalId,loteId,1,Guid.NewGuid()),"No cambia costos de un lote ya cerrado"); }
await using(var db=Db()) { await Reject(()=>new CierreCajaService(db).CerrarAsync(semana,30,null,null,false),"No duplica un cierre financiado"); }
int reservaId;
await using(var db=Db()) { reservaId=await db.ReservasReintegro.Where(x=>x.IdCierre==cierreId&&x.IdAporte==maxiId).Select(x=>x.Id).SingleAsync(); }
var pagoClave=Guid.NewGuid();
async Task PagarReserva(int aporte,int reserva,decimal monto,Guid? key=null) { await using var db=Db(); await new FinanciacionService(db).PagarAsync(aporte,reserva,CuentaCaja.Banco,monto,key??Guid.NewGuid(),"prueba"); }
await Task.WhenAll(PagarReserva(maxiId,reservaId,50,pagoClave),PagarReserva(maxiId,reservaId,50,pagoClave));
await using(var db=Db()) {
    Check(await db.PagosReintegro.CountAsync(x=>x.IdReserva==reservaId)==1, "Confirmación concurrente con la misma clave crea un único pago parcial");
    var mov=await db.ReportesCaja.SingleAsync(x=>x.ClaveOperacion==pagoClave);
    Check(!mov.DescontarDelReparto && mov.Monto==50, "Pago de un reintegro reservado no vuelve a reducir el reparto");
    await Reject(()=>new ReporteCajaService(db).DeleteAsync(mov.Id),"El pago confirmado no se borra como movimiento manual");
}
await using(var db=Db()) { await Reject(()=>new FinanciacionService(db).PagarAsync(maxiId,reservaId,CuentaCaja.Banco,27,Guid.NewGuid(),null),"Rechaza pagar por encima del pendiente del cierre"); }
await PagarReserva(maxiId,reservaId,26);
await using(var db=Db()) {
    foreach(var reserva in await db.ReservasReintegro.Where(x=>x.IdCierre==cierreId&&x.IdAporte!=maxiId).ToListAsync()) await new FinanciacionService(db).PagarAsync(reserva.IdAporte,reserva.Id,CuentaCaja.Banco,reserva.Monto,Guid.NewGuid(),null);
    await new FinanciacionService(db).PagarAsync(maxiId,null,CuentaCaja.Banco,200,Guid.NewGuid(),null);
    var guardado=await new CierreCajaService(db).DetalleAsync(cierreId);
    Check(guardado.Cierre.ReintegrosGenerados==208 && guardado.Cierre.ReintegrosProvisionados==108 && guardado.Cierre.BaseReparto==26792, "Pagos posteriores no alteran la fotografía del cierre");
    var siguiente=await new CierreCajaService(db).ResumenAsync(semana.AddDays(7));
    Check(siguiente.Cierre.ReintegrosGenerados==328 && siguiente.Cierre.ReintegrosProvisionados==128, "Adelanto adicional de 200 compensa ventas futuras; no se devuelve dos veces");
    var consumos=siguiente.Financiacion.VentasNuevas;
    Check(consumos.Where(x=>x.IdLote==loteId).Sum(x=>x.Unidades)==6 && consumos.Where(x=>x.IdLote==lote2Id).Sum(x=>x.Unidades)==2, "FIFO reparte ocho ventas entre dos lotes sin duplicar unidades");
}
foreach(var s in new[]{semana.AddDays(7),semana.AddDays(14)})
{
    int id; await using(var db=Db()) { id=await new CierreCajaService(db).CerrarAsync(s,30,null,null,false); }
    await using(var db=Db()) { foreach(var reserva in await db.ReservasReintegro.Where(x=>x.IdCierre==id).ToListAsync()) await PagarReserva(reserva.IdAporte,reserva.Id,reserva.Monto); }
}
await using(var db=Db())
{
    var r=await new FinanciacionService(db).ResumenAsync();
    Check(r.Deudas.Sum(x=>x.Deuda)==0 && r.Deudas.Sum(x=>x.Pagado)==600 && r.Deudas.Sum(x=>x.Sugerido)==0, "Recuperación completa: 600 aportados, 600 pagados, ninguna deuda o sugerencia duplicada");
    Check(await db.VentasLote.SumAsync(x=>x.Unidades)==20 && await db.ReservasReintegro.SumAsync(x=>x.Monto)==300, "Veinte unidades generan solo 300 de reintegros después de 300 en adelantos");
    Check((await new CierreCajaService(db).ResumenAsync(CalendarioCaja.Lunes(CalendarioCaja.Hoy))).Cierre.GastosReparto==200, "Semana del pago descuenta solo el adelanto nuevo, no los reintegros de cierres");
    await Reject(()=>new FinanciacionService(db).PagarAsync(maxiId,null,CuentaCaja.Banco,.01m,Guid.NewGuid(),null),"No reintegra más de la deuda original");
    await Reject(()=>new CierreCajaService(db).CerrarAsync(semana.AddDays(-7),30,null,null,false),"Con financiación evita cerrar semanas fuera de orden");
}
// Centavos, caja insuficiente y concurrencia con claves distintas en una deuda nueva.
int pequenoMaxi, pequenoAni;
await using(var db=Db())
{
    var compra=NuevaCompra(.02m,0,[new(){Origen=OrigenFondos.Maxi,Monto=.01m},new(){Origen=OrigenFondos.Ani,Monto=.01m}]);
    compra.Fecha=CalendarioCaja.InicioUtc(CalendarioCaja.Hoy);
    await new CompraService(db).CreateAsync(compra);
    pequenoMaxi=compra.Aportes.Single(x=>x.Origen==OrigenFondos.Maxi).Id; pequenoAni=compra.Aportes.Single(x=>x.Origen==OrigenFondos.Ani).Id;
    var lote=new LoteFinanciado { Nombre="Centavos",IdProducto=productoId,Unidades=2,DesdeUtc=compra.Fecha,ClaveOperacion=Guid.NewGuid() };
    await new FinanciacionService(db).CrearLoteAsync(lote);
    await new FinanciacionService(db).AsignarAsync(compra.Id,lote.Id,.02m,Guid.NewGuid());
}
await Venta(1,DateTime.UtcNow.AddSeconds(-1));
await using(var db=Db())
{
    var r=await new FinanciacionService(db).ResumenAsync();
    Check(r.Deudas.Where(x=>x.Aporte.Id==pequenoMaxi||x.Aporte.Id==pequenoAni).Sum(x=>x.Generado)==.01m,"Un centavo de costo parcial se atribuye una vez entre dos personas");
    await Reject(()=>new FinanciacionService(db).PagarAsync(pequenoAni,null,CuentaCaja.Efectivo,.01m,Guid.NewGuid(),null),"No confirma un reintegro sin fondos en la cuenta elegida");
}
async Task<bool> IntentarPagoCentavo() { await using var db=Db(); try { await new FinanciacionService(db).PagarAsync(pequenoMaxi,null,CuentaCaja.Banco,.01m,Guid.NewGuid(),null); return true; } catch(InvalidOperationException) { return false; } }
var simultaneos=await Task.WhenAll(IntentarPagoCentavo(),IntentarPagoCentavo());
Check(simultaneos.Count(x=>x)==1,"Dos pagos concurrentes con claves distintas no superan la deuda");
await using(var db=Db())
{
    var resumen=await new FinanciacionService(db).ResumenAsync();
    var cierre=new CierreCaja { CobrosBanco=.01m };
    var soloAni=new Panaderia.Models.DTOs.FinanciacionResumen { Deudas=[new(new AporteCompra { Id=pequenoAni,Monto=100,Origen=OrigenFondos.Ani },100,0,0)] };
    FinanciacionService.PrepararCierre(db,cierre,soloAni,false); CierreCajaService.Repartir(cierre,30);
    Check(cierre.ReintegrosProvisionados==.01m && cierre.BaseReparto==0 && cierre.Reserva+cierre.Ani+cierre.Maxi==0,"Una sugerencia mayor que los cobros solo separa el dinero disponible antes de reservar y repartir");
}
Directory.CreateDirectory(".artifacts/financiacion-checks");
await File.WriteAllTextAsync(".artifacts/financiacion-checks/connection.txt",connection);
Console.WriteLine($"{checks} comprobaciones correctas. Base local: {database}");
