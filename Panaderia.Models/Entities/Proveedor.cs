using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities
{
    public class Proveedor
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "El campo es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;
        public string? Contacto { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Direccion { get; set; }
        public string? Notas { get; set; }
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
    }
}