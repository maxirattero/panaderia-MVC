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
            var productos = await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Formato)
                .Include(p => p.Tamano)
                .ToListAsync();

            var comparer = StringComparer.Create(new System.Globalization.CultureInfo("es-AR"), ignoreCase: true);

            return productos
                .AsEnumerable()
                .OrderBy(p => p.NombreVisible, comparer)
                .ToList();
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

        //Mostrar/ocultar Producto en la tienda pública
        public async Task ToggleOcultoEnTiendaAsync(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return;

            producto.OcultoEnTienda = !producto.OcultoEnTienda;
            producto.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        //Marcar/desmarcar Producto como sin stock en la tienda
        public async Task ToggleSinStockAsync(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return;

            producto.SinStock = !producto.SinStock;
            producto.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        //Guardar (crear o reemplazar) la imagen del Producto
        public async Task GuardarImagenAsync(int idProducto, byte[] datos, string contentType)
        {
            var producto = await _context.Productos.FindAsync(idProducto);
            if (producto == null) return;

            var imagen = await _context.ProductoImagenes
                .FirstOrDefaultAsync(i => i.IdProducto == idProducto);

            if (imagen == null)
            {
                imagen = new ProductoImagen
                {
                    IdProducto = idProducto,
                    FechaCreacion = DateTime.UtcNow
                };
                await _context.ProductoImagenes.AddAsync(imagen);
            }
            else
            {
                imagen.FechaModificacion = DateTime.UtcNow;
            }

            imagen.Datos = datos;
            imagen.ContentType = contentType;

            // La tienda sirve la imagen desde la DB vía /Tienda/Imagen/{id}.
            // El ?v= (ticks) burla la caché del navegador al reemplazar la imagen.
            producto.ImagenURL = $"/Tienda/Imagen/{idProducto}?v={DateTime.UtcNow.Ticks}";
            producto.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        //Obtener la imagen del Producto (o null si no tiene)
        public async Task<ProductoImagen?> GetImagenAsync(int idProducto)
        {
            return await _context.ProductoImagenes
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IdProducto == idProducto);
        }
    }
}