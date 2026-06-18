using Panaderia.Models.Enums;

namespace Panaderia.MVC.Models
{
    public class DetallePedidoViewModel
    {
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public TipoBolsa Bolsa { get; set; }
    }
}