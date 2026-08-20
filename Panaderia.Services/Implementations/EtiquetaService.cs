using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class EtiquetaService : IEtiquetaService
    {
        private readonly PanaderiaContext _context;

        public EtiquetaService(PanaderiaContext context)
        {
            _context = context;
        }

        //Listado Etiquetas (orden alfabético con acentos y ñ)
        public async Task<IEnumerable<Etiqueta>> GetAllAsync()
        {
            var etiquetas = await _context.Etiquetas.ToListAsync();

            var comparer = StringComparer.Create(new CultureInfo("es-AR"), ignoreCase: true);

            return etiquetas
                .AsEnumerable()
                .OrderBy(e => e.Nombre, comparer)
                .ToList();
        }

        //Obtener Etiqueta por su Id
        public async Task<Etiqueta?> GetByIdAsync(int id)
        {
            return await _context.Etiquetas.FirstOrDefaultAsync(e => e.Id == id);
        }

        //Crear nueva Etiqueta
        public async Task CreateAsync(Etiqueta etiqueta)
        {
            await _context.Etiquetas.AddAsync(etiqueta);
            await _context.SaveChangesAsync();
        }

        //Actualizar Etiqueta existente
        public async Task UpdateAsync(Etiqueta etiqueta)
        {
            var existe = await _context.Etiquetas.FindAsync(etiqueta.Id);
            if (existe == null) return;

            existe.Nombre = etiqueta.Nombre;
            existe.Icono = etiqueta.Icono;
            existe.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        //Eliminar Etiqueta por Id (las asignaciones caen por cascade en la DB)
        public async Task DeleteAsync(int id)
        {
            await _context.Etiquetas.Where(e => e.Id == id).ExecuteDeleteAsync();
        }

        //Verificar si Etiqueta existe por Id
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Etiquetas.AnyAsync(e => e.Id == id);
        }

        //Verificar si ya existe una Etiqueta con ese nombre (ignora mayúsculas)
        public async Task<bool> ExisteNombreAsync(string nombre)
        {
            var buscado = nombre.Trim();
            return await _context.Etiquetas
                .AnyAsync(e => e.Nombre.ToLower() == buscado.ToLower());
        }

        //Ids de las Etiquetas asignadas a un Producto
        public async Task<List<int>> GetIdsPorProductoAsync(int idProducto)
        {
            return await _context.ProductoEtiquetas
                .Where(pe => pe.IdProducto == idProducto)
                .Select(pe => pe.IdEtiqueta)
                .ToListAsync();
        }

        //Reemplazar las Etiquetas asignadas a un Producto
        public async Task AsignarAProductoAsync(int idProducto, IEnumerable<int> idsEtiquetas)
        {
            var actuales = await _context.ProductoEtiquetas
                .Where(pe => pe.IdProducto == idProducto)
                .ToListAsync();

            _context.ProductoEtiquetas.RemoveRange(actuales);

            var nuevas = idsEtiquetas?.Distinct().ToList() ?? new List<int>();
            foreach (var idEtiqueta in nuevas)
            {
                _context.ProductoEtiquetas.Add(new ProductoEtiqueta
                {
                    IdProducto = idProducto,
                    IdEtiqueta = idEtiqueta
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
