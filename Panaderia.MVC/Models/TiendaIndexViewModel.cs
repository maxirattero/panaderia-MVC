using Panaderia.Models.Entities;

namespace Panaderia.MVC.Models
{
    public class TiendaIndexViewModel
    {
        public static string SeccionCategoria(string? categoria) => categoria?.Trim().ToLowerInvariant() switch
        {
            "pan" or "panes" => "Panes",
            "cracker" or "crackers" => "Crackers",
            "pizza" or "pizzas" or "prepizza" or "prepizzas" => "Pizzas",
            _ => "Otros"
        };

        public static int OrdenCategoria(string? categoria) => SeccionCategoria(categoria) switch
        {
            "Panes" => 0,
            "Crackers" => 1,
            "Pizzas" => 2,
            _ => 3
        };

        public List<Producto> Productos { get; set; } = new();
        public List<string> Categorias { get; set; } = new();
        public string? CategoriaSeleccionada { get; set; }
        public string? Busqueda { get; set; }

        // Etiquetas presentes en el catálogo visible (Vegano, Sin gluten, ...)
        public List<Etiqueta> Etiquetas { get; set; } = new();
        public int? EtiquetaSeleccionada { get; set; }
        public bool EsRevendedor { get; set; }
        public bool PedidosSemanalesCerrados { get; set; }
        public Dictionary<int, int> CantidadesEnCarrito { get; set; } = new();
    }
}
