using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface IProductoFinalService
    {
        //Obtener todos los productos finales
        Task<IEnumerable<ProductoFinal>> GetAllAsync();

        //obtener un producto final por id
        Task<ProductoFinal?> GetByIdAsync(int id);

        //obtener productos finales por categoria
        Task<IEnumerable<ProductoFinal>> GetByCategoriaAsync(int idCategoria);

        //crear un nuevo producto final
        Task CreateAsync(ProductoFinal producto);

        //actualizar un producto final existente
        Task UpdateAsync(ProductoFinal producto);

        //eliminar un producto final por id
        Task DeleteAsync(int id);

        //verificar si un producto final existe por id
        Task<bool> ExistsAsync(int id);
    }
}