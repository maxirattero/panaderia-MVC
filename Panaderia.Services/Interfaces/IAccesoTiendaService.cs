using Microsoft.AspNetCore.Identity;
using Panaderia.Models.Entities;
namespace Panaderia.Services.Interfaces;

public interface IAccesoTiendaService
{
    Task<string?> ObtenerEmailAsync(int idCliente);
    Task<Cliente?> ObtenerClienteAsync(string idUsuario);
    Task<IdentityResult> CrearAsync(int idCliente, string email, string password);
}
