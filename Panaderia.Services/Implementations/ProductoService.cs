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
                .Include(p => p.ProductoEtiquetas).ThenInclude(pe => pe.Etiqueta)
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
                .Include(p => p.ProductoEtiquetas).ThenInclude(pe => pe.Etiqueta)
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
            existe.ObservacionesElaboracion = producto.ObservacionesElaboracion;
            existe.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();

        }

        public async Task<Producto?> DuplicarAsync(int id)
        {
            var origen = await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.ProductoEtiquetas)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (origen == null) return null;

            var recetaOrigen = await _context.Recetas
                .Include(r => r.Detalles)
                .FirstOrDefaultAsync(r => r.IdProducto == id);
            var imagenOrigen = await _context.ProductoImagenes
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IdProducto == id);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var copia = new Producto
            {
                IdCategoria = origen.IdCategoria,
                Masa = origen.Masa,
                Variedad = origen.Variedad,
                IdFormato = origen.IdFormato,
                IdTamano = origen.IdTamano,
                Nombre = $"{origen.NombreVisible} (copia)",
                PrecioFinal = origen.PrecioFinal,
                PrecioReventa = origen.PrecioReventa,
                Stock = 0,
                OcultoEnTienda = true,
                SinStock = false,
                PorEncargo = origen.PorEncargo,
                ObservacionesElaboracion = origen.ObservacionesElaboracion,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Productos.Add(copia);
            await _context.SaveChangesAsync();

            foreach (var etiqueta in origen.ProductoEtiquetas)
                _context.ProductoEtiquetas.Add(new ProductoEtiqueta
                {
                    IdProducto = copia.Id,
                    IdEtiqueta = etiqueta.IdEtiqueta
                });

            if (recetaOrigen != null)
            {
                _context.Recetas.Add(new Receta
                {
                    IdProducto = copia.Id,
                    TamanioLote = recetaOrigen.TamanioLote,
                    PesoUnitario = recetaOrigen.PesoUnitario,
                    FechaCreacion = DateTime.UtcNow,
                    Detalles = recetaOrigen.Detalles.Select(d => new RecetaDetalle
                    {
                        IdInsumo = d.IdInsumo,
                        IdSubReceta = d.IdSubReceta,
                        PorcentajePanadero = d.PorcentajePanadero,
                        CantidadFija = d.CantidadFija
                    }).ToList()
                });
            }

            if (imagenOrigen != null)
            {
                _context.ProductoImagenes.Add(new ProductoImagen
                {
                    IdProducto = copia.Id,
                    Datos = imagenOrigen.Datos,
                    ContentType = imagenOrigen.ContentType,
                    FechaCreacion = DateTime.UtcNow
                });
                copia.ImagenURL = $"/Tienda/Imagen/{copia.Id}?v={DateTime.UtcNow.Ticks}";
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return copia;
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

        //Marcar/desmarcar Producto como "por encargo" en la tienda
        public async Task TogglePorEncargoAsync(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return;

            producto.PorEncargo = !producto.PorEncargo;
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
