using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Panaderia.Models.Entities
{
    public class ProduccionStock
    {
        public int Id { get; set; }
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }

        [ValidateNever]
        [ForeignKey(nameof(IdProducto))]
        public Producto Producto { get; set; } = null!;
    }
}