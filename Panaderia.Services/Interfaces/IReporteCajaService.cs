using Panaderia.Models.Entities;
using Panaderia.Models.Enums;


namespace Panaderia.Services.Interfaces
{
    public interface IReporteCajaService
    {
        //obtener el reporte de caja total desde el inicio de operaciones
        Task<IEnumerable<ReporteCaja>> GetReporteCajaTotalAsync();

        //obtener el reporte de caja por fecha especifica
        Task<IEnumerable<ReporteCaja>> GetReporteCajaPorFechaAsync(DateTime fecha);

        //obtener el reporte de caja por rango de fechas
        Task<IEnumerable<ReporteCaja>> GetReporteCajaPorRangoFechasAsync(DateTime fechaInicio, DateTime fechaFin);

        //obtener el reporte de caja por tipo de movimiento
        Task<IEnumerable<ReporteCaja>> GetReporteCajaPorTipoMovimientoAsync(TipoMovimiento tipo);

        //crear un nuevo reporte de caja
        Task CreateAsync(ReporteCaja reporteCaja);

        //actualizar un reporte de caja existente
        Task UpdateAsync(ReporteCaja reporteCaja);

        //eliminar un reporte de caja por id
        Task DeleteAsync(int id);

        //verificar si un reporte de caja existe por id
        Task<bool> ExistsAsync(int id);

        //metodo de saldo actual de caja
        Task<decimal> GetSaldoAsync();

        //obtener un reporte de caja por id
        Task<ReporteCaja?> GetByIdAsync(int id);
    }
}