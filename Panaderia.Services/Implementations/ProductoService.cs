using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class ProductoService : IProductoService
    {
        private readonly PanaderiaContext _context;

        public ProductoService(PanaderiaContext context)
        {
            _context = context;
        }

        //Listado Productos
        public async Task<IEnumerable<Producto>> GetAllAsync()
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Formato)
                .Include(p => p.Tamano)
                .ToListAsync();
        }

        //Obtener Producto por su Id
        public async Task<Producto?> GetByIdAsync(int id)
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Formato)
                .Include(p => p.Tamano)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        //Crear nuevo Producto
        public async Task CreateAsync(Producto producto)
        {
            await _context.Productos.AddAsync(producto);
            await _context.SaveChangesAsync();
        }

        //Actualizar Producto existente
        public async Task UpdateAsync(Producto producto)
        {
            var existe = await _context.Productos.FindAsync(producto.Id);
            if (existe == null) return;

            existe.IdCategoria = producto.IdCategoria;
            existe.Masa = producto.Masa;
            existe.Variedad = producto.Variedad;
            existe.IdFormato = producto.IdFormato;
            existe.IdTamano = producto.IdTamano;
            existe.Nombre = producto.Nombre;
            existe.PrecioFinal = producto.PrecioFinal;
            existe.PrecioReventa = producto.PrecioReventa;
            existe.Stock = producto.Stock;
            existe.ImagenURL = producto.ImagenURL;
            existe.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();

        }

        //Eliminar Producto por Id
        public async Task DeleteAsync(int id)
        {
            await _context.Productos.Where(c => c.Id == id).ExecuteDeleteAsync();
        }

        //Verificar si Producto existe por Id
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Productos.AnyAsync(c => c.Id == id);
        }
    }
}