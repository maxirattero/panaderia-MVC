using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Panaderia.Models.Entities;

public class SubRecetaDetalle
{
    public int Id { get; set; }
    public int IdSubReceta { get; set; }
    public int IdInsumo { get; set; }
    public decimal? PorcentajePanadero { get; set; }
    public decimal? CantidadFija { get; set; }

    [ValidateNever]
    public SubReceta SubReceta { get; set; } = null!;

    [ValidateNever]
    public Insumo Insumo { get; set; } = null!;
}
