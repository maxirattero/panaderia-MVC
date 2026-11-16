using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;

// Base local nueva por ejecución. No usa secretos ni conexiones de producción.
var database = "materiales_checks_" + Guid.NewGuid().ToString("N");
var port = int.Parse(Environment.GetEnvironmentVariable("CAJA_TEST_PORT") ?? "55439");
var connection = $"Host=127.0.0.1;Port={port};Username=postgres;Database={database};Timeout=5";
var options = new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options;
PanaderiaContext Db() => new(options);
int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); checks++; }
async Task Reject(Func<Task> action, string message) { try { await action(); } catch (InvalidOperationException) { Check(true,message); return; } throw new Exception("No se rechazó: " + message); }
int productoId, clienteId, legadoId, legadoDetalleId;
int moldeId, monoId, cintaId, papelId, selladaId, regaloId, etiquetaId, habitualId, ingredienteId;
await using (var db = Db())
{
    await db.GetService<IMigrator>().MigrateAsync("20261106085224_IncorporarFinanciacionInsumos");
    var categoria = new CategoriaProducto { Nombre = "Temporada" };
    var cliente = new Cliente { Nombre = "Cliente materiales" };
    var producto = new Producto { Nombre = "Pan dulce", Categoria = categoria, PorEncargo = true, CostoManual = 1000, PrecioFinal = 3000, PrecioReventa = 2500 };
    var legado = new Pedido { Cliente = cliente, FechaCreacion = DateTime.UtcNow, FechaEntrega = DateTime.UtcNow, MontoTotal = 3000 };
    db.AddRange(producto,legado); await db.SaveChangesAsync();
    productoId = producto.Id; clienteId = cliente.Id; legadoId = legado.Id;
    await db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "DetallesPedido" ("IdPedido","IdProducto","Cantidad","CantidadProducida","ReservaStock","PrecioUnitario","LlevaEtiqueta","CostoEmpaque")
        VALUES ({legadoId},{productoId},1,0,false,3000,false,12.3456)
        """);
    await db.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon; END IF;
          IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated; END IF;
          IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='service_role') THEN CREATE ROLE service_role BYPASSRLS; END IF;
        END $$;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO anon, authenticated, service_role;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO anon, authenticated, service_role;
        """);
    await db.Database.MigrateAsync();
    Check(!db.Database.HasPendingModelChanges(), "Modelo y migración coinciden");
    var viejo = await db.DetallesPedido.SingleAsync(); legadoDetalleId=viejo.Id;
    Check(viejo.MaterialesJson==null && viejo.CostoEmpaque==12.3456m, "Migración conserva costos anteriores sin inventar materiales");
    await using (var cmd=new NpgsqlCommand("SELECT rowsecurity FROM pg_tables WHERE schemaname='public' AND tablename='ProductosMateriales'",(NpgsqlConnection)db.Database.GetDbConnection())) {
        await db.Database.OpenConnectionAsync(); Check((bool)(await cmd.ExecuteScalarAsync())!,"RLS habilitado en ProductosMateriales");
        cmd.CommandText="SELECT has_table_privilege('authenticated', 'public.\"ProductosMateriales\"','SELECT') OR has_table_privilege('anon', 'public.\"ProductosMateriales\"','INSERT') OR has_table_privilege('service_role', 'public.\"ProductosMateriales\"','SELECT')";
        Check(!(bool)(await cmd.ExecuteScalarAsync())!,"Revoca grants heredados de Data API y mantiene tablas internas");
        cmd.CommandText="SELECT has_sequence_privilege('authenticated',pg_get_serial_sequence('public.\"ProductosMateriales\"','Id'),'USAGE')";
        Check(!(bool)(await cmd.ExecuteScalarAsync())!,"La secuencia tampoco queda accesible por Data API");
        await db.Database.CloseConnectionAsync();
    }
    db.ChangeTracker.Clear();
    await db.GetService<IMigrator>().MigrateAsync("20261106085224_IncorporarFinanciacionInsumos");
    await db.Database.MigrateAsync();
    Check((await db.DetallesPedido.SingleAsync()).CostoEmpaque==12.3456m,"Migración reversible y reaplicable preserva pedidos anteriores");
    Insumo Ins(string nombre,TipoInsumo tipo,decimal precio,decimal rendimiento=100) => new() { Nombre=nombre,TipoInsumo=tipo,PrecioCompra=precio,CantidadRendimiento=rendimiento,StockActual=1000 };
    var molde=Ins("Molde pan dulce",TipoInsumo.Empaque,20000);
    var mono=Ins("Moño",TipoInsumo.Empaque,6000);
    var cinta=Ins("Cinta",TipoInsumo.Consumible,500);
    var papel=Ins("Bolsa papel",TipoInsumo.Empaque,10000); papel.EsBolsaPapel=true;
    var sellada=Ins("Bolsa sellada",TipoInsumo.Empaque,15000);
    var regalo=Ins("Bolsa regalo",TipoInsumo.Empaque,25000);
    var habitual=Ins("Etiqueta habitual",TipoInsumo.Etiqueta,1000);
    var especial=Ins("Etiqueta pan dulce",TipoInsumo.Etiqueta,2000);
    var ingrediente=Ins("Harina",TipoInsumo.Ingrediente,1000,1000);
    db.AddRange(molde,mono,cinta,papel,sellada,regalo,habitual,especial,ingrediente); await db.SaveChangesAsync();
    moldeId=molde.Id; monoId=mono.Id; cintaId=cinta.Id; papelId=papel.Id; selladaId=sellada.Id; regaloId=regalo.Id; etiquetaId=especial.Id; habitualId=habitual.Id; ingredienteId=ingrediente.Id;
}
List<ProductoMaterial> Materiales() => [new(){IdInsumo=moldeId,Cantidad=1},new(){IdInsumo=monoId,Cantidad=1},new(){IdInsumo=cintaId,Cantidad=.5m},new(){IdInsumo=etiquetaId,Cantidad=1}];
await using(var db=Db())
{
    var service=new ProductoService(db); var producto=(await service.GetByIdAsync(productoId))!;
    producto.Materiales=Materiales(); producto.IdEmpaquePredeterminado=papelId; producto.EtiquetaPredeterminada=true;
    await service.UpdateAsync(producto); db.ChangeTracker.Clear();
    var leido=(await service.GetByIdAsync(productoId))!;
    Check(leido.Materiales.Count==4 && leido.Materiales.Single(x=>x.IdInsumo==cintaId).Cantidad==.5m,"Producto guarda cantidades por unidad y recupera los insumos");
    var insumos=await db.Insumos.AsNoTracking().ToDictionaryAsync(x=>x.Id);
    var presentacion=PresentacionProducto.Calcular(leido,insumos,papelId,true);
    Check(presentacion.Sum(x=>x.Costo)==382.5m && presentacion.Single(x=>x.Uso==UsoMaterial.Etiqueta).IdInsumo==etiquetaId,"Suma molde, moño, cinta, bolsa y etiqueta elegida exactamente una vez");
    Check(PresentacionProducto.Calcular(leido,insumos,selladaId,false).Sum(x=>x.Costo)==412.5m,"Cambiar a sellada reemplaza papel y destildar etiqueta la excluye");
    var simple=new Producto();
    Check(PresentacionProducto.Calcular(simple,insumos,papelId,true).Sum(x=>x.Costo)==110,"Un pan sin adicionales conserva bolsa y etiqueta habitual");
    var doble=new Producto { Materiales=[new(){IdInsumo=regaloId,Cantidad=1}] };
    Check(PresentacionProducto.Calcular(doble,insumos,regaloId,false).Sum(x=>x.Costo)==250,"Bolsa principal que coincide con adicional no se cuenta dos veces");
    doble.Materiales[0].Cantidad=2;
    Check(PresentacionProducto.Calcular(doble,insumos,regaloId,false).Sum(x=>x.Costo)==500,"Dos envoltorios expresos conservan dos unidades, no tres");
    var copia=await service.DuplicarAsync(productoId); db.ChangeTracker.Clear();
    var duplicado=(await service.GetByIdAsync(copia!.Id))!;
    Check(duplicado.Materiales.Count==4 && duplicado.IdEmpaquePredeterminado==papelId && duplicado.EtiquetaPredeterminada,"Duplicar producto copia materiales y presentación");
}
async Task Invalidar(List<ProductoMaterial> materiales,string message) {
    await using var db=Db(); var producto=(await new ProductoService(db).GetByIdAsync(productoId))!; producto.Materiales=materiales;
    await Reject(()=>new ProductoService(db).UpdateAsync(producto),message);
}
await Invalidar([new(){IdInsumo=moldeId,Cantidad=1},new(){IdInsumo=moldeId,Cantidad=2}],"Rechaza materiales repetidos");
await Invalidar([new(){IdInsumo=moldeId,Cantidad=0}],"Rechaza cantidades no positivas");
await Invalidar([new(){IdInsumo=moldeId,Cantidad=.00001m}],"Rechaza cantidades con más de cuatro decimales");
await Invalidar([new(){IdInsumo=ingredienteId,Cantidad=1}],"Los ingredientes siguen perteneciendo a la receta");
await Invalidar([new(){IdInsumo=etiquetaId,Cantidad=1},new(){IdInsumo=habitualId,Cantidad=1}],"Rechaza dos etiquetas para una misma selección del pedido");
Pedido Nuevo(int cantidad=2,int? bolsa=null,bool etiqueta=true) => new(){IdCliente=clienteId,FechaCreacion=DateTime.UtcNow,FechaEntrega=DateTime.UtcNow, MontoTotal=3000*cantidad,
    Detalles=[new(){IdProducto=productoId,Cantidad=cantidad,PrecioUnitario=3000,IdEmpaque=bolsa??papelId,LlevaEtiqueta=etiqueta}]};
