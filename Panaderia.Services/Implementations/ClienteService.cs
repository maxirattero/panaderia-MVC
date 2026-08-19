using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class ClienteService : IClienteService
    {
        private readonly PanaderiaContext _context;

        public ClienteService(PanaderiaContext context)
        {
            _context = context;
        }

        //listado de clientes
        public async Task<IEnumerable<Cliente>> GetAllAsync()
        {
            return await _context.Clientes
                .OrderBy(c => c.Nombre)
                .ThenBy(c => c.Apellido)
                .ToListAsync();
        }

        //obtener un cliente por su ID
        public async Task<Cliente?> GetByIdAsync(int id)
        {
            return await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id);
        }

        //crear un nuevo cliente
        public async Task CreateAsync(Cliente cliente)
        {
            await _context.Clientes.AddAsync(cliente);
            await _context.SaveChangesAsync();
        }

        //actualizar un cliente existente
        public async Task UpdateAsync(Cliente cliente)
        {
            var existe = await _context.Clientes.FindAsync(cliente.Id);
            if (existe == null) return;

            existe.Nombre = cliente.Nombre;
            existe.Apellido = cliente.Apellido;
            existe.Direccion = cliente.Direccion;
            existe.Localidad = cliente.Localidad;
            existe.Provincia = cliente.Provincia;
            existe.Telefono = cliente.Telefono;
            existe.Revendedor = cliente.Revendedor;
            existe.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        //eliminar un cliente por su ID
        public async Task DeleteAsync(int id)
        {
            await _context.Clientes.Where(c => c.Id == id).ExecuteDeleteAsync();
        }

        //verificar si un cliente existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Clientes.AnyAsync(c => c.Id == id);
        }

        //buscar un cliente por teléfono (comparación por dígitos, ignora espacios y guiones)
        public async Task<Cliente?> GetByTelefonoAsync(string telefono)
        {
            var buscado = SoloDigitos(telefono);
            if (string.IsNullOrEmpty(buscado)) return null;

            var clientes = await _context.Clientes
                .Where(c => c.Telefono != null && c.Telefono != "")
                .ToListAsync();

            return clientes.FirstOrDefault(c => SoloDigitos(c.Telefono!) == buscado);
        }

        private static string SoloDigitos(string valor)
        {
            return new string(valor.Where(char.IsDigit).ToArray());
        }
    }
}