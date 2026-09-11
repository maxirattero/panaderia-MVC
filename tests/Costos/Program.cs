using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;

// PostgreSQL real, exclusivamente tablas temporales de esta conexión.
// search_path excluye public: ningún dato de la panadería se lee o modifica.
var config = new ConfigurationBuilder().AddUserSecrets<Panaderia.MVC.Controllers.PedidoController>().AddEnvironmentVariables().Build();
await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("COSTOS_TEST_CONNECTION") ?? config.GetConnectionString("DefaultConnection"));
await connection.OpenAsync();
await new NpgsqlCommand("SET search_path TO pg_temp", connection).ExecuteNonQueryAsync();
await using var db = new PanaderiaContext(new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options);
var schema = db.Database.GenerateCreateScript().Replace("CREATE TABLE ", "CREATE TEMP TABLE ");
if (schema.Contains("public.") || schema.Contains("CREATE SCHEMA")) throw new Exception("Esquema no aislado");
await new NpgsqlCommand(schema, connection).ExecuteNonQueryAsync();
var hoy = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc);
var harina = new Insumo { Nombre = "Harina", PrecioCompra = 2000, CantidadRendimiento = 1000, FechaCreacion = hoy };
var bolsa = new Insumo { Nombre = "Bolsa", TipoInsumo = TipoInsumo.Empaque, PrecioCompra = 100, CantidadRendimiento = 10, FechaCreacion = hoy };
var etiqueta = new Insumo { Nombre = "Etiqueta", TipoInsumo = TipoInsumo.Etiqueta, PrecioCompra = 50, CantidadRendimiento = 10, FechaCreacion = hoy };
var sr = new SubReceta { Nombre = "Masa madre", Detalles = [new() { Insumo = harina, PorcentajePanadero = 100 }] };
var producto = new Producto { Nombre = "Pan", Categoria = new() { Nombre = "Panes" }, PorEncargo = true, PrecioFinal = 5000, PrecioReventa = 4000 };
var receta = new Receta { Producto = producto, PesoUnitario = 100, TamanioLote = 1, FechaCreacion = hoy,
    Detalles = [new() { Insumo = harina, PorcentajePanadero = 50 }, new() { SubReceta = sr, PorcentajePanadero = 50 },
        new() { Insumo = bolsa, CantidadFija = 1 }] };
var cliente = new Cliente { Nombre = "Costo", PrecioDeCosto = true, DescuentoPorcentaje = 50, FechaCreacion = hoy };
db.AddRange(receta, bolsa, etiqueta, cliente);
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
var service = new PedidoService(db);
var costos = await service.GetPreciosCostoAsync([producto.Id]);
Check(costos[producto.Id] == 200, "Costo incluye ingredientes de subrecetas");
Pedido Nuevo(int cantidad) => new() { IdCliente = cliente.Id, FechaCreacion = hoy, FechaEntrega = hoy, DescuentoPorcentaje = 50,
    Detalles = [new() { IdProducto = producto.Id, Cantidad = cantidad, PrecioUnitario = 5000, IdEmpaque = bolsa.Id, LlevaEtiqueta = true }] };
var pedido = Nuevo(2);
await service.CreateAsync(pedido);
Check(pedido.MontoTotal == 400 && pedido.DescuentoPorcentaje == 0 && pedido.Detalles.First().CostoEmpaque == 15, "Crear: solo ingredientes, sin descuento; conserva costo de packaging");
db.ChangeTracker.Clear();
var editado = Nuevo(3); editado.Id = pedido.Id;
await service.UpdateAsync(editado);
db.ChangeTracker.Clear();
var guardado = await db.Pedidos.Include(p => p.Detalles).SingleAsync();
Check(guardado.MontoTotal == 600 && guardado.Detalles.First().PrecioUnitario == 200 && guardado.DescuentoPorcentaje == 0, "Editar mantiene precio de costo");
guardado.Estado = EstadoPedido.Entregado;
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
var cierre = await service.GetResumenCierreSemanalAsync(hoy);
Check(cierre.CostoInsumos == 645 && cierre.DetallesCosto.Single().CostoEmpaque == 45 && cierre.DetallesCosto.Sum(d => d.CostoTotal) == cierre.CostoInsumos, "Cierre incluye subrecetas + 3 bolsas + 3 etiquetas y concilia desglose");
var normal = await db.Clientes.FindAsync(cliente.Id); normal!.PrecioDeCosto = false; await db.SaveChangesAsync();
var venta = Nuevo(1); venta.MontoTotal = 2500;
await service.CreateAsync(venta);
Check(venta.MontoTotal == 2500 && venta.Detalles.First().PrecioUnitario == 5000, "Cliente normal conserva precio y descuento");
normal.PrecioDeCosto = true; await db.SaveChangesAsync();
var ampliacion = await service.CrearOAmpliarDesdeTiendaAsync(Nuevo(1));
Check(ampliacion.Id != venta.Id && ampliacion.MontoTotal == 200,
    "Tienda: costo sin descuento, conserva separado el pedido previo con descuento");
db.Pedidos.Remove(venta); await db.SaveChangesAsync();
var otraAmpliacion = await service.CrearOAmpliarDesdeTiendaAsync(Nuevo(1));
Check(otraAmpliacion.Id == ampliacion.Id && otraAmpliacion.MontoTotal == 400,
    "Tienda: unifica pedidos a costo sin duplicar importes");
var sinReceta = new Producto { Nombre = "Sin receta", IdCategoria = producto.IdCategoria, PorEncargo = true }; db.Add(sinReceta); await db.SaveChangesAsync();
var invalido = Nuevo(1); invalido.Detalles.First().IdProducto = sinReceta.Id;
try { await service.CreateAsync(invalido); throw new Exception("Se permitió vender sin costo"); }
catch (InvalidOperationException e) when (e.Message.Contains("sin receta")) { Console.WriteLine("PASS: rechaza productos sin costo"); }
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS: " + name); }

