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
    public decimal SumaPorcentajes =>
        Detalles?.Where(d => d.PorcentajePanadero.HasValue)
                 .Sum(d => d.PorcentajePanadero!.Value) ?? 0m;

    [NotMapped]
    public decimal CostoTotal => CalcularCosto(false);

    [NotMapped]
    public decimal CostoIngredientesPorUnidad =>
        TamanioLote > 0 ? CalcularCosto(true) / TamanioLote : 0m;

    private decimal CalcularCosto(bool soloIngredientes) =>
        SumaPorcentajes == 0 ? 0m :
        Detalles?.Sum(d =>
        {
            if (soloIngredientes && d.Insumo is not null && d.Insumo.TipoInsumo != Panaderia.Models.Enums.TipoInsumo.Ingrediente)
                return 0m;
            if (d.Insumo is not null && d.PorcentajePanadero.HasValue)
                return d.Insumo.CostoPorUnidadBase
                       * (PesoMasaTotal / SumaPorcentajes * d.PorcentajePanadero.Value);

            if (d.Insumo is not null && d.CantidadFija.HasValue)
                return d.Insumo.CostoPorUnidadBase * d.CantidadFija.Value * TamanioLote;

            if (d.SubReceta is not null && d.PorcentajePanadero.HasValue)
            {
                var gramosSubReceta = PesoMasaTotal / SumaPorcentajes * d.PorcentajePanadero.Value;
                var sumaPctSub = d.SubReceta.Detalles?
                    .Where(sd => sd.PorcentajePanadero.HasValue)
                    .Sum(sd => sd.PorcentajePanadero!.Value) ?? 0m;
                if (sumaPctSub == 0) return 0m;
                return d.SubReceta.Detalles?.Sum(sd =>
                    sd.Insumo is null || (soloIngredientes && sd.Insumo.TipoInsumo != Panaderia.Models.Enums.TipoInsumo.Ingrediente) ? 0m :
                    sd.PorcentajePanadero.HasValue
                        ? sd.Insumo.CostoPorUnidadBase
                          * (gramosSubReceta / sumaPctSub * sd.PorcentajePanadero.Value)
                        : sd.Insumo.CostoPorUnidadBase * (sd.CantidadFija ?? 0m) * TamanioLote
                ) ?? 0m;
            }

            return 0m;
        }) ?? 0m;

    [NotMapped]
    public decimal CostoPorUnidad =>
        TamanioLote > 0 ? CostoTotal / TamanioLote : 0m;
}
