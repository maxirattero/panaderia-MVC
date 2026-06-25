using Panaderia.Models.Entities;

namespace Panaderia.MVC.Models;

public class ImprimirRecetaViewModel
{
    public Receta Receta { get; set; } = null!;
    public decimal CantidadUnidades { get; set; }
    public decimal VecesReceta => CantidadUnidades / Receta.TamanioLote;
}
