using Panaderia.Models.Entities;
using Panaderia.Models.Enums;

namespace Panaderia.MVC.Models
{
    public class ReporteCajaIndexViewModel
    {
        public IEnumerable<ReporteCaja> Movimientos { get; set; } = new List<ReporteCaja>();
        public decimal Saldo { get; set; }
        public decimal TotalIngresos { get; set; }
        public decimal TotalEgresos { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public TipoMovimiento? TipoFiltro { get; set; }
        public bool SoloTotalesVendidos { get; set; }
        public List<Panaderia.Models.DTOs.SaldoCuentaCaja> SaldosCuentas { get; set; } = [];
        public bool SaldosConfigurados { get; set; }
        public Panaderia.Models.Enums.CuentaCaja? CuentaFiltro { get; set; }
        public int Pagina { get; set; } = 1;
        public int TotalPaginas { get; set; }
    }
}
