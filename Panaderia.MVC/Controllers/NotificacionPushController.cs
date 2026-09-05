using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers;

[Authorize(Roles = "Admin")]
public class NotificacionPushController : Controller
{
    private readonly IPushNotificationService _pushNotificationService;
    private readonly PanaderiaContext _context;

    public NotificacionPushController(IPushNotificationService pushNotificationService, PanaderiaContext context)
    {
        _pushNotificationService = pushNotificationService;
        _context = context;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PedidosRecientes(int ultimoVisto = 0, CancellationToken cancellationToken = default)
    {
        var desde = DateTime.UtcNow.AddDays(-7);
        var consulta = _context.Pedidos.AsNoTracking()
            .Where(p => !p.Anulado && p.FechaCreacion >= desde);
        var ultimoId = await consulta.MaxAsync(p => (int?)p.Id, cancellationToken) ?? 0;
        // Mantener contador y lista en el mismo corte si ingresa otro pedido durante la consulta.
        consulta = consulta.Where(p => p.Id <= ultimoId);
        var nuevos = await consulta.CountAsync(p => p.Id > Math.Max(0, ultimoVisto), cancellationToken);
        var pedidos = await consulta.OrderByDescending(p => p.Id).Take(20)
            .Select(p => new
            {
                p.Id,
                Cliente = p.Cliente.Nombre + " " + (p.Cliente.Apellido ?? ""),
                p.MontoTotal,
                p.FechaCreacion
            }).ToListAsync(cancellationToken);
        return Json(new { ultimoId, nuevos, pedidos });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suscribir([FromBody] SuscripcionPushRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(request.Endpoint) ||
            string.IsNullOrWhiteSpace(request.Keys?.P256dh) ||
            string.IsNullOrWhiteSpace(request.Keys.Auth))
            return BadRequest();

        await _pushNotificationService.GuardarSuscripcionAsync(
            userId,
            new SuscripcionPushDto(request.Endpoint, request.Keys.P256dh, request.Keys.Auth));

        return Ok();
    }
}
