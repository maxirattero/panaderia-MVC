namespace Panaderia.Models.DTOs;

public class ResumenCierreSemanal
{
    public decimal TotalIngresos { get; set; }
    public decimal TotalEgresos { get; set; }
    public decimal NetoSemana => TotalIngresos - TotalEgresos;
    public decimal CostoInsumos { get; set; }
    public decimal GananciaEstimada => NetoSemana - CostoInsumos;
    public List<CostoProductoItem> DetallesCosto { get; set; } = new();
}
