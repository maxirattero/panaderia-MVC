using Panaderia.Models.DTOs;

namespace Panaderia.MVC.Models;

public class CierreSemanalViewModel
{
    public DateTime InicioSemana { get; set; }
    public DateTime FinSemana { get; set; }
    // Se muestra por separado: los cobros ya componen TotalIngresos.
    public decimal TotalVendido { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal NetoSemana => TotalIngresos - TotalEgresos;
    public decimal CostoInsumos { get; set; }
    public decimal GananciaEstimada => NetoSemana - CostoInsumos;
    public decimal GananciaFija => NetoSemana * 0.70m;
    public decimal MontoARetirar { get; set; }
    public string? Notas { get; set; }
    public List<CostoProductoItem> DetallesCosto { get; set; } = new();
}
