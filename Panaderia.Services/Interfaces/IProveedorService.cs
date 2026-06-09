using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface IProveedorService
    {
        //Obtener todos los proveedores
        Task<IEnumerable<Proveedor>> GetAllAsync();

        //obtener un proveedor por id
        Task<Proveedor?> GetByIdAsync(int id);

        //Crear un nuevo proveedor
        Task CreateAsync(Proveedor proveedor);

        //actualizar un proveedor existente
        Task UpdateAsync(Proveedor proveedor);

        //eliminar un proveedor por id
        Task DeleteAsync(int id);

        //verificar si un proveedor existe por id
        Task<bool> ExistsAsync(int id);
    }
}
