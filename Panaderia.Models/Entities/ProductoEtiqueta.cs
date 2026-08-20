using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Panaderia.Models.Entities
{
    // Relación muchos a muchos entre Producto y Etiqueta.
    // Clave compuesta (IdProducto, IdEtiqueta); ambas FKs en cascade:
    // la asignación no guarda historial, así que muere con el producto o la etiqueta.
    public class ProductoEtiqueta
    {
        public int IdProducto { get; set; }
        public int IdEtiqueta { get; set; }

        [ValidateNever]
        public Producto Producto { get; set; } = null!;

        [ValidateNever]
        public Etiqueta Etiqueta { get; set; } = null!;
    }
}
