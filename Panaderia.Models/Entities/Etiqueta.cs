using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities
{
    // Etiqueta de tienda (Masa madre, Vegano, Sin gluten, ...).
    // Se administra desde Configuración y se asigna a los productos.
    public class Etiqueta
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de la etiqueta es obligatorio.")]
        [MaxLength(40)]
        public string Nombre { get; set; } = string.Empty;

        // Nombre del ícono de Material Symbols (ej. "grain", "eco"). Ver Configuración.
        [MaxLength(40)]
        public string Icono { get; set; } = "grain";

        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}
