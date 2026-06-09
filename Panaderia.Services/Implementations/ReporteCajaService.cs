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

        //reporte de caja total desde el inicio de operaciones
        public async Task<IEnumerable<ReporteCaja>> GetReporteCajaTotalAsync()
        {
            return await _context.ReportesCaja.ToListAsync();
        }

        //reporte de caja por fecha especifica
        public async Task<IEnumerable<ReporteCaja>> GetReporteCajaPorFechaAsync(DateTime fecha)
        {
            return await _context.ReportesCaja.Where(r => r.Fecha.Date == fecha.Date).ToListAsync();
        }

        //reporte de caja por rango de fechas
        public async Task<IEnumerable<ReporteCaja>> GetReporteCajaPorRangoFechasAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            return await _context.ReportesCaja.Where(r => r.Fecha.Date >= fechaInicio.Date && r.Fecha.Date <= fechaFin.Date).ToListAsync();
        }

        //reporte de caja por tipo de movimiento
        public async Task<IEnumerable<ReporteCaja>> GetReporteCajaPorTipoMovimientoAsync(TipoMovimiento tipo)
        {
            return await _context.ReportesCaja.Where(r => r.Tipo == tipo).ToListAsync();
        }

        //crear un nuevo reporte de caja
        public async Task CreateAsync(ReporteCaja reporteCaja)
        {
            await _context.ReportesCaja.AddAsync(reporteCaja);
            await _context.SaveChangesAsync();
        }

        //actualizar un reporte de caja existente
        public async Task UpdateAsync(ReporteCaja reporteCaja)
        {
            reporteCaja.Fecha = DateTime.Now;
            _context.ReportesCaja.Update(reporteCaja);
            await _context.SaveChangesAsync();
        }

        //eliminar un reporte de caja por id
        public async Task DeleteAsync(int id)
        {
            await _context.ReportesCaja.Where(r => r.Id == id).ExecuteDeleteAsync();
        }

        //verificar si un reporte de caja existe por id
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.ReportesCaja.AnyAsync(r => r.Id == id);
        }

        //metodo de saldo actual de caja
        public async Task<decimal> GetSaldoAsync()
        {
            var ingresos = await _context.ReportesCaja.Where(r => r.Tipo == TipoMovimiento.Ingreso).SumAsync(r => r.Monto);
            var egresos = await _context.ReportesCaja.Where(r => r.Tipo == TipoMovimiento.Egreso).SumAsync(r => r.Monto);
            return ingresos - egresos;

        }
    }
}