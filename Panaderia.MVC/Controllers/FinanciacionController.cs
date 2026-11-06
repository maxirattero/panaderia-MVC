using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Panaderia.MVC.Models;
using Panaderia.Services.Implementations;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers;

public class FinanciacionController(IFinanciacionService financiacion, ICompraService compras, IProductoService productos) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Compras = (await compras.GetAllAsync()).Where(x => x.Aportes.Count > 0).ToList();
        ViewBag.Productos = (await productos.GetAllAsync()).OrderBy(x => x.NombreVisible).ToList();
        return View(await financiacion.ResumenAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearLote(LoteFinanciadoViewModel vm)
    {
        if (!ModelState.IsValid) TempData["Error"] = "Revisá los datos del lote.";
        else try
        {
            await financiacion.CrearLoteAsync(new LoteFinanciado { Nombre = vm.Nombre, IdProducto = vm.IdProducto,
                Unidades = vm.Unidades, DesdeUtc = CalendarioCaja.InicioUtc(DateOnly.FromDateTime(vm.Desde!.Value)), ClaveOperacion = vm.Clave });
            TempData["Success"] = "Lote creado. Asignale los costos de las compras antes de cerrar la semana.";
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Asignar(int compra, int lote, decimal monto, Guid clave)
    {
        if (!ModelState.IsValid) TempData["Error"] = "Revisá el importe de la asignación.";
        else try { await financiacion.AsignarAsync(compra, lote, monto, clave); TempData["Success"] = "Costo asignado al lote."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pagar(PagoReintegroViewModel vm)
    {
        if (!ModelState.IsValid) TempData["Error"] = "Revisá los datos del pago.";
        else try { await financiacion.PagarAsync(vm.Aporte, vm.Reserva, vm.Cuenta, vm.Monto, vm.Clave, User.Identity?.Name); TempData["Success"] = "Pago confirmado y deuda actualizada."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
