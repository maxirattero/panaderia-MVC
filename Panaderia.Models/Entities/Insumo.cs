using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Panaderia.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Panaderia.Models.Entities;

public class Insumo
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El campo es obligatorio.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El campo es obligatorio.")]
    public UnidadMedida UnidadBase { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Debe ser mayor o igual a cero.")]
    public decimal PrecioCompra { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Debe ser mayor a cero.")]
    public decimal CantidadRendimiento { get; set; }

    public int? IdProveedor { get; set; }

    [ValidateNever]
    public Proveedor? Proveedor { get; set; }

    public string? Notas { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaModificacion { get; set; }

    [NotMapped]
    public decimal CostoPorUnidadBase =>
        CantidadRendimiento > 0 ? PrecioCompra / CantidadRendimiento : 0m;
}
