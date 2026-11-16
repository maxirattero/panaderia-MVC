using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
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
                .AsSplitQuery()
                .Include(p => p.Materiales).ThenInclude(m => m.Insumo)
                .Include(p => p.EmpaquePredeterminado)
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
                .AsSplitQuery()
                .Include(p => p.Materiales).ThenInclude(m => m.Insumo)
                .Include(p => p.EmpaquePredeterminado)
                .Include(p => p.Categoria)
                .Include(p => p.Formato)
                .Include(p => p.Tamano)
                .Include(p => p.ProductoEtiquetas).ThenInclude(pe => pe.Etiqueta)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        //Crear nuevo Producto
        public async Task CreateAsync(Producto producto)
        {
            producto.Materiales = await ValidarMaterialesAsync(producto);
            producto.OcultoEnTienda = true;
            AplicarEstadoStock(producto);
            await _context.Productos.AddAsync(producto);
            await _context.SaveChangesAsync();
        }

        //Actualizar Producto existente
        public async Task UpdateAsync(Producto producto)
        {
            var materiales = await ValidarMaterialesAsync(producto);
            var existe = await _context.Productos.Include(p => p.Materiales).FirstOrDefaultAsync(p => p.Id == producto.Id);
            if (existe == null) return;

            existe.IdCategoria = producto.IdCategoria;
            existe.Masa = producto.Masa;
            existe.Variedad = producto.Variedad;
            existe.IdFormato = producto.IdFormato;
            existe.IdTamano = producto.IdTamano;
            existe.Nombre = producto.Nombre;
            existe.PrecioFinal = producto.PrecioFinal;
            existe.PrecioReventa = producto.PrecioReventa;
            existe.CostoManual = producto.CostoManual;
            existe.Stock = producto.Stock;
            existe.PorEncargo = producto.PorEncargo;
            existe.TieneDisponibilidadSemanal = producto.TieneDisponibilidadSemanal;
            existe.IdEmpaquePredeterminado = producto.IdEmpaquePredeterminado;
            existe.EtiquetaPredeterminada = producto.EtiquetaPredeterminada;
            var anteriores = await _context.ProductosMateriales.Where(m => m.IdProducto == existe.Id).ToListAsync();
            // Un llamador también puede pasar la entidad trackeada con filas nuevas aún sin guardar.
            foreach (var nuevo in existe.Materiales.Where(m => _context.Entry(m).State == EntityState.Added || m.Id <= 0).ToList())
                _context.Entry(nuevo).State = EntityState.Detached;
            _context.ProductosMateriales.RemoveRange(anteriores);
            existe.Materiales.Clear();
            existe.Materiales.AddRange(materiales);
            existe.DescripcionTienda = producto.DescripcionTienda;
            existe.Ingredientes = producto.Ingredientes;
            existe.ImagenURL = producto.ImagenURL;
            existe.ObservacionesElaboracion = producto.ObservacionesElaboracion;
            AplicarEstadoStock(existe);
            existe.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();

        }

        public async Task<Producto?> DuplicarAsync(int id)
        {
            var origen = await _context.Productos
                .Include(p => p.Materiales)
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
                CostoManual = origen.CostoManual,
                Stock = 0,
                OcultoEnTienda = true,
                PorEncargo = origen.PorEncargo,
                TieneDisponibilidadSemanal = origen.TieneDisponibilidadSemanal,
                IdEmpaquePredeterminado = origen.IdEmpaquePredeterminado,
                EtiquetaPredeterminada = origen.EtiquetaPredeterminada,
                Materiales = origen.Materiales.Select(m => new ProductoMaterial { IdInsumo = m.IdInsumo, Cantidad = m.Cantidad }).ToList(),
                DescripcionTienda = origen.DescripcionTienda,
                Ingredientes = origen.Ingredientes,
                ObservacionesElaboracion = origen.ObservacionesElaboracion,
                FechaCreacion = DateTime.UtcNow
            };
            AplicarEstadoStock(copia);

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

        private async Task<List<ProductoMaterial>> ValidarMaterialesAsync(Producto producto)
        {
            var materiales = producto.Materiales ?? [];
            if (materiales.Count > 50 || materiales.GroupBy(x => x.IdInsumo).Any(g => g.Count() > 1)
                || materiales.Any(m => m.IdInsumo <= 0 || m.Cantidad <= 0 || m.Cantidad > 999999.9999m || m.Cantidad != decimal.Round(m.Cantidad, 4)))
                throw new InvalidOperationException("Revisá los materiales: no pueden repetirse y cada cantidad debe ser positiva, con hasta cuatro decimales.");
            var ids = materiales.Select(m => m.IdInsumo).ToList();
            if (producto.IdEmpaquePredeterminado.HasValue) ids.Add(producto.IdEmpaquePredeterminado.Value);
            var insumos = await _context.Insumos.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            if (materiales.Any(m => !insumos.TryGetValue(m.IdInsumo, out var i) || !i.Activo || i.CantidadRendimiento <= 0
                || i.TipoInsumo is not (TipoInsumo.Empaque or TipoInsumo.Consumible or TipoInsumo.Etiqueta)))
                throw new InvalidOperationException("Elegí materiales activos de tipo empaque, etiqueta o consumible, con rendimiento válido.");
            if (materiales.Count(m => insumos[m.IdInsumo].TipoInsumo == TipoInsumo.Etiqueta) > 1)
                throw new InvalidOperationException("Elegí una sola etiqueta para el producto. Su inclusión se controla desde el pedido.");
            if (producto.IdEmpaquePredeterminado is int bolsa && (!insumos.TryGetValue(bolsa, out var b) || !b.Activo || b.TipoInsumo != TipoInsumo.Empaque || b.CantidadRendimiento <= 0))
                throw new InvalidOperationException("La bolsa predeterminada debe ser un empaque activo con rendimiento válido.");
            return materiales.Select(m => new ProductoMaterial { IdInsumo = m.IdInsumo, Cantidad = m.Cantidad }).ToList();
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

        //Marcar/desmarcar Producto como "por encargo" en la tienda
        public async Task TogglePorEncargoAsync(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return;

            producto.PorEncargo = !producto.PorEncargo;
            AplicarEstadoStock(producto);
            producto.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private static void AplicarEstadoStock(Producto producto)
        {
            producto.SinStock = !producto.PorEncargo && producto.Stock <= 0;
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
