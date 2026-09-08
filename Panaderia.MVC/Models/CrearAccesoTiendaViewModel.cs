using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
namespace Panaderia.MVC.Models;

public class CrearAccesoTiendaViewModel
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public int IdCliente { get; set; }
    [ValidateNever]
    public string NombreCliente { get; set; } = "";
    [Required(ErrorMessage = "Ingresá el correo para iniciar sesión.")]
    [EmailAddress(ErrorMessage = "Ingresá un correo electrónico válido.")]
    [StringLength(256)]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Ingresá una contraseña inicial.")]
    [StringLength(128, MinimumLength = 10, ErrorMessage = "Usá entre 10 y 128 caracteres.")]
    [RegularExpression(@"(?s)(?=.*[a-z])(?=.*[0-9]).+", ErrorMessage = "Incluí al menos una letra minúscula y un número.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    public string ConfirmarPassword { get; set; } = "";
}
