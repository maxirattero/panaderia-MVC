using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers;

public class InsumoController : Controller
{
    private readonly IInsumoService _insumoService;
    private readonly IProveedorService _proveedorService;

    public InsumoController(IInsumoService insumoService, IProveedorService proveedorService)
    {
        _insumoService = insumoService;
        _proveedorService = proveedorService;
    }

    public async Task<IActionResult> Index()
    {
        var insumos = await _insumoService.GetAllAsync();
        return View(insumos);
    }

    public async Task<IActionResult> Create()
    {
        await CargarDropdowns();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Insumo insumo)
    {
        if (!ModelState.IsValid)
        {
            await CargarDropdowns();
            return View(insumo);
        }
        insumo.FechaCreacion = DateTime.UtcNow;
        await _insumoService.CreateAsync(insumo);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var insumo = await _insumoService.GetByIdAsync(id.Value);
        if (insumo == null) return NotFound();
        await CargarDropdowns();
        return View(insumo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Insumo insumo)
    {
        if (id != insumo.Id) return NotFound();

        foreach (var key in ModelState.Keys
            .Where(k => k.StartsWith("UnidadesCompra[") || k.StartsWith("Proveedor")).ToList())
            ModelState.Remove(key);

        if (!ModelState.IsValid)
        {
            await CargarDropdowns();
            return View(insumo);
        }
        await _insumoService.UpdateAsync(insumo);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _insumoService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarDropdowns()
    {
        ViewBag.Proveedores = new SelectList(
            await _proveedorService.GetAllAsync(), "Id", "Nombre");
    }
}
