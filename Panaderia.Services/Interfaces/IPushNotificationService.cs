using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces;

public interface IPushNotificationService
{
    Task GuardarSuscripcionAsync(string userId, SuscripcionPushDto suscripcion);
    Task NotificarNuevoPedidoAsync(Pedido pedido, bool esRevendedor);
}
