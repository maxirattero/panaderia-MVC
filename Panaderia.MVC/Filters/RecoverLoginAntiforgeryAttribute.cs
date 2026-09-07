using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Panaderia.MVC.Filters;

// Solo para el POST de login: descartar el formulario rechazado y comenzar un GET nuevo.
// Nunca ejecutar el inicio de sesión con un token inválido ni reenviar las credenciales.
[AttributeUsage(AttributeTargets.Method)]
public sealed class RecoverLoginAntiforgeryAttribute : Attribute, IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not IAntiforgeryValidationFailedResult) return;

        context.HttpContext.RequestServices
            .GetRequiredService<ILogger<RecoverLoginAntiforgeryAttribute>>()
            .LogInformation("Formulario de login rechazado por antiforgery; se redirige a un GET nuevo. Sesión activa: {Authenticated}",
                context.HttpContext.User.Identity?.IsAuthenticated == true);

        context.Result = new RedirectToActionResult("Login", "Account", new { formularioVencido = true });
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
