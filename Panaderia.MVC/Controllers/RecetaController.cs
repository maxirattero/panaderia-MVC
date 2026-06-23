using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers;

public class RecetaController : Controller
{
    private readonly IRecetaService _recetaService;
    private readonly IProductoService _productoService;
    private readonly IInsumoService _insumoService;

    public RecetaController(
        IRecetaService recetaService,
        IProductoService productoService,
        IInsumoService insumoService)
    {
        _recetaService = recetaService;
        _productoService = productoService;
        _insumoService = insumoService;
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int idProducto)
    {
        var producto = await _productoService.GetByIdAsync(idProducto);
        if (producto == null) return NotFound();

        var receta = await _recetaService.GetByProductoIdAsync(idProducto)
                     ?? new Receta { IdProducto = idProducto, TamanioLote = 1, Detalles = new() };

        ViewBag.NombreProducto = producto.Nombre;
        await CargarDropdowns();
        return View(receta);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int idProducto, Receta receta)
    {
        ModelState.Remove(nameof(receta.Producto));

        foreach (var key in ModelState.Keys
            .Where(k => k.Contains(".Receta") || k.Contains(".Insumo")).ToList())
            ModelState.Remove(key);

        if (!ModelState.IsValid)
        {
            var prod = await _productoService.GetByIdAsync(idProducto);
            ViewBag.NombreProducto = prod?.Nombre ?? "";
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
            .Where(i => i.Activo)
            .ToList();
        ViewBag.InsumosLista = insumos;
    }
}
