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
            return await _context.Clientes.ToListAsync();
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
            cliente.FechaModificacion = DateTime.Now;
            _context.Clientes.Update(cliente);
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
    }
}