using Panaderia.Models.Enums;

namespace Panaderia.Models.Entities;

public class AporteCompra
{
    public int Id { get; set; }
    public int IdCompra { get; set; }
    public CompraProveedor Compra { get; set; } = null!;
    public OrigenFondos Origen { get; set; }
    public decimal Monto { get; set; }
    public CuentaCaja? Cuenta { get; set; }
}

// Un lote puede reunir costos de varias compras. Sus unidades se consumen una sola vez.
public class LoteFinanciado
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int IdProducto { get; set; }
    public Producto Producto { get; set; } = null!;
    public int Unidades { get; set; }
    public DateTime DesdeUtc { get; set; }
    public Guid ClaveOperacion { get; set; }
    public List<AsignacionCompraLote> Asignaciones { get; set; } = [];
}

public class AsignacionCompraLote
{
    public int Id { get; set; }
    public int IdCompra { get; set; }
    public CompraProveedor Compra { get; set; } = null!;
    public int IdLote { get; set; }
    public LoteFinanciado Lote { get; set; } = null!;
    public decimal Monto { get; set; }
    public Guid ClaveOperacion { get; set; }
}

public class VentaLote
{
    public int Id { get; set; }
    public int IdLote { get; set; }
    public LoteFinanciado Lote { get; set; } = null!;
    public int IdDetallePedido { get; set; }
    public DetallePedido DetallePedido { get; set; } = null!;
    public int Unidades { get; set; }
    public int IdCierre { get; set; }
    public CierreCaja Cierre { get; set; } = null!;
}

public class ReservaReintegro
{
    public int Id { get; set; }
    public int IdAporte { get; set; }
    public AporteCompra Aporte { get; set; } = null!;
    public int IdCierre { get; set; }
    public CierreCaja Cierre { get; set; } = null!;
    public decimal Monto { get; set; }
}

public class PagoReintegro
{
    public int Id { get; set; }
    public int IdAporte { get; set; }
    public AporteCompra Aporte { get; set; } = null!;
    public int? IdReserva { get; set; }
    public ReservaReintegro? Reserva { get; set; }
    public int IdMovimiento { get; set; }
    public ReporteCaja Movimiento { get; set; } = null!;
    public decimal Monto { get; set; }
    public string? Usuario { get; set; }
}
