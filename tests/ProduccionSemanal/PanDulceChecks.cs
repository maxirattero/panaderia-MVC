using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.MVC.Controllers;
using Panaderia.MVC.Models;
using Panaderia.Services.Implementations;

static class PanDulceChecks
{
    public static async Task RunAsync(int port, Action<bool, string> check)
    {
        // Caso que fallaba al abrir el planificador: panes dulces sin tipo de masa.
        // Solo tablas temporales locales, en otra conexión para aislar el resto de pruebas.
        await using var connection = new NpgsqlConnection($"Host=127.0.0.1;Port={port};Username=postgres;Database=postgres");
        await connection.OpenAsync();
        await new NpgsqlCommand("SET search_path TO pg_temp", connection).ExecuteNonQueryAsync();
        await using var db = new PanaderiaContext(new DbContextOptionsBuilder<PanaderiaContext>().UseNpgsql(connection).Options);
        var schema = db.Database.GenerateCreateScript().Replace("CREATE TABLE ", "CREATE TEMP TABLE ");
        if (schema.Contains("public.") || schema.Contains("CREATE SCHEMA")) throw new Exception("Esquema no aislado");
        await new NpgsqlCommand(schema, connection).ExecuteNonQueryAsync();
        var harina = new Insumo { Nombre = "Harina", StockActual = 10000, CantidadRendimiento = 1000 };
        var pasas = new Insumo { Nombre = "Pasas", StockActual = 1000, CantidadRendimiento = 1000 };
        var categoria = new CategoriaProducto { Nombre = "Panes dulces" };
        var clasico = new Producto { Nombre = "Pan dulce clásico", Categoria = categoria, Masa = null, Variedad = null,
            ObservacionesElaboracion = "Usar molde de pan dulce" };
        var chocolate = new Producto { Nombre = "Pan dulce chocolate", Categoria = categoria, Masa = null, Variedad = null };
        var pan = new Producto { Nombre = "Pan integral", Categoria = new() { Nombre = "Panes" }, Masa = Masa.Integral };
        db.AddRange(new Receta { Producto = clasico, PesoUnitario = 500, TamanioLote = 2, Detalles = [
            new() { Insumo = harina, PorcentajePanadero = 100 }, new() { Insumo = pasas, CantidadFija = 50 }] },
            new Receta { Producto = chocolate, PesoUnitario = 400, TamanioLote = 1, Detalles = [new() { Insumo = harina, PorcentajePanadero = 100 }] },
            new Receta { Producto = pan, PesoUnitario = 100, TamanioLote = 1, Detalles = [new() { Insumo = harina, PorcentajePanadero = 100 }] });
        await db.SaveChangesAsync();
        var servicio = new PedidoService(db);
        await servicio.AgregarProduccionStockAsync(clasico.Id, 4);
        await servicio.AgregarProduccionStockAsync(chocolate.Id, 2);
        await servicio.AgregarProduccionStockAsync(pan.Id, 1);
        var controller = new PedidoController(servicio, null!, null!, new RecetaService(db), null!) {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };
        var plan = (ViewResult)await controller.PlanificarAmasadas();
        var productos = JsonSerializer.Deserialize<List<ProduccionProductoDetalle>>((string)controller.ViewBag.ProductosJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        check(productos.Count == 3, "Planificador abre con panes dulces sin tipo de masa y un pan habitual");
        var dulce = productos.Single(p => p.IdProducto == clasico.Id);
        var otro = productos.Single(p => p.IdProducto == chocolate.Id);
        check(!string.IsNullOrWhiteSpace(dulce.MasaKey) && dulce.MasaKey != otro.MasaKey,
            "Panes dulces sin masa definida mantienen grupos separados por producto");
        check(dulce.NombreMasa == clasico.NombreVisible && otro.NombreMasa == chocolate.NombreVisible,
            "El grupo de amasado sin tipo usa el nombre del producto");
        check(dulce.CantidadUnidades == 4 && dulce.PesoMasaTotal == 2000
            && dulce.Ingredientes.Single(i => i.IdInsumo == harina.Id).Gramos == 2000
            && dulce.Ingredientes.Single(i => i.IdInsumo == pasas.Id).Gramos == 200,
            "Plan de stock conserva unidades, peso e ingredientes del pan dulce");
        var habitual = productos.Single(p => p.IdProducto == pan.Id);
        check(habitual.MasaKey == $"{(int)Masa.Integral}-" && habitual.NombreMasa == "Integral",
            "Los panes con tipo de masa conservan su agrupación habitual");
        check(dulce.ObservacionesElaboracion == clasico.ObservacionesElaboracion,
            "El planificador conserva las indicaciones del pan dulce");
        var vm = (ProduccionViewModel)plan.Model!;
        check(vm.ItemsSeleccionables.Count == 3 && vm.ItemsSeleccionables.All(i => i.EsStock && i.Revision.Length > 0)
            && clasico.Stock == 0 && harina.StockActual == 10000,
            "Abrir el plan no confirma producción ni descuenta ingredientes");
        var confirmacion = (JsonResult)await controller.ConfirmarPlanificacion(vm);
        check(JsonSerializer.SerializeToElement(confirmacion.Value).GetProperty("success").GetBoolean()
            && clasico.Stock == 4 && chocolate.Stock == 2 && pan.Stock == 1
            && harina.StockActual == 7100 && pasas.StockActual == 800,
            "Confirmar el plan produce stock de pan dulce y descuenta ingredientes una sola vez");
        var reintento = (JsonResult)await controller.ConfirmarPlanificacion(vm);
        check(!JsonSerializer.SerializeToElement(reintento.Value).GetProperty("success").GetBoolean()
            && clasico.Stock == 4 && harina.StockActual == 7100,
            "Reintentar el plan no duplica stock ni consumo de pan dulce");
        check((await servicio.GetProduccionStockAsync()).Count == 0,
            "La producción confirmada deja de figurar como stock pendiente");
    }
}
