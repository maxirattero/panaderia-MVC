using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class CategoriaService : ICategoriaService
    {
        private readonly PanaderiaContext _context;

        public CategoriaService(PanaderiaContext context)
        {
            _context = context;
        }

        //Listado Categorías
        public async Task<IEnumerable<CategoriaProducto>> GetAllAsync()
        {
            return await _context.CategoriasProducto.ToListAsync();
        }

        //Obtener Categoria por su Id
        public async Task<CategoriaProducto?> GetByIdAsync(int id)
        {
            return await _context.CategoriasProducto.FirstOrDefaultAsync(x => x.Id == id);
        }

        //Crear nueva Categoria
        public async Task CreateAsync(CategoriaProducto categoriaProducto)
        {
            await _context.CategoriasProducto.AddAsync(categoriaProducto);
            await _context.SaveChangesAsync();
        }

        //Actualizar Categoria existente
        public async Task UpdateAsync(CategoriaProducto categoriaProducto)
        {
            var existe = await _context.CategoriasProducto.FindAsync(categoriaProducto.Id);
            if (existe == null) return;

            existe.Nombre = categoriaProducto.Nombre;            
            existe.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        //Eliminar Categoria por Id
        public async Task DeleteAsync(int id)
        {
            await _context.CategoriasProducto.Where(c => c.Id == id).ExecuteDeleteAsync();
        }

        //Verificar si Categoria existe por Id
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.CategoriasProducto.AnyAsync(c => c.Id == id);
        }
    }
}