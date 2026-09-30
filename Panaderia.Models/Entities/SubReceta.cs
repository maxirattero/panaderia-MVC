using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Panaderia.Models.Enums;

namespace Panaderia.Models.Entities;

public class SubReceta
{
    public int Id { get; set; }

    [Required]
    public string Nombre { get; set; } = string.Empty;

    public string? Notas { get; set; }

    public decimal MargenSeguridad { get; set; } = 0;

    public List<SubRecetaDetalle> Detalles { get; set; } = new();

    [NotMapped]
    public decimal SumaPorcentajes => Detalles.Sum(d => d.PorcentajePanadero ?? 0m);

    public decimal CalcularCosto(decimal gramos, decimal unidades, bool soloIngredientes = false) =>
        Desglosar(gramos, unidades).Where(x => !soloIngredientes || x.Insumo.TipoInsumo == TipoInsumo.Ingrediente)
            .Sum(x => x.Cantidad * x.Insumo.CostoPorUnidadBase);

    public List<(Insumo Insumo, decimal Cantidad, string Nombre)> Desglosar(decimal gramos, decimal unidades) =>
        Desglosar(gramos, unidades, new HashSet<SubReceta>(), "");

    private List<(Insumo Insumo, decimal Cantidad, string Nombre)> Desglosar(decimal gramos, decimal unidades, HashSet<SubReceta> camino, string prefijo)
    {
        if (!camino.Add(this)) throw new InvalidOperationException("Las sub-recetas no pueden tener referencias circulares.");
        try
        {
            var resultado = new List<(Insumo, decimal, string)>();
            foreach (var d in Detalles)
            {
                var cantidad = d.PorcentajePanadero.HasValue
                    ? (SumaPorcentajes > 0 ? gramos / SumaPorcentajes * d.PorcentajePanadero.Value : 0m)
                    : (d.CantidadFija ?? 0m) * unidades;
                if (d.SubRecetaIngrediente != null)
                    resultado.AddRange(d.SubRecetaIngrediente.Desglosar(cantidad, unidades, camino, prefijo + d.SubRecetaIngrediente.Nombre + " › "));
                else if (d.Insumo != null)
                    resultado.Add((d.Insumo, cantidad, prefijo + d.Insumo.Nombre));
                else throw new InvalidOperationException("No se pudo cargar un ingrediente de la sub-receta.");
            }
            return resultado;
        }
        finally { camino.Remove(this); }
    }
}
