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

        //buscar un cliente por teléfono: compara los últimos 6 dígitos, así coincide
        //con o sin característica, con o sin el 15, y con cualquier formato de escritura
        Task<Cliente?> GetByTelefonoAsync(string telefono);
    }
}