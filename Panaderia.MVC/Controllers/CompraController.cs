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
        var vm = new CompraViewModel { Fecha = Panaderia.Services.Implementations.CalendarioCaja.Hoy.ToDateTime(TimeOnly.MinValue) };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompraViewModel vm)
    {
        ModelState.Remove("Proveedor");

        if (vm.Detalles == null || vm.Detalles.Count == 0)
        {
            ModelState.AddModelError("Detalles", "Debe agregar al menos un ítem a la compra.");
        }
        else
        {
            for (int i = 0; i < vm.Detalles.Count; i++)
            {
                var d = vm.Detalles[i];
                if (d.IdInsumo <= 0)
                    ModelState.AddModelError($"Detalles[{i}].IdInsumo", "Debe seleccionar un insumo.");
                if (d.IdUnidadCompra <= 0)
                    ModelState.AddModelError($"Detalles[{i}].IdUnidadCompra", "Debe seleccionar una unidad de compra.");
                if (d.Cantidad <= 0)
                    ModelState.AddModelError($"Detalles[{i}].Cantidad", "La cantidad debe ser mayor a 0.");
                if (d.PrecioUnitario < 0)
                    ModelState.AddModelError($"Detalles[{i}].PrecioUnitario", "El precio no puede ser negativo.");
            }
        }

        if (!ModelState.IsValid)
        {
            await CargarDropdowns();
            return View(vm);
        }

        var compra = new CompraProveedor
        {
            IdProveedor   = vm.IdProveedor,
            Fecha         = Panaderia.Services.Implementations.CalendarioCaja.InicioUtc(DateOnly.FromDateTime(vm.Fecha)),
            CuentaPago    = vm.CuentaPago,
            DescontarDelReparto = vm.DescontarDelReparto,
            ClaveOperacion = vm.ClaveOperacion,
            NumeroFactura = vm.NumeroFactura,
            Notas         = vm.Notas,
            Detalles      = vm.Detalles.Select(d => new CompraDetalle
            {
                IdInsumo       = d.IdInsumo,
                IdUnidadCompra = d.IdUnidadCompra,
                Cantidad       = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                CostoEnvio     = d.CostoEnvio
            }).ToList()
        };

        var total = vm.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario + d.CostoEnvio);
        var masaViva = total - vm.AporteMaxi - vm.AporteAni;
        if (vm.AporteMaxi > 0) compra.Aportes.Add(new() { Origen = Panaderia.Models.Enums.OrigenFondos.Maxi, Monto = vm.AporteMaxi });
        if (vm.AporteAni > 0) compra.Aportes.Add(new() { Origen = Panaderia.Models.Enums.OrigenFondos.Ani, Monto = vm.AporteAni });
        if (masaViva > 0) compra.Aportes.Add(new() { Origen = Panaderia.Models.Enums.OrigenFondos.MasaViva, Monto = masaViva, Cuenta = vm.CuentaPago });

        try
        {
            if (masaViva < 0) throw new InvalidOperationException("Los aportes personales superan el total de la compra.");
            await _compraService.CreateAsync(compra);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await CargarDropdowns();
            return View(vm);
        }
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
