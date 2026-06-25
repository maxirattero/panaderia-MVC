using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    public class ReporteCajaController : Controller
    {
        private readonly IReporteCajaService _reporteCajaService;
        private readonly IProveedorService _proveedorService;
        private readonly IPedidoService _pedidoService;

        public ReporteCajaController(
            IReporteCajaService reporteCajaService,
            IProveedorService proveedorService,
            IPedidoService pedidoService)
        {
            _reporteCajaService = reporteCajaService;
            _proveedorService = proveedorService;
            _pedidoService = pedidoService;
        }

        private async Task CargarDropdowns(ReporteCajaFormViewModel vm)
        {
            var proveedores = await _proveedorService.GetAllAsync();
            vm.Proveedores = new SelectList(proveedores, "Id", "Nombre");
        }

        public async Task<IActionResult> Index(DateTime? fechaInicio, DateTime? fechaFin, TipoMovimiento? TipoFiltro)
        {
            var todos = await _reporteCajaService.GetReporteCajaTotalAsync();
            var movimientos = todos.AsEnumerable();

            if (fechaInicio.HasValue)
            {
                var inicioUtc = DateTime.SpecifyKind(fechaInicio.Value, DateTimeKind.Utc);
                movimientos = movimientos.Where(r => r.Fecha >= inicioUtc);
            }
            if (fechaFin.HasValue)
            {
                var finUtc = DateTime.SpecifyKind(fechaFin.Value, DateTimeKind.Utc).AddDays(1);
                movimientos = movimientos.Where(r => r.Fecha < finUtc);
            }
            if (TipoFiltro.HasValue)
            {
                movimientos = movimientos.Where(r => r.Tipo == TipoFiltro.Value);
            }

            var lista = movimientos.ToList();

            var vm = new ReporteCajaIndexViewModel
            {
                Movimientos = lista,
                Saldo = await _reporteCajaService.GetSaldoAsync(),
                TotalIngresos = lista.Where(r => r.Tipo == TipoMovimiento.Ingreso).Sum(r => r.Monto),
                TotalEgresos = lista.Where(r => r.Tipo == TipoMovimiento.Egreso).Sum(r => r.Monto),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin,
                TipoFiltro = TipoFiltro
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new ReporteCajaFormViewModel
            {
                Fecha = DateTime.Today,
                Categoria = CategoriaMovimiento.Gasto,
                Tipo = TipoMovimiento.Egreso
            };
            await CargarDropdowns(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReporteCajaFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await CargarDropdowns(vm);
                return View(vm);
            }

            var reporte = new ReporteCaja
            {
                Fecha = vm.Fecha,
                Tipo = vm.Tipo,
                Categoria = vm.Categoria,
                Monto = vm.Monto,
                Descripcion = vm.Descripcion,
                IdProveedor = vm.IdProveedor
            };

            await _reporteCajaService.CreateAsync(reporte);
            TempData["Success"] = "Movimiento registrado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var reporte = await _reporteCajaService.GetByIdAsync(id);
            if (reporte == null) return NotFound();

            var vm = new ReporteCajaFormViewModel
            {
                Id = reporte.Id,
                Fecha = reporte.Fecha,
                Tipo = reporte.Tipo,
                Categoria = reporte.Categoria,
                Monto = reporte.Monto,
                Descripcion = reporte.Descripcion,
                IdProveedor = reporte.IdProveedor,
                IdPedido = reporte.IdPedido
            };

            await CargarDropdowns(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ReporteCajaFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await CargarDropdowns(vm);
                return View(vm);
            }

            var reporte = new ReporteCaja
            {
                Id = vm.Id,
                Fecha = vm.Fecha,
                Tipo = vm.Tipo,
                Categoria = vm.Categoria,
                Monto = vm.Monto,
                Descripcion = vm.Descripcion,
                IdProveedor = vm.IdProveedor
            };

            await _reporteCajaService.UpdateAsync(reporte);
            TempData["Success"] = "Movimiento actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _reporteCajaService.DeleteAsync(id);
            TempData["Success"] = "Movimiento eliminado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> CierreSemanal()
        {
            var (totalCobrado, costoInsumos, detalles) = await _pedidoService.GetResumenCierreSemanalAsync();
            var hoy = DateTime.UtcNow.Date;
            int diasDesdeDomingo = (int)hoy.DayOfWeek;
            var inicioSemana = hoy.AddDays(-diasDesdeDomingo);
            var vm = new CierreSemanalViewModel
            {
                InicioSemana  = inicioSemana,
                FinSemana     = inicioSemana.AddDays(6),
                TotalCobrado  = totalCobrado,
                CostoInsumos  = costoInsumos,
                MontoARetirar = Math.Round(totalCobrado - costoInsumos, 2),
                DetallesCosto = detalles
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarCierre(CierreSemanalViewModel vm)
        {
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("DetallesCosto")).ToList())
                ModelState.Remove(key);

            if (vm.MontoARetirar <= 0)
            {
                TempData["Error"] = "El monto a retirar debe ser mayor a cero.";
                return RedirectToAction(nameof(CierreSemanal));
            }

            var periodoStr = $"{vm.InicioSemana:dd/MM} - {vm.FinSemana:dd/MM}";
            await _reporteCajaService.CreateAsync(new ReporteCaja
            {
                Fecha       = DateTime.UtcNow,
                Tipo        = TipoMovimiento.Ingreso,
                Categoria   = CategoriaMovimiento.Recaudacion,
                Monto       = vm.MontoARetirar,
                Descripcion = $"Recaudación semanal {periodoStr}" +
                              (string.IsNullOrWhiteSpace(vm.Notas) ? "" : $" – {vm.Notas}")
            });

            TempData["Success"] = $"Recaudación de {vm.MontoARetirar:C} registrada correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
