using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class ProductoFinalService : IProductoFinalService
    {
        private readonly PanaderiaContext _context;

        public ProductoFinalService(PanaderiaContext context)
        {
            _context = context;
        }

        //listado de productos finales incluyendo su producto base, tamano y formato
        public async Task<IEnumerable<ProductoFinal>> GetAllAsync()
        {
            return await _context.ProductosFinales
                .Include(p => p.ProductoBase)
                .Include(p => p.Tamano)
                .Include(p => p.Formato)
                .ToListAsync();
        }

        //obtener productos finales por categoria (a través del producto base)
        public async Task<IEnumerable<ProductoFinal>> GetByCategoriaAsync(int idCategoria)
        {
            return await _context.ProductosFinales
                .Include(p => p.ProductoBase)
                .Include(p => p.Tamano)
                .Include(p => p.Formato)
                .Where(p => p.ProductoBase.IdCategoria == idCategoria)
                .ToListAsync();
        }

        //obtener un producto final por su ID
        public async Task<ProductoFinal?> GetByIdAsync(int id)
        {
            return await _context.ProductosFinales
                .Include(p => p.ProductoBase)
                .Include(p => p.Tamano)
                .Include(p => p.Formato)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        //crear un nuevo producto final
        public async Task CreateAsync(ProductoFinal producto)
        {
            await _context.ProductosFinales.AddAsync(producto);
            await _context.SaveChangesAsync();
        }

        //actualizar un producto final existente
        public async Task UpdateAsync(ProductoFinal producto)
        {
            producto.FechaModificacion = DateTime.Now;
            _context.ProductosFinales.Update(producto);
            await _context.SaveChangesAsync();
        }

        //eliminar un producto final por su ID
        public async Task DeleteAsync(int id)
        {
            await _context.ProductosFinales.Where(p => p.Id == id).ExecuteDeleteAsync();
        }

        //verificar si un producto final existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.ProductosFinales.AnyAsync(p => p.Id == id);
        }
    }
}