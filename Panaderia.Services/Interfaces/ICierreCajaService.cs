using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;

namespace Panaderia.Services.Interfaces;

public interface ICierreCajaService
{
    Task<ConfiguracionCaja> ConfiguracionAsync();
    Task ConfigurarAsync(decimal porcentaje, DateTime? inicio, decimal efectivo, decimal banco, decimal reserva, string? usuario);
    Task<List<SaldoCuentaCaja>> SaldosAsync(DateTime? hasta = null);
    Task<CajaResumen> ResumenAsync(DateOnly semana);
    Task<CajaResumen> DetalleAsync(int id);
    Task<List<CierreCaja>> HistorialAsync();
    Task<int> CerrarAsync(DateOnly semana, decimal porcentaje, string? notas, string? usuario, bool aceptarCostosPendientes);
    Task RegistrarPagoAsync(int id, DestinoCierre destino, CuentaCaja cuenta, decimal monto, DateTime fecha, Guid clave);
    Task TransferirAsync(CuentaCaja origen, CuentaCaja destino, decimal monto, DateTime fecha, Guid clave);
    Task ClasificarAsync(int idMovimiento, CuentaCaja cuenta);
}
