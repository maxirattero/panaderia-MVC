using Panaderia.Models.Entities;

namespace Panaderia.MVC.Models
{
    public class CarritoItemViewModel
    {
        public Producto Producto { get; set; } = null!;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => PrecioUnitario * Cantidad;
    }

    public class CarritoViewModel
    {
        public List<CarritoItemViewModel> Items { get; set; } = new();
        public decimal Total => Items.Sum(i => i.Subtotal);
        public int CantidadTotal => Items.Sum(i => i.Cantidad);
    }
}
