using Microsoft.AspNetCore.Identity;

namespace Panaderia.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string? NombreCompleto { get; set; }
    public int? IdCliente { get; set; }
}
