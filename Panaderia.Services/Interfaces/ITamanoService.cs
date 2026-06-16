using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface ITamanoService
    {
        //obtener todos los Tamaños
        Task<IEnumerable<Tamano>> GetAllAsync();

        //obtener un Tamaño por id
        Task<Tamano?> GetByIdAsync(int id);

        //crear un nuevo Tamaño
        Task CreateAsync(Tamano tamano);

        //actualizar un Tamaño existente
        Task UpdateAsync(Tamano tamano);

        //eliminar un Tamaño por id
        Task DeleteAsync(int id);

        //verificar si un Tamaño existe por id
        Task<bool> ExistsAsync(int id);
    }
}