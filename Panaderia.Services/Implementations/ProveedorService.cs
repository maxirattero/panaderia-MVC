using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class ProveedorService : IProveedorService
    {
        private readonly PanaderiaContext _context;

        public ProveedorService(PanaderiaContext context)
        {
            _context = context;
        }

        //Listado de proveedores
        public async Task<IEnumerable<Proveedor>> GetAllAsync()
        {
            return await _context.Proveedores.ToListAsync();
        }

        //Obtener un proveedor por su ID
        public async Task<Proveedor?> GetByIdAsync(int id)
        {
            return await (_context.Proveedores.FirstOrDefaultAsync(p => p.Id == id));
        }

        //Crear un nuevo proveedor
        public async Task CreateAsync(Proveedor proveedor)
        {
            await _context.Proveedores.AddAsync(proveedor);
            await _context.SaveChangesAsync();
        }

        //Actualizar un proveedor existente
        public async Task UpdateAsync(Proveedor proveedor)
        {
            var existe = await _context.Proveedores.FindAsync(proveedor.Id);
            if (existe == null) return;

            existe.Nombre = proveedor.Nombre;
            existe.Contacto = proveedor.Contacto;
            existe.Telefono = proveedor.Telefono;
            existe.Email = proveedor.Email;
            existe.Direccion = proveedor.Direccion;
            existe.Notas = proveedor.Notas;
            existe.Activo = proveedor.Activo;
            existe.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        //Eliminar un proveedor por su ID
        public async Task DeleteAsync(int id)
        {
            await _context.Proveedores.Where(p => p.Id == id).ExecuteDeleteAsync();
        }

        // Verificar si un proveedor existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Proveedores.AnyAsync(p => p.Id == id);
        }
    }
}
