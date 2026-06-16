using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class FormatoService : IFormatoService
    {
        private readonly PanaderiaContext _context;
        public FormatoService(PanaderiaContext context)
        {
            _context = context;
        }

        //listado de Formatos
        public async Task<IEnumerable<Formato>> GetAllAsync()
        {
            return await _context.Formatos.ToListAsync();
        }

        //obtener un Formato por su ID
        public async Task<Formato?> GetByIdAsync(int id)
        {
            return await _context.Formatos.FirstOrDefaultAsync(c => c.Id == id);
        }

        //crear un nuevo Formato
        public async Task CreateAsync(Formato formato)
        {
            await _context.Formatos.AddAsync(formato);
            await _context.SaveChangesAsync();
        }

        //actualizar un Formato existente
        public async Task UpdateAsync(Formato formato)
        {
            var existe = await _context.Formatos.FindAsync(formato.Id);
            if (existe == null) return;

            existe.Descripcion = formato.Descripcion;
            await _context.SaveChangesAsync();
        }

        //eliminar un Formato por su ID
        public async Task DeleteAsync(int id)
        {
            await _context.Formatos.Where(c => c.Id == id).ExecuteDeleteAsync();
        }

        //verificar si un Formato existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Formatos.AnyAsync(c => c.Id == id);
        }
    }
}
