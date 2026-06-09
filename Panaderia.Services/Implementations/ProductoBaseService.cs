using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class ProductoBaseService : IProductoBaseService
    {
        private readonly PanaderiaContext _context;

        public ProductoBaseService(PanaderiaContext context)
        {
            _context = context;
        }

        //Listado de productos
        public async Task<IEnumerable<ProductoBase>> GetAllAsync()
        {
            return await _context.ProductosBase
                .Include(p => p.Categoria) // Incluir la categoría relacionada
                .ToListAsync();
        }

        //Obtener un producto por su ID
        public async Task<ProductoBase?> GetByIdAsync(int id)
        {
            return await (_context.ProductosBase.FirstOrDefaultAsync(p => p.Id == id));
        }

        //Crear un nuevo producto
        public async Task CreateAsync(ProductoBase producto)
        {
            await _context.ProductosBase.AddAsync(producto);
            await _context.SaveChangesAsync();
        }

        //Actualizar un producto existente
        public async Task UpdateAsync(ProductoBase producto)
        {
            producto.FechaModificacion = DateTime.Now;
            _context.ProductosBase.Update(producto);
            await _context.SaveChangesAsync();
        }

        //Eliminar un producto por su ID
        public async Task DeleteAsync(int id)
        {
            await _context.ProductosBase.Where(p => p.Id == id).ExecuteDeleteAsync();
        }

        // Verificar si un producto existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.ProductosBase.AnyAsync(p => p.Id == id);
        }
    }
}