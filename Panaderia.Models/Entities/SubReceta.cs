using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities;

public class SubReceta
{
    public int Id { get; set; }

    [Required]
    public string Nombre { get; set; } = string.Empty;

    public string? Notas { get; set; }

    public decimal MargenSeguridad { get; set; } = 0;

    public List<SubRecetaDetalle> Detalles { get; set; } = new();
}
