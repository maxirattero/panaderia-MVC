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
        public async Task<IActionResult> CierreSemanal(string? inicioSemana)
        {
            var hoy = DateTime.UtcNow.Date;
            // Lunes de la semana actual (DayOfWeek.Sunday == 0 → offset 6, resto → dayOfWeek - 1)
            int diasDesdeLunes = hoy.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)hoy.DayOfWeek - 1;
            var lunesSemanaActual = DateTime.SpecifyKind(hoy.AddDays(-diasDesdeLunes), DateTimeKind.Utc);
            // Si hoy es domingo la semana actual (lun-dom) está completa; si no, usar la anterior
            var semanaDefecto = hoy.DayOfWeek == DayOfWeek.Sunday
                ? lunesSemanaActual
                : lunesSemanaActual.AddDays(-7);

            // Últimas 6 semanas completas (lunes a domingo, hacia atrás)
            var semanas = Enumerable.Range(0, 6)
                .Select(i => semanaDefecto.AddDays(-7 * i))
                .ToList();

            DateTime inicio = semanaDefecto;
            if (!string.IsNullOrWhiteSpace(inicioSemana)
                && DateTime.TryParseExact(inicioSemana, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed))
            {
                inicio = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            var fin = inicio.AddDays(7);
            var resumen   = await _pedidoService.GetResumenCierreSemanalAsync(inicio);
            var yaCerrado = await _pedidoService.ExisteCierreSemanalAsync(inicio, fin);

            ViewBag.Semanas = semanas.Select(s => new
            {
                Valor = s.ToString("yyyy-MM-dd"),
                Label = $"{s:dd/MM} - {s.AddDays(6):dd/MM/yyyy}"
            }).ToList();
            ViewBag.SemanaSeleccionada = inicio.ToString("yyyy-MM-dd");
            ViewBag.YaCerrado = yaCerrado;

            var vm = new CierreSemanalViewModel
            {
                InicioSemana  = inicio,
                FinSemana     = inicio.AddDays(6),
                TotalIngresos = resumen.TotalIngresos,
                TotalEgresos  = resumen.TotalEgresos,
                CostoInsumos  = resumen.CostoInsumos,
                MontoARetirar = Math.Round(resumen.GananciaEstimada, 2),
                DetallesCosto = resumen.DetallesCosto
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
                return RedirectToAction(nameof(CierreSemanal), new { inicioSemana = vm.InicioSemana.ToString("yyyy-MM-dd") });
            }

            var inicioUtc = DateTime.SpecifyKind(vm.InicioSemana.Date, DateTimeKind.Utc);
            var finUtc    = inicioUtc.AddDays(7);

            if (await _pedidoService.ExisteCierreSemanalAsync(inicioUtc, finUtc))
            {
                TempData["Error"] = "Ya existe un cierre registrado para esa semana.";
                return RedirectToAction(nameof(CierreSemanal), new { inicioSemana = vm.InicioSemana.ToString("yyyy-MM-dd") });
            }

            var periodoStr = $"{vm.InicioSemana:dd/MM} - {vm.InicioSemana.AddDays(6):dd/MM/yyyy}";
            await _reporteCajaService.CreateAsync(new ReporteCaja
            {
                Fecha                = DateTime.UtcNow,
                Tipo                 = TipoMovimiento.Egreso,
                Categoria            = CategoriaMovimiento.Recaudacion,
                Monto                = vm.MontoARetirar,
                Descripcion          = $"Recaudación semanal {periodoStr}" +
                                       (string.IsNullOrWhiteSpace(vm.Notas) ? "" : $" – {vm.Notas}"),
                FechaInicioPeriodo   = inicioUtc,
                FechaFinPeriodo      = finUtc
            });

            TempData["Success"] = $"Recaudación de {vm.MontoARetirar:C} registrada correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
