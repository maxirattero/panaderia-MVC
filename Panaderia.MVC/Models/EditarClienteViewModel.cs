using System.ComponentModel.DataAnnotations;
using Panaderia.Models.Entities;
namespace Panaderia.MVC.Models;

public class EditarClienteViewModel : Cliente
{
    public bool AccesoTienda { get; set; }
    [EmailAddress(ErrorMessage = "Ingresá un correo electrónico válido.")]
    [StringLength(256)]
    public string? EmailAcceso { get; set; }
    [StringLength(128, MinimumLength = 10, ErrorMessage = "Usá entre 10 y 128 caracteres.")]
    [RegularExpression(@"(?s)(?=.*[a-z])(?=.*[0-9]).+", ErrorMessage = "Incluí una letra minúscula y un número.")]
    [DataType(DataType.Password)]
    public string? NuevaPassword { get; set; }
    [Compare(nameof(NuevaPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    public string? ConfirmarPassword { get; set; }
}
