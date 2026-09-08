using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations;

public class ConfiguracionTiendaService(PanaderiaContext context) : IConfiguracionTiendaService
{
    // Sin caché: cada confirmación consulta la configuración vigente.
    public Task<ConfiguracionTienda> GetAsync() =>
        context.ConfiguracionTienda.AsNoTracking().SingleAsync(c => c.Id == 1);

    public async Task GuardarAsync(bool retiroHabilitado, decimal montoMinimoPedido)
    {
        if (montoMinimoPedido < 0 || montoMinimoPedido > 999999999m || decimal.Round(montoMinimoPedido, 2) != montoMinimoPedido)
            throw new ArgumentOutOfRangeException(nameof(montoMinimoPedido));

        var configuracion = await context.ConfiguracionTienda.SingleAsync(c => c.Id == 1);
        configuracion.RetiroHabilitado = retiroHabilitado;
        configuracion.MontoMinimoPedido = montoMinimoPedido;
        await context.SaveChangesAsync();
    }
}
