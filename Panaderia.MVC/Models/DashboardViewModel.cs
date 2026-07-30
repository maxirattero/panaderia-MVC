using Panaderia.Models.DTOs;

namespace Panaderia.MVC.Models
{
    public class DashboardViewModel
    {
        public CajaMesResumen Caja { get; set; } = null!;
        public AlertasDiaResumen Alertas { get; set; } = null!;
        public ProduccionSemanaResumen Produccion { get; set; } = null!;
    }
}
