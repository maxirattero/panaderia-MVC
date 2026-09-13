using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class ReporteCajaService : IReporteCajaService
    {
        private readonly PanaderiaContext _context;

        public ReporteCajaService(PanaderiaContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ReporteCaja>> GetReporteCajaTotalAsync()
        {
            return await _context.ReportesCaja
                .Include(r => r.Pedido)
                .Include(r => r.Proveedor)
                .OrderByDescending(r => r.Fecha)
                .ThenByDescending(r => r.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<ReporteCaja>> GetReporteCajaPorFechaAsync(DateTime fecha)
        {
            return await _context.ReportesCaja
                .Include(r => r.Pedido)
                .Include(r => r.Proveedor)
                .Where(r => r.Fecha.Date == fecha.Date)
                .ToListAsync();
        }

        public async Task<IEnumerable<ReporteCaja>> GetReporteCajaPorRangoFechasAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            return await _context.ReportesCaja
                .Include(r => r.Pedido)
                .Include(r => r.Proveedor)
                .Where(r => r.Fecha.Date >= fechaInicio.Date && r.Fecha.Date <= fechaFin.Date)
                .ToListAsync();
        }

        public async Task<IEnumerable<ReporteCaja>> GetReporteCajaPorTipoMovimientoAsync(TipoMovimiento tipo)
        {
            return await _context.ReportesCaja
                .Include(r => r.Pedido)
                .Include(r => r.Proveedor)
                .Where(r => r.Tipo == tipo)
                .ToListAsync();
        }

        public async Task<ReporteCaja?> GetByIdAsync(int id)
        {
            return await _context.ReportesCaja
                .Include(r => r.Pedido)
                .Include(r => r.Proveedor)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task CreateAsync(ReporteCaja reporteCaja)
        {
            ValidarManual(reporteCaja);
            await using var tx = await ReglasCaja.IniciarAsync(_context);
            await ReglasCaja.PeriodoAbiertoAsync(_context, reporteCaja.Fecha);
            reporteCaja.Fecha = DateTime.SpecifyKind(reporteCaja.Fecha, DateTimeKind.Utc);
            await _context.ReportesCaja.AddAsync(reporteCaja);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task UpdateAsync(ReporteCaja reporteCaja)
        {
            ValidarManual(reporteCaja);
            await using var tx = await ReglasCaja.IniciarAsync(_context);
            var existing = await _context.ReportesCaja.FindAsync(reporteCaja.Id);
            if (existing == null) return;
            ValidarEditable(existing);
            await ReglasCaja.PeriodoAbiertoAsync(_context, existing.Fecha);
            await ReglasCaja.PeriodoAbiertoAsync(_context, reporteCaja.Fecha);

            existing.Fecha = DateTime.SpecifyKind(reporteCaja.Fecha, DateTimeKind.Utc);
            existing.Tipo = reporteCaja.Tipo;
            existing.Categoria = reporteCaja.Categoria;
            existing.Monto = reporteCaja.Monto;
            existing.Descripcion = reporteCaja.Descripcion;
            existing.IdProveedor = reporteCaja.IdProveedor;
            existing.Cuenta = reporteCaja.Cuenta;
            existing.DescontarDelReparto = reporteCaja.DescontarDelReparto;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await using var tx = await ReglasCaja.IniciarAsync(_context);
            var r = await _context.ReportesCaja.SingleOrDefaultAsync(r => r.Id == id);
            if (r == null) return;
            ValidarEditable(r);
            await ReglasCaja.PeriodoAbiertoAsync(_context, r.Fecha);
            _context.ReportesCaja.Remove(r);
            await _context.SaveChangesAsync(); await tx.CommitAsync();
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.ReportesCaja.AnyAsync(r => r.Id == id);
        }

        private static void ValidarEditable(ReporteCaja r)
        {
            if (r.IdPedido.HasValue || r.IdCompra.HasValue || r.IdCierre.HasValue || r.IdCierreDestino.HasValue || r.IdTransferencia.HasValue || r.FechaInicioPeriodo.HasValue || r.Categoria == CategoriaMovimiento.Proveedor)
                throw new InvalidOperationException("Este movimiento pertenece a un pedido, compra, transferencia o cierre. Se conserva para mantener el historial; los cobros se corrigen registrando una devolución.");
        }
        private static void ValidarManual(ReporteCaja r)
        {
            ReglasCaja.CuentaValida(r.Cuenta); ReglasCaja.ImporteValido(r.Monto);
            if (r.Tipo != TipoMovimiento.Ingreso && r.Tipo != TipoMovimiento.Egreso)
                throw new InvalidOperationException("Elegí ingreso o egreso.");
            if (r.Categoria != CategoriaMovimiento.Gasto && r.Categoria != CategoriaMovimiento.Otro && r.Categoria != CategoriaMovimiento.Ajuste)
                throw new InvalidOperationException("Usá Pedidos, Compras o Cierre para registrar esa operación.");
            if (r.IdPedido.HasValue || r.IdCompra.HasValue || r.IdCierre.HasValue || r.IdCierreDestino.HasValue || r.IdTransferencia.HasValue || r.FechaInicioPeriodo.HasValue)
                throw new InvalidOperationException("Un movimiento manual no puede vincularse a otra operación.");
            if (r.DescontarDelReparto && (r.Tipo != TipoMovimiento.Egreso || r.Cuenta == CuentaCaja.ReservaMercadoPago || r.Categoria == CategoriaMovimiento.Ajuste))
                throw new InvalidOperationException("Solo un gasto pagado en efectivo o banco puede descontarse del reparto.");
            if (string.IsNullOrWhiteSpace(r.Descripcion)) throw new InvalidOperationException("Indicá el motivo del movimiento.");
        }

        public async Task<decimal> GetSaldoAsync()
        {
            var ingresos = await _context.ReportesCaja.Where(r => r.Tipo == TipoMovimiento.Ingreso).SumAsync(r => r.Monto);
            var egresos = await _context.ReportesCaja.Where(r => r.Tipo == TipoMovimiento.Egreso).SumAsync(r => r.Monto);
            return ingresos - egresos;
        }
    }
}
