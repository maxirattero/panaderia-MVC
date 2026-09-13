using Panaderia.Models.Enums;

namespace Panaderia.Models.Entities
{
    public class ReporteCaja
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public TipoMovimiento Tipo { get; set; } = TipoMovimiento.Ingreso;
        public CategoriaMovimiento Categoria { get; set; } = CategoriaMovimiento.Otro;
        public decimal Monto { get; set; }
        public string? Descripcion { get; set; }
        public int? IdPedido { get; set; }
        public Pedido? Pedido { get; set; }
        public int? IdProveedor { get; set; }
        public Proveedor? Proveedor { get; set; }
        public DateTime? FechaInicioPeriodo { get; set; }
        public DateTime? FechaFinPeriodo { get; set; }
        // Fotografía de ventas al registrar un cierre. No es un movimiento de caja.
        public decimal? TotalVendidoInformativo { get; set; }
        public CuentaCaja Cuenta { get; set; }
        public bool DescontarDelReparto { get; set; }
        public int? IdCompra { get; set; }
        public CompraProveedor? Compra { get; set; }
        public Guid? IdTransferencia { get; set; }
        public Guid? ClaveOperacion { get; set; }
        public int? IdCierre { get; set; }
        public CierreCaja? Cierre { get; set; }
        public int? IdCierreDestino { get; set; }
        public CierreCaja? CierreDestino { get; set; }
        public DestinoCierre? Destino { get; set; }
    }
}
