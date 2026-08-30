using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.DTOs;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers;

[Authorize(Roles = "Admin")]
public class NotificacionPushController : Controller
{
    private readonly IPushNotificationService _pushNotificationService;

    public NotificacionPushController(IPushNotificationService pushNotificationService)
    {
        _pushNotificationService = pushNotificationService;
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
