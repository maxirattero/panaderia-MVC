using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities
{
    public class Cliente
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;
        [Required(ErrorMessage = "El apellido es obligatorio.")]
        public string Apellido { get; set; } = string.Empty;
        public string? Direccion { get; set; }
        public string? Localidad { get; set; }
        public string? Provincia { get; set; }
        public string? Telefono { get; set; }
        public bool Revendedor { get; set; } = false;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }        
    }
}
