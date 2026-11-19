using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Services;

internal sealed class PrepararMiniaturasProductos(
    IServiceScopeFactory scopeFactory, ILogger<PrepararMiniaturasProductos> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var productos = scope.ServiceProvider.GetRequiredService<IProductoService>();
            await productos.CompletarMiniaturasAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron completar las miniaturas de los productos");
        }
    }
}
