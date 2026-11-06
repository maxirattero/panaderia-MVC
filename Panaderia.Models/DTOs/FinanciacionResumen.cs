using Panaderia.Models.Entities;

namespace Panaderia.Models.DTOs;

public class FinanciacionResumen
{
    public List<DeudaAporte> Deudas { get; set; } = [];
    public List<LoteFinanciado> Lotes { get; set; } = [];
    public List<ReservaReintegro> Reservas { get; set; } = [];
    public List<PagoReintegro> Pagos { get; set; } = [];
    public List<VentaLote> VentasNuevas { get; set; } = [];
    public Dictionary<int, int> UnidadesVendidas { get; set; } = [];
    public decimal GeneradoNuevo { get; set; }
    public decimal AdelantosPeriodo { get; set; }
}

public record DeudaAporte(AporteCompra Aporte, decimal Generado, decimal Pagado, decimal Reservado)
{
    public decimal Deuda => Aporte.Monto - Pagado;
    public decimal CreditoAdelantosAplicado { get; init; }
    public decimal Sugerido => Math.Max(0, Math.Min(Deuda - Reservado, Generado - Pagado - Reservado) - CreditoAdelantosAplicado);
    public decimal DisponibleAdelanto => Math.Max(0, Deuda - Reservado);
}
