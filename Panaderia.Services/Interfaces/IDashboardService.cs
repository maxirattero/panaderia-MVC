using Panaderia.Models.DTOs;

namespace Panaderia.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<DashboardResumen> GetResumenDashboardAsync();
    }
}
