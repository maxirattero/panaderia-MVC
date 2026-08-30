using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;
using WebPush;

namespace Panaderia.Services.Implementations;

public class PushNotificationService : IPushNotificationService
{
    private readonly PanaderiaContext _context;
    private readonly PushNotificationOptions _options;

    public PushNotificationService(PanaderiaContext context, PushNotificationOptions options)
    {
        _context = context;
        _options = options;
    }

    public async Task GuardarSuscripcionAsync(string userId, SuscripcionPushDto suscripcion)
    {
        var existente = await _context.SuscripcionesPush
            .FirstOrDefaultAsync(s => s.Endpoint == suscripcion.Endpoint);

        if (existente == null)
        {
            _context.SuscripcionesPush.Add(new SuscripcionPush
            {
                UserId = userId,
                Endpoint = suscripcion.Endpoint,
                P256dh = suscripcion.P256dh,
                Auth = suscripcion.Auth,
                FechaCreacion = DateTime.UtcNow,
                FechaModificacion = DateTime.UtcNow
            });
        }
        else
        {
            existente.UserId = userId;
            existente.P256dh = suscripcion.P256dh;
            existente.Auth = suscripcion.Auth;
            existente.FechaModificacion = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task NotificarNuevoPedidoAsync(Pedido pedido, bool esRevendedor)
    {
        // Sin claves no falla la carga del pedido: la configuración se completa en Railway.
        if (!_options.EstaConfigurado) return;

        var adminRoleId = await _context.Roles
            .Where(r => r.NormalizedName == "ADMIN")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();
        if (adminRoleId == null) return;

        var suscripciones = await _context.SuscripcionesPush
            .Where(s => _context.UserRoles.Any(ur => ur.UserId == s.UserId && ur.RoleId == adminRoleId))
            .ToListAsync();
        if (suscripciones.Count == 0) return;

        var tipo = esRevendedor ? "revendedor" : "cliente";
        var payload = JsonSerializer.Serialize(new
        {
            title = $"Nuevo pedido de {tipo}",
            body = $"{pedido.Cliente.NombreCompleto} · Pedido #{pedido.Id}",
            url = $"/Pedido/Details/{pedido.Id}"
        });
        var vapid = new VapidDetails(_options.Subject, _options.VapidPublicKey, _options.VapidPrivateKey);
        var webPush = new WebPushClient();
        var vencidas = new List<SuscripcionPush>();

        foreach (var suscripcion in suscripciones)
        {
            try
            {
                await webPush.SendNotificationAsync(
                    new PushSubscription(suscripcion.Endpoint, suscripcion.P256dh, suscripcion.Auth),
                    payload,
                    vapid);
            }
            catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
            {
                // El navegador dio de baja el dispositivo: se elimina para no reintentar.
                vencidas.Add(suscripcion);
            }
            catch (WebPushException)
            {
                // Un proveedor de push temporalmente caído no debe impedir tomar el pedido.
            }
        }

        if (vencidas.Count > 0)
        {
            _context.SuscripcionesPush.RemoveRange(vencidas);
            await _context.SaveChangesAsync();
        }
    }
}
