using Panaderia.Models.Entities;
using Panaderia.Models.Enums;

namespace Panaderia.Models.DTOs;

public class CajaResumen
{
    public CierreCaja Cierre { get; set; } = new();
    public bool Guardado => Cierre.Id != 0;
    public bool SaldosConfigurados { get; set; }
    public decimal ReservaPagada { get; set; }
    public decimal AniPagado { get; set; }
    public decimal MaxiPagado { get; set; }
    public List<SaldoCuentaCaja> SaldosActuales { get; set; } = [];
    public List<ReporteCaja> SinClasificar { get; set; } = [];
    public decimal Pendiente(DestinoCierre destino) => destino switch
    {
        DestinoCierre.Reserva => Cierre.Reserva - ReservaPagada,
        DestinoCierre.Ani => Cierre.Ani - AniPagado,
        _ => Cierre.Maxi - MaxiPagado
    };
}

public record SaldoCuentaCaja(CuentaCaja Cuenta, decimal Saldo);
