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
        Detalles?.Sum(d =>
        {
            if (soloIngredientes && d.Insumo is not null && d.Insumo.TipoInsumo != Panaderia.Models.Enums.TipoInsumo.Ingrediente)
                return 0m;
            if (d.Insumo is not null && d.PorcentajePanadero.HasValue)
                return SumaPorcentajes == 0 ? 0m : d.Insumo.CostoPorUnidadBase
                       * (PesoMasaTotal / SumaPorcentajes * d.PorcentajePanadero.Value);

            if (d.Insumo is not null && d.CantidadFija.HasValue)
                return d.Insumo.CostoPorUnidadBase * d.CantidadFija.Value * TamanioLote;

            if (d.SubReceta is not null && d.PorcentajePanadero.HasValue)
            {
                if (SumaPorcentajes == 0) return 0m;
                var gramosSubReceta = PesoMasaTotal / SumaPorcentajes * d.PorcentajePanadero.Value;
                return d.SubReceta.CalcularCosto(gramosSubReceta, TamanioLote, soloIngredientes);
            }

            return 0m;
        }) ?? 0m;

    [NotMapped]
    public decimal CostoPorUnidad =>
        TamanioLote > 0 ? CostoTotal / TamanioLote : 0m;
}
