using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.MVC.Controllers;
using Panaderia.MVC.Models;
using Panaderia.Services.Implementations;

static class ConfirmacionChecks
{
    public static async Task RunAsync(PanaderiaContext db, PedidoService service, TimeProvider clock, DateTime monday,
        Producto bread, Receta recipe, Cliente customer, Insumo water, Action<bool, string> check)
    {
        var other = new Producto { Nombre = "Otro pan", Categoria = bread.Categoria, Masa = bread.Masa };
        var otherRecipe = new Receta { Producto = other, TamanioLote = 1, PesoUnitario = 100,
            Detalles = [new() { Insumo = water, PorcentajePanadero = 100 }] };
        var mixed = new Pedido { Cliente = customer, FechaEntrega = monday.AddDays(5), Detalles = [
            new() { Producto = bread, Cantidad = 4 }, new() { Producto = other, Cantidad = 3 }] };
        db.AddRange(otherRecipe, mixed);
        await db.SaveChangesAsync();
        PedidoController Controller(string? excluded = null) => new PedidoController(service, null!, null!, new RecetaService(db), null!)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        }.WithExclusions(excluded);
        async Task<ProduccionViewModel> Plan(PedidoController controller) =>
            (ProduccionViewModel)((ViewResult)await controller.PlanificarAmasadas()).Model!;
        static bool Success(IActionResult result) => JsonSerializer.SerializeToElement(((JsonResult)result).Value).GetProperty("success").GetBoolean();

        // El mismo plan debe poder confirmarse con exclusiones, sin tocar otros productos del pedido mixto.
        var controller = Controller(other.Id.ToString());
        var plan = await Plan(controller);
        check(plan.ItemsSeleccionables.Count == 2 && plan.ItemsSeleccionables.All(i => i.IdProducto == bread.Id), "Planificador solo ofrece los productos seleccionados (pedidos y stock)");
        var stockBefore = water.StockActual;
        check(Success(await controller.ConfirmarPlanificacion(plan)), "Confirmar desde el planificador permite productos excluidos");
        check(water.StockActual == stockBefore - 250 && bread.Stock == 1, "Confirmación seleccionada descuenta pedidos y stock una sola vez");
        check(mixed.Estado == EstadoPedido.Pendiente && mixed.Detalles.Single(d => d.IdProducto == bread.Id).CantidadProducida == 4
            && mixed.Detalles.Single(d => d.IdProducto == other.Id).CantidadProducida == 0, "Pedido mixto conserva pendiente únicamente el producto sin confirmar");
        var remaining = await service.GetResumenProduccionAsync();
        check(remaining.PorProducto.Count == 1 && remaining.PorProducto[0].IdProducto == other.Id
            && !(await service.GetProduccionStockAsync()).Any(), "Productos confirmados desaparecen de listado, planificación y stock pendiente");
        var stockAfter = water.StockActual;
        check(!Success(await controller.ConfirmarPlanificacion(plan)) && water.StockActual == stockAfter && bread.Stock == 1,
            "Reenviar el mismo plan no vuelve a descontar insumos ni sumar stock");

        // Una pestaña vieja no confirma productos que el usuario acaba de destildar.
        var otherController = Controller();
        var otherPlan = await Plan(otherController);
        otherController.Request.Headers.Cookie = "mv_prod_excluidos=" + other.Id;
        check(!Success(await otherController.ConfirmarPlanificacion(otherPlan)) && water.StockActual == stockAfter,
            "Cambio de selección entre abrir e imprimir rechaza la confirmación");
        otherController.Request.Headers.Cookie = "";
        var item = otherPlan.ItemsSeleccionables.Single();
        item.CantidadAProducir = 1;
        check(Success(await otherController.ConfirmarPlanificacion(otherPlan)), "Se puede confirmar una cantidad parcial");
        check((await service.GetResumenProduccionAsync()).PorProducto.Single().CantidadTotal == 2,
            "La producción parcial muestra solo las unidades restantes");
        stockAfter = water.StockActual;
        check(!Success(await otherController.ConfirmarPlanificacion(otherPlan)) && water.StockActual == stockAfter,
            "Reintentar una cantidad parcial con el mismo formulario no vuelve a descontar");
        otherPlan = await Plan(otherController);
        check(Success(await otherController.ConfirmarPlanificacion(otherPlan)) && mixed.Estado == EstadoPedido.EnProduccion,
            "Al completar el último producto, el pedido pasa a EnProduccion");

