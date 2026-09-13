using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.MVC.Models;
using Panaderia.Services.Implementations;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers;

public class ReporteCajaController(IReporteCajaService caja, IProveedorService proveedores, IPedidoService pedidos, ICierreCajaService cierres) : Controller
{
    public async Task<IActionResult> Index(DateTime? fechaInicio, DateTime? fechaFin, TipoMovimiento? tipoFiltro, bool soloTotalesVendidos = false, CuentaCaja? cuentaFiltro = null, int pagina = 1)
    {
        if (soloTotalesVendidos) return RedirectToAction(nameof(Historial));
        var lista = (await caja.GetReporteCajaTotalAsync()).AsEnumerable();
        if (fechaInicio.HasValue) lista = lista.Where(r => r.Fecha >= CalendarioCaja.InicioUtc(DateOnly.FromDateTime(fechaInicio.Value)));
        if (fechaFin.HasValue) lista = lista.Where(r => r.Fecha < CalendarioCaja.InicioUtc(DateOnly.FromDateTime(fechaFin.Value).AddDays(1)));
        if (cuentaFiltro.HasValue) lista = lista.Where(r => r.Cuenta == cuentaFiltro);
        var periodo = lista.ToList();
        if (tipoFiltro.HasValue) lista = lista.Where(r => r.Tipo == tipoFiltro);
        var filtrada = lista.ToList();
        pagina = Math.Max(1, pagina);
        var config = await cierres.ConfiguracionAsync();
        return View(new ReporteCajaIndexViewModel {
            Movimientos = filtrada.Skip((pagina - 1) * 50).Take(50).ToList(),
            Saldo = await caja.GetSaldoAsync(), SaldosCuentas = await cierres.SaldosAsync(), SaldosConfigurados = config.InicioSaldosUtc.HasValue,
            TotalIngresos = periodo.Where(r => r.Tipo == TipoMovimiento.Ingreso && r.Categoria != CategoriaMovimiento.Transferencia).Sum(r => r.Monto),
            TotalEgresos = periodo.Where(r => r.Tipo == TipoMovimiento.Egreso && r.Categoria != CategoriaMovimiento.Transferencia).Sum(r => r.Monto),
            FechaInicio = fechaInicio, FechaFin = fechaFin, TipoFiltro = tipoFiltro, CuentaFiltro = cuentaFiltro,
            Pagina = pagina, TotalPaginas = (int)Math.Ceiling(filtrada.Count / 50m)
        });
    }
    private async Task Cargar(ReporteCajaFormViewModel vm) => vm.Proveedores = new SelectList(await proveedores.GetAllAsync(), "Id", "Nombre");
    public async Task<IActionResult> Create()
    {
        var vm = new ReporteCajaFormViewModel { Fecha = CalendarioCaja.Hoy.ToDateTime(TimeOnly.MinValue), Categoria = CategoriaMovimiento.Gasto, Tipo = TipoMovimiento.Egreso };
        await Cargar(vm); return View(vm);
    }
    private static ReporteCaja Mapear(ReporteCajaFormViewModel vm) => new() {
        Id = vm.Id, Fecha = CalendarioCaja.InicioUtc(DateOnly.FromDateTime(vm.Fecha)), Tipo = vm.Tipo,
        Categoria = vm.Categoria, Monto = vm.Monto, Descripcion = vm.Descripcion, IdProveedor = vm.IdProveedor,
        Cuenta = vm.Cuenta, DescontarDelReparto = vm.DescontarDelReparto
    };
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReporteCajaFormViewModel vm)
    {
        if (ModelState.IsValid) try { await caja.CreateAsync(Mapear(vm)); TempData["Success"] = "Movimiento registrado."; return RedirectToAction(nameof(Index)); }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        await Cargar(vm); return View(vm);
    }
    public async Task<IActionResult> Edit(int id)
    {
        var r = await caja.GetByIdAsync(id); if (r == null) return NotFound();
        var vm = new ReporteCajaFormViewModel { Id = r.Id, Fecha = CalendarioCaja.Local(r.Fecha), Tipo = r.Tipo, Categoria = r.Categoria, Monto = r.Monto,
            Descripcion = r.Descripcion, IdProveedor = r.IdProveedor, IdPedido = r.IdPedido, Cuenta = r.Cuenta, DescontarDelReparto = r.DescontarDelReparto,
            EsAutomatico = r.IdPedido.HasValue || r.IdCompra.HasValue || r.IdCierre.HasValue || r.IdCierreDestino.HasValue || r.IdTransferencia.HasValue || r.FechaInicioPeriodo.HasValue || r.Categoria == CategoriaMovimiento.Proveedor };
        await Cargar(vm); return View(vm);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ReporteCajaFormViewModel vm)
    {
        if (ModelState.IsValid) try { await caja.UpdateAsync(Mapear(vm)); TempData["Success"] = "Movimiento actualizado."; return RedirectToAction(nameof(Index)); }
        catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        await Cargar(vm); return View(vm);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try { await caja.DeleteAsync(id); TempData["Success"] = "Movimiento eliminado."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    private async Task<CierreCajaViewModel> CargarCierre(DateOnly semana)
    {
        var resumen = await cierres.ResumenAsync(semana);
        return new() { Resumen = resumen, InicioSemana = semana, PorcentajeReserva = resumen.Cierre.PorcentajeReserva ?? 30m,
            Semanas = Enumerable.Range(0, 12).Select(i => CalendarioCaja.Lunes(CalendarioCaja.Hoy).AddDays(-i * 7)).ToList() };
    }
    public async Task<IActionResult> CierreSemanal(DateOnly? inicioSemana)
    {
        var semana = inicioSemana ?? CalendarioCaja.Lunes(CalendarioCaja.Hoy).AddDays(CalendarioCaja.Hoy.DayOfWeek == DayOfWeek.Sunday ? 0 : -7);
        try { return View(await CargarCierre(semana)); }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; return RedirectToAction(nameof(Index)); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarCierre(CierreCajaViewModel vm)
    {
        if (vm.InicioSemana == default || vm.InicioSemana != CalendarioCaja.Lunes(vm.InicioSemana) || vm.InicioSemana > CalendarioCaja.Hoy)
        {
            TempData["Error"] = "Elegí una semana válida para cerrar.";
            return RedirectToAction(nameof(CierreSemanal));
        }
        if (ModelState.IsValid) try {
            var id = await cierres.CerrarAsync(vm.InicioSemana, vm.PorcentajeReserva, vm.Notas, User.Identity?.Name, vm.AceptarCostosPendientes);
            TempData["Success"] = "Cierre guardado. Registrá los envíos a reserva y retiros cuando se realicen.";
            return RedirectToAction(nameof(DetalleCierre), new { id });
        } catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        var completo = await CargarCierre(vm.InicioSemana);
        completo.PorcentajeReserva = vm.PorcentajeReserva; completo.Notas = vm.Notas;
        return View("CierreSemanal", completo);
    }
    public async Task<IActionResult> Historial() => View(await cierres.HistorialAsync());
    public async Task<IActionResult> DetalleCierre(int id)
    {
        try { var r = await cierres.DetalleAsync(id); return View("CierreSemanal", new CierreCajaViewModel { Resumen = r, InicioSemana = r.Cierre.InicioSemana, PorcentajeReserva = r.Cierre.PorcentajeReserva ?? 30m }); }
        catch (InvalidOperationException) { return NotFound(); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarPago(PagoCierreViewModel vm)
    {
        if (!ModelState.IsValid) TempData["Error"] = "Revisá los datos del pago.";
        else try { await cierres.RegistrarPagoAsync(vm.Id, vm.Destino, vm.Cuenta, vm.Monto, vm.Fecha.HasValue ? CalendarioCaja.DesdeLocal(vm.Fecha.Value) : DateTime.UtcNow, vm.Clave); TempData["Success"] = "Movimiento registrado y pendiente actualizado."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(DetalleCierre), new { id = vm.Id });
    }
    public async Task<IActionResult> Configuracion()
    {
        var c = await cierres.ConfiguracionAsync();
        return View(new ConfiguracionCajaViewModel { PorcentajeReserva = c.PorcentajeReserva, AperturaGuardada = c.InicioSaldosUtc.HasValue,
            InicioSaldos = c.InicioSaldosUtc.HasValue ? CalendarioCaja.Local(c.InicioSaldosUtc.Value) : null,
            Efectivo = c.AperturaEfectivo, Banco = c.AperturaBanco, Reserva = c.AperturaReserva });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Configuracion(ConfiguracionCajaViewModel vm)
    {
        var config = await cierres.ConfiguracionAsync();
        vm.AperturaGuardada = config.InicioSaldosUtc.HasValue;
        if (ModelState.IsValid) try {
            await cierres.ConfigurarAsync(vm.PorcentajeReserva, !vm.AperturaGuardada && vm.InicioSaldos.HasValue ? CalendarioCaja.DesdeLocal(vm.InicioSaldos.Value) : null,
                vm.Efectivo, vm.Banco, vm.Reserva, User.Identity?.Name);
            TempData["Success"] = "Configuración guardada."; return RedirectToAction(nameof(Index));
        } catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        return View(vm);
    }
    public IActionResult Transferencia() => View(new TransferenciaCajaViewModel());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Transferencia(TransferenciaCajaViewModel vm)
    {
        if (ModelState.IsValid) try {
            await cierres.TransferirAsync(vm.Origen, vm.Destino, vm.Monto, vm.Fecha.HasValue ? CalendarioCaja.DesdeLocal(vm.Fecha.Value) : DateTime.UtcNow, vm.Clave);
            TempData["Success"] = "Transferencia registrada entre cuentas."; return RedirectToAction(nameof(Index));
        } catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        return View(vm);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Clasificar(int id, CuentaCaja cuenta, DateOnly? inicioSemana)
    {
        try { await cierres.ClasificarAsync(id, cuenta); TempData["Success"] = "Origen del movimiento registrado."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return inicioSemana.HasValue ? RedirectToAction(nameof(CierreSemanal), new { inicioSemana = inicioSemana.Value.ToString("yyyy-MM-dd") }) : RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Devolucion(int idPedido, decimal monto, CuentaCaja cuenta, string? motivo, Guid clave)
    {
        if (!ModelState.IsValid) TempData["Error"] = "Revisá los datos de la devolución.";
        else try { await pedidos.RegistrarDevolucionAsync(idPedido, monto, cuenta, DateTime.UtcNow, clave, motivo); TempData["Success"] = "Devolución registrada; el cobro original permanece en el historial."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
