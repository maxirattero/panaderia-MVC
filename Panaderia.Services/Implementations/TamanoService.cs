using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class TamanoService : ITamanoService
    {
        private readonly PanaderiaContext _context;
        public TamanoService(PanaderiaContext context)
        {
            _context = context;
        }

        //listado de Tamaños
        public async Task<IEnumerable<Tamano>> GetAllAsync()
        {
            return await _context.Tamanos.ToListAsync();
        }

        //obtener un Tamaño por su ID
        public async Task<Tamano?> GetByIdAsync(int id)
        {
            return await _context.Tamanos.FirstOrDefaultAsync(c => c.Id == id);
        }

        //crear un nuevo Tamaño
        public async Task CreateAsync(Tamano tamano)
        {
            await _context.Tamanos.AddAsync(tamano);
            await _context.SaveChangesAsync();
        }

        //actualizar un Tamaño existente
        public async Task UpdateAsync(Tamano tamano)
        {
            var existe = await _context.Tamanos.FindAsync(tamano.Id);
            if (existe == null) return;

            existe.Descripcion = tamano.Descripcion;
            await _context.SaveChangesAsync();
        }

        //eliminar un Tamaño por su ID
        public async Task DeleteAsync(int id)
        {
            await _context.Tamanos.Where(c => c.Id == id).ExecuteDeleteAsync();
        }

        //verificar si un Tamaño existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Tamanos.AnyAsync(c => c.Id == id);
        }
    }
}