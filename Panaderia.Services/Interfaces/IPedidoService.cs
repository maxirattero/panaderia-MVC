using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;

namespace Panaderia.Services.Interfaces
{
    public interface IPedidoService
    {
        //Obtener todos los pedidos
        Task<IEnumerable<Pedido>> GetAllAsync();

        //obtener un pedido por id
        Task<Pedido?> GetByIdAsync(int id);

        //obtener pedidos por cliente
        Task<IEnumerable<Pedido>> GetByClienteAsync(int idCliente);

        //obtener pedidos por estado
        Task<IEnumerable<Pedido>> GetByEstadoAsync(EstadoPedido estado);

        //obtener pedidos por fecha
        Task<IEnumerable<Pedido>> GetByFechaAsync(DateTime fecha);

        //registrar un cobro parcial o total de un pedido
        Task RegistrarCobroAsync(int idPedido, decimal monto);

        //Crear un nuevo pedido
        Task CreateAsync(Pedido pedido);

        //actualizar un pedido existente
        Task UpdateAsync(Pedido pedido);

        //eliminar un pedido por id
        Task DeleteAsync(int id);

        //verificar si un pedido existe por id
        Task<bool> ExistsAsync(int id);
        // Anular pedido
        Task AnularAsync(int id);
        // Resumen de producción (pedidos no entregados)
        Task<(List<ResumenProductoItem> PorProducto, List<ResumenBolsaItem> PorBolsa)> GetResumenProduccionAsync();
        // Marcar pedido como entregado
        Task MarcarEntregadoAsync(int id);
        // Total vendido en la semana actual (domingo a sábado)
        Task<decimal> GetTotalVendidoSemanaAsync();
    }
}