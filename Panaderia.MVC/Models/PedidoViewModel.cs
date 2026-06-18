using Panaderia.Models.Enums;

namespace Panaderia.MVC.Models
{
    public class PedidoViewModel
    {
        public int Id { get; set; }
        public int IdCliente { get; set; }
        public EstadoPedido Estado { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public string? Notas { get; set; }
        public List<DetallePedidoViewModel> Detalles { get; set; } = new();
    }
}