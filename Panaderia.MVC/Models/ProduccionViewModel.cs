using Panaderia.Models.DTOs;

namespace Panaderia.MVC.Models
{
    public class ProduccionViewModel
    {
        public List<ResumenProductoItem> PorProducto { get; set; } = new();
        public List<ResumenBolsaItem> PorBolsa { get; set; } = new();
    }
}
