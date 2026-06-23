using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Panaderia.Models.Entities;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers;

public class CompraController : Controller
{
    private readonly ICompraService _compraService;
    private readonly IProveedorService _proveedorService;
    private readonly IInsumoService _insumoService;

    public CompraController(ICompraService compraService, IProveedorService proveedorService, IInsumoService insumoService)
    {
        _compraService    = compraService;
        _proveedorService = proveedorService;
        _insumoService    = insumoService;
    }

    public async Task<IActionResult> Index()
    {
        var compras = await _compraService.GetAllAsync();
        return View(compras);
    }

    public async Task<IActionResult> Create()
    {
        await CargarDropdowns();
        var vm = new CompraViewModel { Fecha = DateTime.Today };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompraViewModel vm)
    {
        foreach (var key in ModelState.Keys
            .Where(k => k.StartsWith("Detalles[") || k.StartsWith("Proveedor")).ToList())
            ModelState.Remove(key);

        if (!ModelState.IsValid)
        {
            await CargarDropdowns();
            return View(vm);
        }

        var compra = new CompraProveedor
        {
            IdProveedor   = vm.IdProveedor,
            Fecha         = vm.Fecha,
            NumeroFactura = vm.NumeroFactura,
            Notas         = vm.Notas,
            Detalles      = vm.Detalles.Select(d => new CompraDetalle
            {
                IdInsumo       = d.IdInsumo,
                IdUnidadCompra = d.IdUnidadCompra,
                Cantidad       = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario
            }).ToList()
        };

        await _compraService.CreateAsync(compra);
        TempData["Success"] = "Compra registrada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var compra = await _compraService.GetByIdAsync(id);
        if (compra == null) return NotFound();
        return View(compra);
    }

    private async Task CargarDropdowns()
    {
        ViewBag.Proveedores = new SelectList(await _proveedorService.GetAllAsync(), "Id", "Nombre");
        ViewBag.Insumos = (await _insumoService.GetAllAsync()).Where(i => i.Activo).ToList();
    }
}
