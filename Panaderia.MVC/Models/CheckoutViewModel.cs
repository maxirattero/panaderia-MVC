using System.ComponentModel.DataAnnotations;

namespace Panaderia.MVC.Models
{
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Tu nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        public string? Apellido { get; set; }

        [Required(ErrorMessage = "Tu teléfono es obligatorio para coordinar la entrega.")]
        public string Telefono { get; set; } = string.Empty;

        // "delivery" (sin costo) o "retiro" (punto de retiro)
        [Required]
        public string Entrega { get; set; } = "delivery";

        // "efectivo" o "transferencia" (alias masaviva.pan)
        [Required]
        public string MedioPago { get; set; } = "efectivo";

        public string? Direccion { get; set; }

        [MaxLength(500)]
        public string? Notas { get; set; }

        // Solo para mostrar en la vista (no se postea)
        public CarritoViewModel Carrito { get; set; } = new();
        public DateTime FechaEntrega { get; set; }
    }
}
