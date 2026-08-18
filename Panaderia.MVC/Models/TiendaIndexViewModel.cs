using Panaderia.Models.Entities;

namespace Panaderia.MVC.Models
{
    public class TiendaIndexViewModel
    {
        public List<Producto> Productos { get; set; } = new();
        public List<string> Categorias { get; set; } = new();
        public string? CategoriaSeleccionada { get; set; }
        public string? Busqueda { get; set; }
    }
}
