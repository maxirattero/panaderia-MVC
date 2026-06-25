using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
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

    private async Task CargarDropdowns()
    {
        ViewBag.InsumosLista = (await _insumoService.GetAllAsync()).Where(i => i.Activo).ToList();
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
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Detalles[")).ToList())
            ModelState.Remove(key);

        if (!ModelState.IsValid)
        {
            await CargarDropdowns();
            return View(subReceta);
        }

        await _subRecetaService.CreateAsync(subReceta);
        TempData["Success"] = "Sub-receta creada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var subReceta = await _subRecetaService.GetByIdAsync(id);
        if (subReceta == null) return NotFound();
        await CargarDropdowns();
        return View(subReceta);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SubReceta subReceta)
    {
        if (id != subReceta.Id) return NotFound();

        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Detalles[")).ToList())
            ModelState.Remove(key);

        if (!ModelState.IsValid)
        {
            await CargarDropdowns();
            return View(subReceta);
        }

        await _subRecetaService.UpdateAsync(subReceta);
        TempData["Success"] = "Sub-receta actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _subRecetaService.DeleteAsync(id);
        TempData["Success"] = "Sub-receta eliminada correctamente.";
        return RedirectToAction(nameof(Index));
    }
}
