using Panaderia.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Panaderia.Models.Entities
{
    public class Producto
    {
        public int Id { get; set; }
        public int IdCategoria { get; set; }
        public Masa? Masa { get; set; }
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

                if (Masa.HasValue) partes.Add(Masa.Value.ToString());

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
        public bool OcultoEnTienda { get; set; }
        public bool SinStock { get; set; }
        public bool PorEncargo { get; set; }

        [MaxLength(1000)]
        public string? DescripcionTienda { get; set; }

        [MaxLength(2000)]
        public string? Ingredientes { get; set; }

        // Los productos por encargo siempre se pueden pedir. Para el resto, la
        // disponibilidad de tienda depende de las unidades reales en stock.
        [NotMapped]
        public bool EstaSinStockEnTienda => !PorEncargo && Stock <= 0;

        [NotMapped]
        public bool EstaEnStockEnTienda => !PorEncargo && Stock > 0;

        [MaxLength(2000)]
        public string? ObservacionesElaboracion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        // Etiquetas de tienda asignadas (Masa madre, Vegano, ...).
        // [BindNever] es obligatorio: sin él, el model binder intenta escribir estas
        // colecciones desde el form del producto y revienta (Etiquetas es solo lectura).
        [ValidateNever]
        [BindNever]
        public ICollection<ProductoEtiqueta> ProductoEtiquetas { get; set; } = new List<ProductoEtiqueta>();

        [NotMapped]
        [ValidateNever]
        [BindNever]
        public IEnumerable<Etiqueta> Etiquetas =>
            (ProductoEtiquetas ?? new List<ProductoEtiqueta>())
                .Where(pe => pe.Etiqueta != null)
                .Select(pe => pe.Etiqueta)
                .OrderBy(e => e.Nombre, StringComparer.Create(new System.Globalization.CultureInfo("es-AR"), ignoreCase: true));

        [ValidateNever]
        public CategoriaProducto Categoria { get; set; } = null!;
        [ValidateNever]
        public Formato? Formato { get; set; }
        [ValidateNever]
        public Tamano? Tamano { get; set; }
    }
}
