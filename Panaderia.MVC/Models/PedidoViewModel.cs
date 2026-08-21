using System.ComponentModel.DataAnnotations;
using Panaderia.Models.Enums;

namespace Panaderia.MVC.Models
{
    public class PedidoViewModel
    {
        public int Id { get; set; }
        public int IdCliente { get; set; }
        public EstadoPedido Estado { get; set; }
        public DateTime? FechaEntrega { get; set; }

        // Nullable a propósito: sin descuento el input se ve vacío (no "0,00").
        [Display(Name = "Descuento (%)")]
        [Range(0, 100, ErrorMessage = "El descuento debe estar entre 0 y 100.")]
        public decimal? DescuentoPorcentaje { get; set; }

        public DateTime FechaCreacion { get; set; }
        public string? Notas { get; set; }
        public List<DetallePedidoViewModel> Detalles { get; set; } = new();
    }
}