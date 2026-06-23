using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Entities;

public class CompraProveedor
{
    public int Id { get; set; }

    [Required]
    public int IdProveedor { get; set; }

    public DateTime Fecha { get; set; }

    public string? NumeroFactura { get; set; }

    public string? Notas { get; set; }

    public decimal MontoTotal { get; set; } = 0;

    [ValidateNever]
    public Proveedor Proveedor { get; set; } = null!;

    public List<CompraDetalle> Detalles { get; set; } = new();
}
