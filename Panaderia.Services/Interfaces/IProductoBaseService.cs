using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface IProductoBaseService
    {
        //Obtener todos los productos
        Task<IEnumerable<ProductoBase>> GetAllAsync();

        //obtener un producto por id
        Task<ProductoBase?> GetByIdAsync(int id);

        //Crear un nuevo producto
        Task CreateAsync(ProductoBase producto);

        //actualizar un producto existente
        Task UpdateAsync(ProductoBase producto);

        //eliminar un producto por id
        Task DeleteAsync(int id);

        //verificar si un producto existe por id
        Task<bool> ExistsAsync(int id);
    }
}