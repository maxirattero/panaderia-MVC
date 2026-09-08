using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;
namespace Panaderia.Services.Implementations;

public class AccesoTiendaService(PanaderiaContext context, UserManager<ApplicationUser> users) : IAccesoTiendaService
{
    public Task<string?> ObtenerEmailAsync(int idCliente) => context.Users.AsNoTracking()
        .Where(u => u.IdCliente == idCliente).Select(u => u.Email).SingleOrDefaultAsync();

    public Task<Cliente?> ObtenerClienteAsync(string idUsuario) =>
        (from user in context.Users.AsNoTracking()
         join cliente in context.Clientes.AsNoTracking() on user.IdCliente equals cliente.Id
         where user.Id == idUsuario
         select cliente).SingleOrDefaultAsync();

    public async Task<IdentityResult> CrearAsync(int idCliente, string email, string password)
    {
        var cliente = await context.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.Id == idCliente);
        if (cliente == null || !cliente.Revendedor)
            return Error("Solo se puede crear acceso para un cliente marcado como revendedor.");
        email = email.Trim();
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            return Error("Ingresá un correo electrónico válido.");
        if (await context.Users.AnyAsync(u => u.IdCliente == idCliente))
            return Error("Este cliente ya tiene acceso a la tienda.");
        if (await users.FindByEmailAsync(email) != null || await users.FindByNameAsync(email) != null)
            return Error("Ese correo ya tiene una cuenta. No se modificó su acceso ni su contraseña.");
        if (!await context.Roles.AnyAsync(r => r.NormalizedName == "REVENDEDOR"))
            return Error("No está configurado el rol Revendedor. Contactá al administrador.");

        // Cuenta y rol se guardan juntos: nunca dejar un alta incompleta.
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var user = new ApplicationUser
            {
                UserName = email, Email = email, NombreCompleto = cliente.NombreCompleto,
                IdCliente = cliente.Id
            };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded) return created;
            var role = await users.AddToRoleAsync(user, "Revendedor");
            if (!role.Succeeded) return role;
            await transaction.CommitAsync();
            return IdentityResult.Success;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Error("El cliente o correo ya tiene una cuenta. Actualizá la página para revisar su acceso.");
        }
    }
    private static IdentityResult Error(string message) => IdentityResult.Failed(new IdentityError { Description = message });
}
