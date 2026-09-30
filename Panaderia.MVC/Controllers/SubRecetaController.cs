using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers;

public class SubRecetaController : Controller
{
    private readonly ISubRecetaService _subRecetaService;
    private readonly IInsumoService    _insumoService;

    public SubRecetaController(ISubRecetaService subRecetaService, IInsumoService insumoService)
    {
        _subRecetaService = subRecetaService;
        _insumoService    = insumoService;
    }

    private async Task CargarDropdowns(int? idActual = null)
    {
        // Los consumibles (film, limpieza, guantes...) no entran en una sub-receta.
        // Empaque y Etiqueta siguen listándose como hasta ahora para no cambiar sub-recetas ya cargadas.
        ViewBag.InsumosLista = (await _insumoService.GetAllAsync())
            .Where(i => i.Activo && i.TipoInsumo != TipoInsumo.Consumible)
            .ToList();
        ViewBag.SubRecetasLista = (await _subRecetaService.GetAllAsync()).Where(s => s.Id != idActual).ToList();
    }

    public async Task<IActionResult> Index()
    {
        var subRecetas = await _subRecetaService.GetAllAsync();
        return View(subRecetas);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await CargarDropdowns();
        return View(new SubReceta());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubReceta subReceta)
    {
        if (!ModelState.IsValid)
        {
            await CargarDropdowns();
            return View(subReceta);
        }

        try { await _subRecetaService.CreateAsync(subReceta); }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await CargarDropdowns();
            return View(subReceta);
        }
        TempData["Success"] = "Sub-receta creada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var subReceta = await _subRecetaService.GetByIdAsync(id);
        if (subReceta == null) return NotFound();
        await CargarDropdowns(id);
        return View(subReceta);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SubReceta subReceta)
    {
        if (id != subReceta.Id) return NotFound();

        if (!ModelState.IsValid)
        {
            await CargarDropdowns(id);
            return View(subReceta);
        }

        try { await _subRecetaService.UpdateAsync(subReceta); }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await CargarDropdowns(id);
            return View(subReceta);
        }
        TempData["Success"] = "Sub-receta actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try { await _subRecetaService.DeleteAsync(id); }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        TempData["Success"] = "Sub-receta eliminada correctamente.";
        return RedirectToAction(nameof(Index));
    }
}