        await service.AgregarProduccionStockAsync(other.Id, 4);
        var stockPlan = await Plan(otherController);
        stockPlan.ItemsSeleccionables.Single().CantidadAProducir = 2;
        check(Success(await otherController.ConfirmarPlanificacion(stockPlan)) && other.Stock == 2
            && (await service.GetProduccionStockAsync()).Single().Cantidad == 2, "Stock parcial conserva las unidades aún pendientes");
        stockAfter = water.StockActual;
        check(!Success(await otherController.ConfirmarPlanificacion(stockPlan)) && water.StockActual == stockAfter && other.Stock == 2,
            "Stock parcial también queda protegido ante reenvíos");
        var freshPlan = await Plan(otherController);
        var freshItem = freshPlan.ItemsSeleccionables.Single();
        foreach (var invalid in new[] { -1m, 0m, 0.5m, 3m })
        {
            freshItem.CantidadAProducir = invalid;
            check(!Success(await otherController.ConfirmarPlanificacion(freshPlan)) && water.StockActual == stockAfter,
                "Rechaza cantidad inválida sin tocar insumos: " + invalid);
        }
        freshItem.CantidadAProducir = 2;
        freshItem.IdReceta = recipe.Id;
        check(!Success(await otherController.ConfirmarPlanificacion(freshPlan)) && water.StockActual == stockAfter,
            "No permite usar la receta de otro producto");
        freshItem.IdReceta = otherRecipe.Id;
        freshPlan.ItemsSeleccionables.Add(freshItem);
        check(!Success(await otherController.ConfirmarPlanificacion(freshPlan)) && water.StockActual == stockAfter,
            "No duplica descuentos si se repite un producto en la solicitud");
        freshPlan.ItemsSeleccionables.RemoveAt(1);
        water.StockActual = 1;
        await db.SaveChangesAsync();
        check(!Success(await otherController.ConfirmarPlanificacion(freshPlan)) && water.StockActual == 1
            && (await service.GetProduccionStockAsync()).Single().Cantidad == 2, "Falta de insumos no confirma ni consume parcialmente");
        water.StockActual = 100000;
        await db.SaveChangesAsync();
        check(Success(await otherController.ConfirmarPlanificacion(freshPlan)), "El mismo plan puede reintentarse después de reponer insumos");

        // La revisión detecta pedidos nuevos, incluso si otra operación deja el mismo total pendiente.
        var newOrder = new Pedido { Cliente = customer, FechaEntrega = monday.AddDays(5),
            Detalles = [new() { Producto = other, Cantidad = 2 }] };
        db.Add(newOrder);
        await db.SaveChangesAsync();
        var stalePlan = await Plan(otherController);
        newOrder.Anulado = true;
        db.Add(new Pedido { Cliente = customer, FechaEntrega = monday.AddDays(5), Detalles = [new() { Producto = other, Cantidad = 2 }] });
        await db.SaveChangesAsync();
        stockAfter = water.StockActual;
        check(!Success(await otherController.ConfirmarPlanificacion(stalePlan)) && water.StockActual == stockAfter,
            "Un plan viejo no confirma pedidos nuevos con la misma cantidad total");
        var invalidModel = Controller();
        invalidModel.ModelState.AddModelError("CantidadAProducir", "Inválida");
        check(!Success(await invalidModel.ConfirmarPlanificacion(await Plan(otherController))), "Controller rechaza formularios inválidos");
        var missingRevision = await Plan(otherController);
        missingRevision.ItemsSeleccionables.Single().Revision = "";
        check(!Success(await otherController.ConfirmarPlanificacion(missingRevision)), "Un formulario anterior al cambio debe recargarse antes de confirmar");
    }

    private static PedidoController WithExclusions(this PedidoController controller, string? excluded)
    {
        if (excluded != null) controller.Request.Headers.Cookie = "mv_prod_excluidos=" + excluded;
        return controller;
    }
}
