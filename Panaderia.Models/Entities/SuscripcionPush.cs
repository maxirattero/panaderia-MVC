using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities;

public class SuscripcionPush
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(1024)]
    public string Endpoint { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string P256dh { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Auth { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; }
    public DateTime FechaModificacion { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
