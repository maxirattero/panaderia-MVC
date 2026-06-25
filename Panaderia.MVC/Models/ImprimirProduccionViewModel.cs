using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;

namespace Panaderia.MVC.Models;

public class ImprimirProduccionItemViewModel
{
    public string NombreProducto { get; set; } = string.Empty;
    public decimal CantidadUnidades { get; set; }
    public Receta Receta { get; set; } = null!;
    public decimal VecesReceta => CantidadUnidades / Receta.TamanioLote;
}

public class ImprimirProduccionViewModel
{
    public List<ImprimirProduccionItemViewModel> Items { get; set; } = new();
    public DateTime Fecha { get; set; }
    public List<ResumenSubRecetaItem> SubRecetas { get; set; } = new();
}
