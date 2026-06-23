using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities;

public class UnidadCompra
{
    public int Id { get; set; }

    public int IdInsumo { get; set; }

    [Required(ErrorMessage = "El campo es obligatorio.")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "Debe ser mayor a cero.")]
    public decimal FactorConversion { get; set; }

    public bool EsPorDefecto { get; set; } = false;

    [ValidateNever]
    public Insumo Insumo { get; set; } = null!;
}
