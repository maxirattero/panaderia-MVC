using System.ComponentModel.DataAnnotations;
using Panaderia.Models.DTOs;
using Panaderia.Models.Enums;

namespace Panaderia.MVC.Models;

public class CierreCajaViewModel
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public CajaResumen Resumen { get; set; } = new();
    public DateOnly InicioSemana { get; set; }
    [Range(0, 100)] public decimal PorcentajeReserva { get; set; } = 30m;
    [StringLength(1000)] public string? Notas { get; set; }
    public bool AceptarCostosPendientes { get; set; }
    public List<DateOnly> Semanas { get; set; } = [];
}
public class ConfiguracionCajaViewModel
{
    [Range(0, 100)] public decimal PorcentajeReserva { get; set; } = 30m;
    public DateTime? InicioSaldos { get; set; }
    [Range(0, double.MaxValue)] public decimal Efectivo { get; set; }
    [Range(0, double.MaxValue)] public decimal Banco { get; set; }
    [Range(0, double.MaxValue)] public decimal Reserva { get; set; }
    public bool AperturaGuardada { get; set; }
}
public class PagoCierreViewModel
{
    public int Id { get; set; }
    public DestinoCierre Destino { get; set; }
    public CuentaCaja Cuenta { get; set; }
    public decimal Monto { get; set; }
    public DateTime? Fecha { get; set; }
    public Guid Clave { get; set; }
}
public class TransferenciaCajaViewModel
{
    public CuentaCaja Origen { get; set; }
    public CuentaCaja Destino { get; set; }
    public decimal Monto { get; set; }
    public DateTime? Fecha { get; set; }
    public Guid Clave { get; set; } = Guid.NewGuid();
}
public static class CajaUi
{
    public static string Cuenta(CuentaCaja cuenta) => cuenta switch {
        CuentaCaja.Efectivo => "Efectivo", CuentaCaja.Banco => "Banco",
        CuentaCaja.ReservaMercadoPago => "Reserva Mercado Pago", _ => "Sin clasificar"
    };
    public static string Moneda(decimal importe) => importe.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("es-AR"));
}
