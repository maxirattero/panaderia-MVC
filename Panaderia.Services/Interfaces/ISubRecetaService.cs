using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces;

public interface ISubRecetaService
{
    Task<IEnumerable<SubReceta>> GetAllAsync();
    Task<SubReceta?> GetByIdAsync(int id);
    Task CreateAsync(SubReceta subReceta);
    Task UpdateAsync(SubReceta subReceta);
    Task DeleteAsync(int id);
}
