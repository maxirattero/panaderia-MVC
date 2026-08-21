using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities
{
    public class Cliente
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;
        public string? Apellido { get; set; }
        public string NombreCompleto => string.IsNullOrWhiteSpace(Apellido) ? Nombre : $"{Nombre} {Apellido}";
        public string? Direccion { get; set; }
        public string? Localidad { get; set; }
        public string? Provincia { get; set; }
        public string? Telefono { get; set; }
        public bool Revendedor { get; set; } = false;
        // Descuento habitual del cliente. Null = sin descuento (el input queda vacío).
        // Solo se usa en el admin: autocompleta el descuento al crear/editar un pedido.
        [Display(Name = "Descuento (%)")]
        [Range(0, 100, ErrorMessage = "El descuento debe estar entre 0 y 100.")]
        public decimal? DescuentoPorcentaje { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }        
    }
}