int pedidoId, detalleId;
await using(var db=Db())
{
    var pedido=Nuevo(); await new PedidoService(db).CreateAsync(pedido); pedidoId=pedido.Id; detalleId=pedido.Detalles.Single().Id;
    var d=pedido.Detalles.Single();
    Check(d.CostoEmpaque==382.5m && d.Materiales.Sum(x=>x.Costo)==d.CostoEmpaque,"Pedido guarda desglose y costo unitario que concilian");
    Check(d.MaterialesJson!=null && d.Materiales.Single(x=>x.IdInsumo==cintaId).Cantidad==.5m,"Snapshot conserva cantidad, nombre, precio y uso de cada material");
    Check(pedido.MontoTotal==6000 && d.PrecioUnitario==3000,"Materiales no alteran automáticamente el precio de venta");
    Check(await db.Insumos.AllAsync(i=>i.StockActual==1000),"Registrar costos no duplica movimientos ni deducciones de stock");
    foreach(var insumo in await db.Insumos.ToListAsync()) insumo.PrecioCompra*=2;
    (await db.Insumos.FindAsync(moldeId))!.Nombre="Molde con nombre nuevo";
    await db.SaveChangesAsync();
    var producto=(await new ProductoService(db).GetByIdAsync(productoId))!;
    producto.Materiales=[new(){IdInsumo=moldeId,Cantidad=2},new(){IdInsumo=etiquetaId,Cantidad=1}];
    await new ProductoService(db).UpdateAsync(producto);
}
await using(var db=Db())
{
    var d=await db.DetallesPedido.SingleAsync(x=>x.Id==detalleId);
    Check(d.CostoEmpaque==382.5m && d.Materiales.Single(x=>x.IdInsumo==moldeId).Nombre=="Molde pan dulce","Cambios de precios, nombres y configuración no reescriben pedidos");
    var editar=Nuevo(3); editar.Id=pedidoId; editar.Detalles.Single().Id=detalleId;
    await new PedidoService(db).UpdateAsync(editar); db.ChangeTracker.Clear();
    d=await db.DetallesPedido.SingleAsync(x=>x.IdPedido==pedidoId); detalleId=d.Id;
    Check(d.CostoEmpaque==382.5m && d.Cantidad==3,"Editar cantidad conserva materiales y costos históricos de esa línea");
    var cambio=Nuevo(3,selladaId); cambio.Id=pedidoId; cambio.Detalles.Single().Id=detalleId;
    await new PedidoService(db).UpdateAsync(cambio); db.ChangeTracker.Clear();
    d=await db.DetallesPedido.SingleAsync(x=>x.IdPedido==pedidoId); detalleId=d.Id;
    Check(d.CostoEmpaque==582.5m && d.Materiales.Single(x=>x.Uso==UsoMaterial.Etiqueta).Costo==20,"Cambiar bolsa cotiza solo la bolsa y conserva adicionales y etiqueta anteriores");
    var sinEtiqueta=Nuevo(3,selladaId,false); sinEtiqueta.Id=pedidoId; sinEtiqueta.Detalles.Single().Id=detalleId;
    await new PedidoService(db).UpdateAsync(sinEtiqueta); db.ChangeTracker.Clear();
    d=await db.DetallesPedido.SingleAsync(x=>x.IdPedido==pedidoId);
    Check(d.CostoEmpaque==562.5m && d.Materiales.All(x=>x.Uso!=UsoMaterial.Etiqueta),"Destildar etiqueta elimina solamente su costo");
    var nuevo=Nuevo(); await new PedidoService(db).CreateAsync(nuevo);
    Check(nuevo.Detalles.Single().CostoEmpaque==1040,"Los pedidos nuevos usan la configuración y precios nuevos");
}
await using(var db=Db())
{
    var viejo=Nuevo(4); viejo.Id=legadoId; var d=viejo.Detalles.Single(); d.Id=legadoDetalleId; d.IdEmpaque=null; d.LlevaEtiqueta=false;
    await new PedidoService(db).UpdateAsync(viejo); db.ChangeTracker.Clear();
    var leido=await db.DetallesPedido.SingleAsync(x=>x.IdPedido==legadoId);
    Check(leido.MaterialesJson==null && leido.CostoEmpaque==12.3456m,"Editar pedido antiguo no agrega materiales actuales retroactivamente");
}
// Ampliaciones web: misma presentación se unifica, cambios mantienen líneas distintas.
int webId;
var sabado=DateTime.UtcNow.Date.AddDays(((int)DayOfWeek.Saturday-(int)DateTime.UtcNow.DayOfWeek+7)%7);
await using(var db=Db()) { var p=Nuevo(1); p.FechaEntrega=sabado; webId=(await new PedidoService(db).CrearOAmpliarDesdeTiendaAsync(p)).Id; }
await using(var db=Db())
{
    var p=Nuevo(1); p.FechaEntrega=sabado; var unido=await new PedidoService(db).CrearOAmpliarDesdeTiendaAsync(p);
    Check(unido.Id==webId && unido.Detalles.Count==1 && unido.Detalles.Single().Cantidad==2,"La tienda unifica snapshots iguales aunque jsonb cambie el orden de las propiedades");
    var molde=await db.Insumos.FindAsync(moldeId); molde!.PrecioCompra+=100; await db.SaveChangesAsync();
}
await using(var db=Db())
{
    var p=Nuevo(1); p.FechaEntrega=sabado; var unido=await new PedidoService(db).CrearOAmpliarDesdeTiendaAsync(p);
    Check(unido.Id==webId && unido.Detalles.Count==2 && unido.Detalles.Sum(x=>x.Cantidad)==3,"Cambio de costo en una ampliación conserva separada la presentación anterior");
    var pedido=await db.Pedidos.Include(x=>x.Detalles).SingleAsync(x=>x.Id==pedidoId);
    pedido.Estado=EstadoPedido.Entregado; pedido.FechaEntregaReal=DateTime.UtcNow;
    foreach(var d in pedido.Detalles) d.CostoIngredientes=1000;
    await db.SaveChangesAsync();
    var cierre=(await new CierreCajaService(db).ResumenAsync(CalendarioCaja.Lunes(CalendarioCaja.Hoy))).Cierre;
    Check(cierre.CostoInsumos==4687.5m && cierre.Costos.Sum(x=>x.Empaque)==1687.5m,"Cierre suma elaboración y materiales guardados una sola vez");
    Check(cierre.GastosReparto==0 && cierre.ReintegrosProvisionados==0 && !await db.ReportesCaja.AnyAsync(),"Agregar materiales no crea egresos ni reintegros personales");
}
await using(var db=Db())
{
    var producto=(await new ProductoService(db).GetByIdAsync(productoId))!;
    var insumos=await db.Insumos.AsNoTracking().ToDictionaryAsync(x=>x.Id);
    producto.Materiales=[new(){IdInsumo=regaloId,Cantidad=1}];
    var conBolsa=PresentacionProducto.Calcular(producto,insumos,regaloId,false);
    var json=MaterialesPedido.Guardar(conBolsa);
    var reordenado=System.Text.Json.JsonSerializer.Serialize(conBolsa.AsEnumerable().Reverse());
    Check(MaterialesPedido.Iguales(json,reordenado),"Comparación de snapshots es semántica y no depende del orden de serialización");
}
int superpuestoId, superpuestoDetalle;
await using(var db=Db())
{
    var productos=new ProductoService(db);
    var producto=(await productos.GetByIdAsync(productoId))!;
    producto.Materiales=[new(){IdInsumo=regaloId,Cantidad=1}]; producto.EtiquetaPredeterminada=false;
    await productos.UpdateAsync(producto);
    var pedido=Nuevo(1,regaloId,false); await new PedidoService(db).CreateAsync(pedido);
    superpuestoId=pedido.Id; superpuestoDetalle=pedido.Detalles.Single().Id;
    Check(pedido.Detalles.Single().CostoEmpaque==500,"Bolsa y adicional coincidentes guardan un solo costo con los precios nuevos");
}
await using(var db=Db())
{
    var cambio=Nuevo(1,papelId,false); cambio.Id=superpuestoId; cambio.Detalles.Single().Id=superpuestoDetalle;
    await new PedidoService(db).UpdateAsync(cambio); db.ChangeTracker.Clear();
    var d=await db.DetallesPedido.SingleAsync(x=>x.IdPedido==superpuestoId);
    Check(d.CostoEmpaque==700 && d.Materiales.Single(x=>x.Uso==UsoMaterial.Adicional).CantidadAplicada==1,"Cambiar de bolsa restaura el envoltorio adicional original una sola vez");
    var material=await db.Insumos.FindAsync(regaloId); material!.Activo=false; await db.SaveChangesAsync();
}
await Invalidar([new(){IdInsumo=regaloId,Cantidad=1}],"Rechaza materiales desactivados para configuraciones nuevas");
Directory.CreateDirectory(".artifacts/materiales-checks");
await File.WriteAllTextAsync(".artifacts/materiales-checks/connection.txt",connection);
Console.WriteLine($"{checks} comprobaciones correctas. Base local: {database}");
