namespace Panaderia.MVC.Models
{
    public class ProductoRecetaOption
    {
        public int IdProducto { get; set; }
        public int IdReceta { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
    }
}
