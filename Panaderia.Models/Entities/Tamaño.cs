using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities
{
    public class Tamaño
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "El campo es obligatorio.")]
        public string Descripcion { get; set; } = string.Empty;
    }
}
