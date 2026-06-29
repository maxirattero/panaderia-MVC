using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers;

public class RecetaController : Controller
{
    private readonly IRecetaService    _recetaService;
    private readonly IProductoService  _productoService;
    private readonly IInsumoService    _insumoService;
    private readonly ISubRecetaService _subRecetaService;

    public RecetaController(
        IRecetaService recetaService,
        IProductoService productoService,
        IInsumoService insumoService,
        ISubRecetaService subRecetaService)
    {
        _recetaService    = recetaService;
        _productoService  = productoService;
        _insumoService    = insumoService;
        _subRecetaService = subRecetaService;
    }

    [HttpGet]
    public async Task<IActionResult> ImprimirReceta(int idProducto, decimal cantidad)
    {
        var receta = await _recetaService.GetByProductoIdAsync(idProducto);
        if (receta == null) return NotFound();
        return View(new ImprimirRecetaViewModel { Receta = receta, CantidadUnidades = cantidad });
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int idProducto)
    {
        var producto = await _productoService.GetByIdAsync(idProducto);
        if (producto == null) return NotFound();

        var receta = await _recetaService.GetByProductoIdAsync(idProducto)
                     ?? new Receta { IdProducto = idProducto, TamanioLote = 1, Detalles = new() };

        ViewBag.NombreProducto = producto.NombreVisible;
        await CargarDropdowns();
        return View(receta);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int idProducto, Receta receta)
    {
        ModelState.Remove(nameof(receta.Producto));

        foreach (var key in ModelState.Keys
            .Where(k => k.Contains(".Receta") || k.Contains(".Insumo") || k.Contains(".SubReceta")).ToList())
            ModelState.Remove(key);

        if (!ModelState.IsValid)
        {
            var prod = await _productoService.GetByIdAsync(idProducto);
            ViewBag.NombreProducto = prod?.NombreVisible ?? "";
            await CargarDropdowns();
            return View(receta);
        }

        receta.IdProducto = idProducto;
        await _recetaService.UpsertAsync(receta);
        return RedirectToAction("Index", "Producto");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _recetaService.DeleteAsync(id);
        return RedirectToAction("Index", "Producto");
    }

    private async Task CargarDropdowns()
    {
        var insumos = (await _insumoService.GetAllAsync())
            .Where(i => i.Activo && i.TipoInsumo == TipoInsumo.Ingrediente)
            .ToList();
        ViewBag.InsumosLista    = insumos;
        ViewBag.SubRecetasLista = await _subRecetaService.GetAllAsync();
    }
}
