using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Panaderia.MVC.Models;

namespace Panaderia.MVC.Controllers;

[AllowAnonymous]
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
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Pedido");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl))
                return LocalRedirect(returnUrl);

            // Admin y Revendedor van a la parte administrativa; el resto, a la tienda.
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null &&
                (await _userManager.IsInRoleAsync(user, "Admin") ||
                 await _userManager.IsInRoleAsync(user, "Revendedor")))
            {
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
