using Panaderia.Models.Enums;

namespace Panaderia.MVC.Models
{
    public class DetallePedidoDetailsViewModel
    {
        public string NombreProducto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public TipoBolsa Bolsa { get; set; }
    }
}
