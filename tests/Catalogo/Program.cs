using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;

// Base nueva y descartable, exclusivamente en PostgreSQL local. Sin secretos de producción.
var database = "catalogo_checks_" + Guid.NewGuid().ToString("N");
var connection = $"Host=127.0.0.1;Port=55459;Username=postgres;Database={database}";
var options = new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options;
PanaderiaContext Db() => new(options);
int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); checks++; }
async Task Reject(Func<Task> action, string message)
{
    try { await action(); } catch (InvalidOperationException) { Check(true, message); return; }
    throw new Exception("Se esperaba rechazo: " + message);
}
var now = DateTime.UtcNow;
await using var db = Db();
try
{
    await db.GetService<IMigrator>().MigrateAsync("20260924164940_AgregarDisponibilidadSemanalProducto");
    await db.Database.ExecuteSqlRawAsync("""
        INSERT INTO "CategoriasProducto" ("Id","Nombre","FechaCreacion") VALUES
            (101,'Pan',now()),(102,'Cracker',now()),(103,'Prepizza',now()),(104,'Otros',now());
        INSERT INTO "Formatos" ("Id","Descripcion") VALUES (101,'Molde'),(102,'Campo');
        INSERT INTO "Insumos" ("Id","Nombre","UnidadBase","PrecioCompra","CantidadRendimiento","TipoInsumo","Activo","StockActual","EsBolsaPapel","FechaCreacion") VALUES
            (101,'Bolsa Papel Kraft Delivery N°7',2,20,1,1,true,100,true,now()),
            (102,'Bolsa Crackers 15x25',2,30,1,1,true,100,false,now()),
            (103,'Bolsa Prepizza 35x45',2,40,1,1,true,100,false,now()),
            (104,'Etiqueta',2,5,1,2,true,100,false,now()),
            (105,'Harina',0,2000,1000,0,true,10000,false,now()),
            (106,'Agua',0,0,1000,0,true,10000,false,now());
        INSERT INTO "Productos" ("Id","IdCategoria","IdFormato","Nombre","PrecioFinal","PrecioReventa","Stock","FechaCreacion","PorEncargo") VALUES
            (101,101,101,'Pan molde',100,80,0,now(),true),
            (102,101,102,'Pan campo',100,80,0,now(),true),
            (103,102,NULL,'Crackers',100,80,0,now(),true),
            (104,103,NULL,'Prepizza',100,80,0,now(),true),
            (105,104,NULL,'Otro',100,80,0,now(),true);
        """);
    await db.Database.MigrateAsync();
    var products = await db.Productos.OrderBy(p => p.Id).ToListAsync();
    Check(products.Single(p => p.Id == 101).IdEmpaquePredeterminado == 101 && !products.Single(p => p.Id == 101).EtiquetaPredeterminada, "Migración: pan con papel sin etiqueta");
    Check(products.Single(p => p.Id == 103).IdEmpaquePredeterminado == 102 && products.Single(p => p.Id == 103).EtiquetaPredeterminada, "Migración: crackers 15x25 con etiqueta");
    Check(products.Single(p => p.Id == 104).IdEmpaquePredeterminado == 103 && products.Single(p => p.Id == 104).EtiquetaPredeterminada, "Migración: prepizza con bolsa y etiqueta");
    Check(products.Single(p => p.Id == 105).IdEmpaquePredeterminado == null, "Otros conservan empaque vacío");
    var client = new Cliente { Nombre = "Prueba", FechaCreacion = now };
    db.Clientes.Add(client); await db.SaveChangesAsync();
    var service = new SubRecetaService(db);
    var madre = new SubReceta { Nombre = "Masa madre", Detalles = [new() { IdInsumo = 105, PorcentajePanadero = 100 }, new() { IdInsumo = 106, PorcentajePanadero = 100 }] };
    await service.CreateAsync(madre);
    var basePan = new SubReceta { Nombre = "Base con masa madre", MargenSeguridad = 0.1m, Detalles = [new() { IdInsumo = 105, PorcentajePanadero = 100 }, new() { IdSubRecetaIngrediente = madre.Id, PorcentajePanadero = 100 }] };
    await service.CreateAsync(basePan);
    var tercera = new SubReceta { Nombre = "Tercer nivel", Detalles = [new() { IdSubRecetaIngrediente = basePan.Id, PorcentajePanadero = 100 }] };
    await service.CreateAsync(tercera);
    db.ChangeTracker.Clear();
    var loaded = (await service.GetByIdAsync(tercera.Id))!;
    Check(loaded.CalcularCosto(100, 1) == 150, "Costo recursivo de tres niveles: 100 g cuestan 150");
    var desglose = loaded.Desglosar(100, 1);
    Check(desglose.Where(x => x.Insumo.Id == 105).Sum(x => x.Cantidad) == 75 && desglose.Where(x => x.Insumo.Id == 106).Sum(x => x.Cantidad) == 25, "Desglose recursivo: 75 g harina y 25 g agua");
    await Reject(() => service.UpdateAsync(new() { Id = madre.Id, Nombre = madre.Nombre, Detalles = [new() { IdSubRecetaIngrediente = madre.Id, PorcentajePanadero = 100 }] }), "Rechaza autoreferencia");
    await Reject(() => service.UpdateAsync(new() { Id = madre.Id, Nombre = madre.Nombre, Detalles = [new() { IdSubRecetaIngrediente = tercera.Id, PorcentajePanadero = 100 }] }), "Rechaza ciclo indirecto de tres niveles");
    await Reject(() => service.CreateAsync(new() { Nombre = "Inválida", Detalles = [new() { IdInsumo = 105, IdSubRecetaIngrediente = madre.Id, PorcentajePanadero = 100 }] }), "Rechaza insumo y subreceta simultáneos");
    await Reject(() => service.CreateAsync(new() { Nombre = "Inválida", Detalles = [new() { IdSubRecetaIngrediente = 99999, PorcentajePanadero = 100 }] }), "Rechaza referencias inexistentes");
    await Reject(() => service.CreateAsync(new() { Nombre = "Inválida", Detalles = [new() { IdInsumo = 105, PorcentajePanadero = -1 }] }), "Rechaza cantidades negativas");
    await Reject(() => service.DeleteAsync(madre.Id), "No elimina subrecetas utilizadas");
    Check(await db.SubRecetaDetalles.CountAsync(d => d.IdSubReceta == madre.Id) == 2, "Rechazos conservan ingredientes originales");
    await service.UpdateAsync(new() { Id = tercera.Id, Nombre = "Tercer nivel editado", Detalles = [new() { IdSubRecetaIngrediente = basePan.Id, PorcentajePanadero = 100 }] });
    db.ChangeTracker.Clear();
    Check((await service.GetByIdAsync(tercera.Id))!.CalcularCosto(100, 1) == 150, "Editar guarda y recarga la subreceta anidada");
    var receta = new Receta { IdProducto = 101, TamanioLote = 1, PesoUnitario = 100, FechaCreacion = now, Detalles = [new() { IdSubReceta = tercera.Id, PorcentajePanadero = 100 }] };
    await new RecetaService(db).UpsertAsync(receta);
    var pedidoService = new PedidoService(db);
    Check((await pedidoService.GetPreciosCostoAsync([101]))[101] == 150, "Precio de costo utiliza todos los niveles");
    Check((await new RecetaService(db).GetByProductoIdAsync(101))!.CostoTotal == 150, "Ficha de receta calcula costo anidado");
    Pedido Pedido(params DetallePedido[] detalles) => new() { IdCliente = client.Id, FechaCreacion = now,
        FechaEntrega = DateTime.SpecifyKind(CalendarioCaja.Hoy.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
        Estado = EstadoPedido.Pendiente, Detalles = detalles.ToList(), MontoTotal = detalles.Sum(d => d.PrecioUnitario * d.Cantidad) };
    var tienda = Pedido(new DetallePedido { IdProducto = 101, Cantidad = 3, PrecioUnitario = 100 }, new() { IdProducto = 103, Cantidad = 2, PrecioUnitario = 100 }, new() { IdProducto = 104, Cantidad = 1, PrecioUnitario = 100 });
    await pedidoService.CrearOAmpliarDesdeTiendaAsync(tienda);
    Check(tienda.Detalles.Single(d => d.IdProducto == 101).CostoEmpaque == 20 && !tienda.Detalles.Single(d => d.IdProducto == 101).LlevaEtiqueta, "Tienda aplica papel sin etiqueta antes de calcular costo");
    Check(tienda.Detalles.Single(d => d.IdProducto == 103).IdEmpaque == 102 && tienda.Detalles.Single(d => d.IdProducto == 103).CostoEmpaque == 35, "Tienda aplica crackers y etiqueta");
    Check(tienda.Detalles.Single(d => d.IdProducto == 104).IdEmpaque == 103 && tienda.Detalles.Single(d => d.IdProducto == 104).CostoEmpaque == 45, "Tienda aplica prepizza y etiqueta");
    var admin = Pedido(new DetallePedido { IdProducto = 102, Cantidad = 4, PrecioUnitario = 100, IdEmpaque = 102, LlevaEtiqueta = true });
    await pedidoService.CreateAsync(admin);
    Check(admin.Detalles.Single().IdEmpaque == 102 && admin.Detalles.Single().LlevaEtiqueta, "Admin conserva la bolsa y etiqueta elegidas manualmente");
    var sinEmpaque = Pedido(new DetallePedido { IdProducto = 103, Cantidad = 1, PrecioUnitario = 100 });
    await pedidoService.CreateAsync(sinEmpaque);
    Check(sinEmpaque.Detalles.Single().IdEmpaque == null && !sinEmpaque.Detalles.Single().LlevaEtiqueta, "Admin puede quitar empaque y etiqueta");
    var resumen = await pedidoService.GetResumenProduccionAsync();
    Check(resumen.PorProducto.Where(p => p.Formato == "Molde").Sum(p => p.CantidadTotal) == 3 && resumen.PorProducto.Where(p => p.Formato == "Campo").Sum(p => p.CantidadTotal) == 4, "Totales de moldes y campos");
    var filtrado = await pedidoService.GetResumenProduccionAsync([101]);
    Check(filtrado.PorProducto.All(p => p.Formato != "Molde"), "Exclusiones se reflejan en formatos");
    var srResumen = resumen.PorSubReceta.Single();
    Check(srResumen.Ingredientes.Where(i => i.NombreInsumo.EndsWith("Harina")).Sum(i => i.Cantidad) == 225, "Producción desglosa harina de todos los niveles");
    await pedidoService.ConfirmarProduccionAsync([new() { IdProducto = 101, IdReceta = receta.Id, NombreProducto = "Pan molde", CantidadAProducir = 3, Seleccionado = true }]);
    db.ChangeTracker.Clear();
    Check((await db.Insumos.FindAsync(105))!.StockActual == 9775 && (await db.Insumos.FindAsync(106))!.StockActual == 9925, "Producción descuenta insumos anidados una sola vez");
    // El resumen también debe descontar unidades producidas de pedidos ampliados.
    await db.DetallesPedido.Where(d => d.IdProducto == 101).ExecuteUpdateAsync(s => s.SetProperty(d => d.CantidadProducida, d => d.Cantidad));
    Check((await pedidoService.GetResumenProduccionAsync()).PorProducto.All(p => p.Formato != "Molde"), "Unidades registradas como producidas no cuentan en formatos");
    await CostoManualChecks.RunAsync(db, Check);
    Console.WriteLine($"{checks} comprobaciones aprobadas.");
    if (args.Contains("--preview")) await Preview.RunAsync(connection);
}
finally { await db.Database.EnsureDeletedAsync(); }
