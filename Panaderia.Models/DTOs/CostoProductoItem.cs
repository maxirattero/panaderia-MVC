namespace Panaderia.Models.DTOs;

public class CostoProductoItem
{
    public string NombreProducto { get; set; } = string.Empty;
    public int CantidadVendida { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal CostoTotal => CantidadVendida * CostoUnitario;
}
