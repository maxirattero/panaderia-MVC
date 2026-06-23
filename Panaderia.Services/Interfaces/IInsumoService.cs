using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces;

public interface IInsumoService
{
    Task<IEnumerable<Insumo>> GetAllAsync();
    Task<Insumo?> GetByIdAsync(int id);
    Task CreateAsync(Insumo insumo);
    Task UpdateAsync(Insumo insumo);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
