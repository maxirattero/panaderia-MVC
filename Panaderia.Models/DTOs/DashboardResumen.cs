namespace Panaderia.Models.DTOs
{
    public class DashboardResumen
    {
        public CajaMesResumen Caja { get; set; } = new();
        public AlertasDiaResumen Alertas { get; set; } = new();
        public ProduccionSemanaResumen Produccion { get; set; } = new();
    }

    public class CajaMesResumen
    {
        public int Mes { get; set; }
        public int Anio { get; set; }
        public decimal TotalIngresos { get; set; }
        public decimal TotalEgresos { get; set; }
        public decimal Balance => TotalIngresos - TotalEgresos;
    }

    public class AlertasDiaResumen
    {
        public int PedidosHoyTotal { get; set; }
        public int PedidosHoyPendientes { get; set; }
        public int PedidosHoyListos { get; set; }
        public decimal SaldoPendienteTotal { get; set; }
        public int PedidosSaldoPendienteCount { get; set; }
        public int InsumosBajoStockCount { get; set; }
        public List<string> InsumosBajoStockNombres { get; set; } = new();
    }

    public class ProduccionSemanaResumen
    {
        public List<ResumenProductoItem> PorProducto { get; set; } = new();
        public DateTime LunesActual { get; set; }
        public DateTime DomingoActual { get; set; }
    }
}
