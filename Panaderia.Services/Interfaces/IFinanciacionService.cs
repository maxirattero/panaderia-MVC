using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;

namespace Panaderia.Services.Interfaces;

public interface IFinanciacionService
{
    Task<FinanciacionResumen> ResumenAsync(DateTime? hasta = null, DateTime? desde = null);
    Task CrearLoteAsync(LoteFinanciado lote);
    Task AsignarAsync(int compra, int lote, decimal monto, Guid clave);
    Task PagarAsync(int aporte, int? reserva, CuentaCaja cuenta, decimal monto, Guid clave, string? usuario, DateTime? fechaPago = null);
}
