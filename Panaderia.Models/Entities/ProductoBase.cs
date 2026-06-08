using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities
{
    public class ProductoBase
    {
        public int Id { get; set; }
        public int IdCategoria { get; set; }
        [Required(ErrorMessage = "El nombre del producto es obligatorio.")]        
        public string Nombre { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public CategoriaProducto Categoria { get; set; } = null!;
    }
}
