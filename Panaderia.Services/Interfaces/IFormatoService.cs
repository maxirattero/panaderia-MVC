using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface IFormatoService
    {
        //obtener todos los Formatos
        Task<IEnumerable<Formato>> GetAllAsync();

        //obtener un Formato por Id
        Task<Formato?> GetByIdAsync(int id);

        //crear un nuevo Formato
        Task CreateAsync(Formato formato);

        //actualizar un Formato existente
        Task UpdateAsync(Formato formato);

        //eliminar un Formato por Id
        Task DeleteAsync(int id);

        //verificar si un Formato existe por id
        Task<bool> ExistsAsync(int id);
    }
}