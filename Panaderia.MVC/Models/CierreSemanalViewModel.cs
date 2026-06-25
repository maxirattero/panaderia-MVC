using Panaderia.Models.DTOs;

namespace Panaderia.MVC.Models;

public class CierreSemanalViewModel
{
    public DateTime InicioSemana { get; set; }
    public DateTime FinSemana { get; set; }
    public decimal TotalCobrado { get; set; }
    public decimal CostoInsumos { get; set; }
    public decimal GananciaDinamica => TotalCobrado - CostoInsumos;
    public decimal GananciaFija => TotalCobrado * 0.70m;
    public decimal MontoARetirar { get; set; }
    public string? Notas { get; set; }
    public List<CostoProductoItem> DetallesCosto { get; set; } = new();
}
