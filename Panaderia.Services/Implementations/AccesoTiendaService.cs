using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;
namespace Panaderia.Services.Implementations;

public class AccesoTiendaService(PanaderiaContext context, UserManager<ApplicationUser> users, IClienteService clientes) : IAccesoTiendaService
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
        await using var transaction = context.Database.CurrentTransaction == null
            ? await context.Database.BeginTransactionAsync() : null;
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
            if (transaction != null) await transaction.CommitAsync();
            return IdentityResult.Success;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Error("El cliente o correo ya tiene una cuenta. Actualizá la página para revisar su acceso.");
        }
    }
    public Task<bool> TieneAccesoAsync(int idCliente) => context.Users.AsNoTracking()
        .AnyAsync(u => u.IdCliente == idCliente && (!u.LockoutEnabled || u.LockoutEnd != DateTimeOffset.MaxValue));

    public async Task<IdentityResult> GuardarClienteAsync(Cliente cliente, bool habilitado, string? email, string? password)
    {
        var actual = await context.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cliente.Id);
        if (actual == null) return Error("El cliente ya no existe.");
        if (habilitado && !cliente.Revendedor) return Error("Marcá al cliente como revendedor para habilitar su acceso a la tienda.");
        var user = await context.Users.SingleOrDefaultAsync(u => u.IdCliente == cliente.Id);
        if (user != null && await users.IsInRoleAsync(user, "Admin"))
            return Error("No se puede modificar una cuenta administradora desde Clientes.");
        email = email?.Trim();
        if ((habilitado || user != null) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            return Error("Ingresá un correo electrónico válido para el acceso.");
        if (habilitado && user == null && string.IsNullOrEmpty(password))
            return Error("Ingresá una contraseña inicial para crear el acceso.");
        if (!string.IsNullOrEmpty(password) && (password.Length < 10 || password.Length > 128 || !password.Any(char.IsLower) || !password.Any(char.IsDigit)))
            return Error("La contraseña debe tener entre 10 y 128 caracteres, una letra minúscula y un número.");
        if (habilitado || user != null)
        {
            var porEmail = await users.FindByEmailAsync(email!);
            var porNombre = await users.FindByNameAsync(email!);
            if ((porEmail != null && porEmail.Id != user?.Id) || (porNombre != null && porNombre.Id != user?.Id))
                return Error("Ese correo ya está asociado a otra cuenta.");
        }
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // Los datos del cliente y sus credenciales se guardan o revierten juntos.
            await clientes.UpdateAsync(cliente);
            if (user == null && habilitado)
            {
                var created = await CrearAsync(cliente.Id, email!, password!);
                if (!created.Succeeded) return created;
            }
            else if (user != null)
            {
                var teniaAcceso = !user.LockoutEnabled || user.LockoutEnd != DateTimeOffset.MaxValue;
                var cambioEmail = user.Email != email || user.UserName != email;
                user.Email = user.UserName = email;
                user.NormalizedEmail = users.NormalizeEmail(email);
                user.NormalizedUserName = users.NormalizeName(email);
                user.NombreCompleto = cliente.NombreCompleto;
                user.LockoutEnabled = true;
                // El bloqueo permanente representa la baja de acceso; no confundirlo
                // con los cinco minutos de bloqueo por intentos fallidos de ingreso.
                if (!habilitado) user.LockoutEnd = DateTimeOffset.MaxValue;
                else if (!teniaAcceso || !string.IsNullOrEmpty(password))
                {
                    user.LockoutEnd = null;
                    user.AccessFailedCount = 0;
                }
                if (cambioEmail) user.EmailConfirmed = false;
                if (!string.IsNullOrEmpty(password))
                {
                    foreach (var validator in users.PasswordValidators)
                    {
                        var valid = await validator.ValidateAsync(users, user, password);
                        if (!valid.Succeeded) return valid;
                    }
                    user.PasswordHash = users.PasswordHasher.HashPassword(user, password);
                }
                if (cambioEmail || teniaAcceso != habilitado || !string.IsNullOrEmpty(password))
                    user.SecurityStamp = Guid.NewGuid().ToString();
                var updated = await users.UpdateAsync(user);
                if (!updated.Succeeded) return updated;
                if (habilitado && !await users.IsInRoleAsync(user, "Revendedor"))
                {
                    var role = await users.AddToRoleAsync(user, "Revendedor");
                    if (!role.Succeeded) return role;
                }
            }
            await transaction.CommitAsync();
            return IdentityResult.Success;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Error("El correo o cliente ya tiene una cuenta. Actualizá la página y revisá el acceso.");
        }
    }

    private static IdentityResult Error(string message) => IdentityResult.Failed(new IdentityError { Description = message });
}
