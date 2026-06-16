using Panaderia.Models.Entities;

namespace Panaderia.MVC.Models
{
    public class ConfiguracionViewModel
    {
        public IEnumerable<CategoriaProducto> Categorias { get; set; } = new List<CategoriaProducto>();
        public IEnumerable<Formato> Formatos { get; set; } = new List<Formato>();
        public IEnumerable<Tamano> Tamanos { get; set; } = new List<Tamano>();
    }
}