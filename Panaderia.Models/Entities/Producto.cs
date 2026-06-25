using Panaderia.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Panaderia.Models.Entities
{
    public class Producto
    {
        public int Id { get; set; }
        public int IdCategoria { get; set; }
        public Masa Masa { get; set; }
        public Variedad? Variedad { get; set; }
        public int? IdFormato { get; set; }
        public int? IdTamano { get; set; }
        public string? Nombre { get; set; }

        [NotMapped]
        public string NombreVisible
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Nombre))
                    return Nombre;

                var partes = new List<string>();
                if (Categoria != null) partes.Add(Categoria.Nombre);
                else if (IdCategoria > 0) partes.Add($"Cat.{IdCategoria}");

                partes.Add(Masa.ToString());

                if (Variedad.HasValue) partes.Add(Variedad.Value.ToString());
                if (Formato != null) partes.Add(Formato.Descripcion);
                else if (IdFormato.HasValue) partes.Add($"Fmt.{IdFormato}");

                return string.Join(" ", partes);
            }
        }

        public decimal PrecioFinal { get; set; }
        public decimal PrecioReventa { get; set; }
        public int Stock { get; set; }
        public string? ImagenURL { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        [ValidateNever]
        public CategoriaProducto Categoria { get; set; } = null!;
        [ValidateNever]
        public Formato? Formato { get; set; }
        [ValidateNever]
        public Tamano? Tamano { get; set; }
    }
}