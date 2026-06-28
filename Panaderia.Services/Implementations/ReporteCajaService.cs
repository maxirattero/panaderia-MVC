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
            reporteCaja.Fecha = DateTime.SpecifyKind(reporteCaja.Fecha, DateTimeKind.Utc);
            await _context.ReportesCaja.AddAsync(reporteCaja);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(ReporteCaja reporteCaja)
        {
            var existing = await _context.ReportesCaja.FindAsync(reporteCaja.Id);
            if (existing == null) return;

            existing.Fecha = DateTime.SpecifyKind(reporteCaja.Fecha, DateTimeKind.Utc);
            existing.Tipo = reporteCaja.Tipo;
            existing.Categoria = reporteCaja.Categoria;
            existing.Monto = reporteCaja.Monto;
            existing.Descripcion = reporteCaja.Descripcion;
            existing.IdProveedor = reporteCaja.IdProveedor;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await _context.ReportesCaja.Where(r => r.Id == id).ExecuteDeleteAsync();
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.ReportesCaja.AnyAsync(r => r.Id == id);
        }

        public async Task<decimal> GetSaldoAsync()
        {
            var ingresos = await _context.ReportesCaja.Where(r => r.Tipo == TipoMovimiento.Ingreso).SumAsync(r => r.Monto);
            var egresos = await _context.ReportesCaja.Where(r => r.Tipo == TipoMovimiento.Egreso).SumAsync(r => r.Monto);
            return ingresos - egresos;
        }
    }
}
