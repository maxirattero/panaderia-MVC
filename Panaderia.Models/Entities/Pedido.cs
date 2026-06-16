using Panaderia.Models.Enums;

namespace Panaderia.Models.Entities
{
    public class Pedido
    {
        public int Id { get; set; }
        public int IdCliente { get; set; }
        public EstadoPedido Estado { get; set; } = EstadoPedido.Pendiente;
        public DateTime? FechaEntrega { get; set; }
        public decimal MontoTotal { get; set; }
        public string? Notas { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public decimal MontoCobrado { get; set; } = 0;
        public bool EstaPagado => MontoCobrado >= MontoTotal;
        public decimal SaldoPendiente => MontoTotal - MontoCobrado;
        public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
        public ICollection<ReporteCaja> Reportes { get; set; } = new List<ReporteCaja>();
        public Cliente Cliente { get; set; } = null!;
    }
}