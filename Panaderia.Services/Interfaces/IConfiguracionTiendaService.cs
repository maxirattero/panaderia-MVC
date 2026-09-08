using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces;

public interface IConfiguracionTiendaService
{
    Task<ConfiguracionTienda> GetAsync();
    Task GuardarAsync(bool retiroHabilitado, decimal montoMinimoPedido);
}
