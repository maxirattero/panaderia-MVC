using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities;

public class CompraDetalle
{
    public int Id { get; set; }

    public int IdCompra { get; set; }

    public int IdInsumo { get; set; }

    public int IdUnidadCompra { get; set; }

    [Required]
    [Range(0.001, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a cero.")]
    public decimal Cantidad { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser mayor o igual a cero.")]
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal { get; set; } = 0;
    public decimal CostoEnvio { get; set; } = 0;

    [ValidateNever]
    public CompraProveedor Compra { get; set; } = null!;

    [ValidateNever]
    public Insumo Insumo { get; set; } = null!;

    [ValidateNever]
    public UnidadCompra UnidadCompra { get; set; } = null!;
}
