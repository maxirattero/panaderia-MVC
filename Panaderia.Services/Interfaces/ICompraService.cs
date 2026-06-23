using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces;

public interface ICompraService
{
    Task<IEnumerable<CompraProveedor>> GetAllAsync();
    Task<CompraProveedor?> GetByIdAsync(int id);
    Task CreateAsync(CompraProveedor compra);
}
