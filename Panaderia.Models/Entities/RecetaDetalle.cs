using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Panaderia.Models.Entities;

public class RecetaDetalle
{
    public int Id { get; set; }

    public int IdReceta { get; set; }

    public int? IdInsumo { get; set; }
    public int? IdSubReceta { get; set; }

    public decimal? PorcentajePanadero { get; set; }
    public decimal? CantidadFija { get; set; }

    [ValidateNever]
    public Receta Receta { get; set; } = null!;

    [ValidateNever]
    public Insumo? Insumo { get; set; }

    [ValidateNever]
    public SubReceta? SubReceta { get; set; }
}
