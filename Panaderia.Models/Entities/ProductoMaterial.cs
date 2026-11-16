using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities;

public class ProductoMaterial
{
    public int Id { get; set; }
    public int IdProducto { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Elegí un material.")]
    public int IdInsumo { get; set; }
    [Range(typeof(decimal), "0.0001", "999999.9999", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "La cantidad por producto debe ser mayor que cero.")]
    public decimal Cantidad { get; set; } = 1;
    [ValidateNever] public Producto Producto { get; set; } = null!;
    [ValidateNever] public Insumo Insumo { get; set; } = null!;
}
