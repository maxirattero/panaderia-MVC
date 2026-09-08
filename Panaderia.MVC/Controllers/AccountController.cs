using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Panaderia.MVC.Models;
using Panaderia.MVC.Filters;

namespace Panaderia.MVC.Controllers;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool formularioVencido = false)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!User.IsInRole("Admin")) return RedirectToAction("Index", "Tienda");
            if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
            return RedirectToAction("Index", "Home");
        }
        if (formularioVencido)
            ModelState.AddModelError(string.Empty, "El formulario quedó desactualizado. Volvé a ingresar tus datos para iniciar sesión.");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RecoverLoginAntiforgery]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Solo el admin puede volver al panel. Evita bucles al denegar acceso.
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
            {
                if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Index", "Tienda");
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Cuenta bloqueada por demasiados intentos. Intentá en unos minutos."
            : "Email o contraseña incorrectos.");

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Tienda");
    }
}
