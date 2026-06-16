using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface IProductoService
    {
        //Obtener todos los Productos
        Task<IEnumerable<Producto>> GetAllAsync();

        //Obtener Producto por Id
        Task<Producto?> GetByIdAsync(int id);

        //Crear nuevo Producto
        Task CreateAsync(Producto producto);

        //Actualizar Producto existente
        Task UpdateAsync(Producto producto);

        //Eliminar Producto por Id
        Task DeleteAsync(int id);

        //Verificar si Producto existe por Id
        Task<bool> ExistsAsync(int id);
    }
}