using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface IClienteService
    {
        //obtener todos los clientes
        Task<IEnumerable<Cliente>> GetAllAsync();

        //obtener un cliente por id
        Task<Cliente?> GetByIdAsync(int id);

        //crear un nuevo cliente
        Task CreateAsync(Cliente cliente);

        //actualizar un cliente existente
        Task UpdateAsync(Cliente cliente);

        //eliminar un cliente por id
        Task DeleteAsync(int id);

        //verificar si un cliente existe por id
        Task<bool> ExistsAsync(int id);
    }
}