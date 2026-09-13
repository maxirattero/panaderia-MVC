using Panaderia.Models.Enums;

namespace Panaderia.Models.Entities;

public class CierreCaja
{
    public int Id { get; set; }
    public DateOnly InicioSemana { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public DateTime RegistradoUtc { get; set; }
    public string? Usuario { get; set; }
    public string? Notas { get; set; }
    public bool EsHistorico { get; set; }
    public int? IdMovimientoHistorico { get; set; }
    public decimal? RetiroHistorico { get; set; }
    public decimal? PorcentajeReserva { get; set; }
    public decimal CobrosEfectivo { get; set; }
    public decimal CobrosBanco { get; set; }
    public decimal CobrosSinClasificar { get; set; }
    public decimal Devoluciones { get; set; }
    public decimal GastosReparto { get; set; }
    public decimal BaseReparto { get; set; }
    public decimal Reserva { get; set; }
    public decimal Ani { get; set; }
    public decimal Maxi { get; set; }
    public decimal? TotalVendido { get; set; }
    public decimal? CostoInsumos { get; set; }
    public List<CierreCajaCosto> Costos { get; set; } = [];
    public List<CierreCajaSaldo> Saldos { get; set; } = [];
}

public class CierreCajaCosto
{
    public int Id { get; set; }
    public int IdCierre { get; set; }
    public CierreCaja Cierre { get; set; } = null!;
    public int IdProducto { get; set; }
    public string Producto { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal Ingredientes { get; set; }
    public decimal Empaque { get; set; }
    public bool CostoPendiente { get; set; }
    public bool Reconstruido { get; set; }
}

public class CierreCajaSaldo
{
    public int Id { get; set; }
    public int IdCierre { get; set; }
    public CierreCaja Cierre { get; set; } = null!;
    public CuentaCaja Cuenta { get; set; }
    public decimal Inicial { get; set; }
    public decimal Entradas { get; set; }
    public decimal Salidas { get; set; }
    public decimal Final { get; set; }
}
