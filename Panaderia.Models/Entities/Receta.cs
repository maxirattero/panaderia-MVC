using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Panaderia.Models.Entities;

public class Receta
{
    public int Id { get; set; }

    public int IdProducto { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El tamaño del lote debe ser al menos 1.")]
    public int TamanioLote { get; set; } = 1;

    public decimal PesoUnitario { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }

    [ValidateNever]
    public Producto Producto { get; set; } = null!;

    public List<RecetaDetalle> Detalles { get; set; } = new();

    [NotMapped]
    public decimal PesoMasaTotal => TamanioLote * PesoUnitario;

    [NotMapped]
    private decimal SumaPorcentajes =>
        Detalles?.Where(d => d.PorcentajePanadero.HasValue)
                 .Sum(d => d.PorcentajePanadero!.Value) ?? 0m;

    [NotMapped]
    public decimal CostoTotal =>
        SumaPorcentajes == 0 ? 0m :
        Detalles?.Sum(d =>
            d.Insumo is null ? 0m :
            d.PorcentajePanadero.HasValue
                ? d.Insumo.CostoPorUnidadBase
                    * (PesoMasaTotal / SumaPorcentajes * d.PorcentajePanadero.Value)
                : d.Insumo.CostoPorUnidadBase * (d.CantidadFija ?? 0m) * TamanioLote
        ) ?? 0m;

    [NotMapped]
    public decimal CostoPorUnidad =>
        TamanioLote > 0 ? CostoTotal / TamanioLote : 0m;
}
