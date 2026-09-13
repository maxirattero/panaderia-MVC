namespace Panaderia.MVC.Models;

public class CompraViewModel
{
    public int IdProveedor { get; set; }
    public DateTime Fecha { get; set; }
    public string? NumeroFactura { get; set; }
    public string? Notas { get; set; }
    public List<CompraDetalleViewModel> Detalles { get; set; } = new();
    [System.ComponentModel.DataAnnotations.Range(1, 3)]
    public Panaderia.Models.Enums.CuentaCaja CuentaPago { get; set; } = Panaderia.Models.Enums.CuentaCaja.ReservaMercadoPago;
    public bool DescontarDelReparto { get; set; }
    public Guid ClaveOperacion { get; set; } = Guid.NewGuid();
}

public class CompraDetalleViewModel
{
    public int IdInsumo { get; set; }
    public int IdUnidadCompra { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal CostoEnvio { get; set; } = 0;
}
