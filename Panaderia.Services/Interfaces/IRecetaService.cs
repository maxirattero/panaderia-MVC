using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces;

public interface IRecetaService
{
    Task<Receta?> GetByProductoIdAsync(int idProducto);
    Task UpsertAsync(Receta receta);
    Task DeleteAsync(int idReceta);
}
