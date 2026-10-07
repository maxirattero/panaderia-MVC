using Microsoft.AspNetCore.Identity;
using Panaderia.Models.Entities;
namespace Panaderia.Services.Interfaces;

public interface IAccesoTiendaService
{
    Task<bool> TieneAccesoAsync(int idCliente);
    Task<IdentityResult> GuardarClienteAsync(Cliente cliente, bool habilitado, string? email, string? password);
    Task<string?> ObtenerEmailAsync(int idCliente);
    Task<Cliente?> ObtenerClienteAsync(string idUsuario);
    Task<IdentityResult> CrearAsync(int idCliente, string email, string password);
}
