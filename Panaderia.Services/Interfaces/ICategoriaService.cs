using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface ICategoriaService
    {
        //Obtener todas las Categorias
        Task<IEnumerable<CategoriaProducto>> GetAllAsync();

        //Obtener Categoria por Id
        Task<CategoriaProducto?> GetByIdAsync(int id);

        //Crear nueva Categoria
        Task CreateAsync(CategoriaProducto categoriaProducto);

        //Actualizar Categoaria existente
        Task UpdateAsync(CategoriaProducto categoriaProducto);

        //Eliminar Categoria por Id
        Task DeleteAsync(int id);

        //Verificar si Categoria existe por Id
        Task<bool> ExistsAsync(int id);
    }
}