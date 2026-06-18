using Panaderia.Models.Entities;
using Panaderia.Models.Enums;

namespace Panaderia.MVC.Models
{
    public class PedidoDetailsViewModel
    {
        public int Id { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public EstadoPedido Estado { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public string? Notas { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal MontoCobrado { get; set; }
        public decimal SaldoPendiente { get; set; }
        public bool EstaPagado { get; set; }
        public List<DetallePedidoDetailsViewModel> Detalles { get; set; } = new();
    }
}