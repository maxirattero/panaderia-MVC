using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Panaderia.Models.Entities
{
    // Imagen del producto para la tienda. Va en tabla aparte para que los
    // listados de Producto no carguen los bytes de la imagen en cada query.
    public class ProductoImagen
    {
        public int Id { get; set; }
        public int IdProducto { get; set; }
        public byte[] Datos { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "image/jpeg";
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }

        [ValidateNever]
        public Producto Producto { get; set; } = null!;
    }
}
