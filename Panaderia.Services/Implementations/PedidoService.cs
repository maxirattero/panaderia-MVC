using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class PedidoService : IPedidoService
    {
        private readonly PanaderiaContext _context;

        public PedidoService(PanaderiaContext context)
        {
            _context = context;
        }

        //listado de pedidos
        public async Task<IEnumerable<Pedido>> GetAllAsync()
        {
            return await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .ToListAsync();
        }

        //obtener un pedido por su ID
        public async Task<Pedido?> GetByIdAsync(int id)
        {
            return await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        //obtener pedidos por cliente
        public async Task<IEnumerable<Pedido>> GetByClienteAsync(int idCliente)
        {
            return await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(p => p.IdCliente == idCliente)
                .ToListAsync();
        }

        //obtener pedidos por estado
        public async Task<IEnumerable<Pedido>> GetByEstadoAsync(EstadoPedido estado)
        {
            return await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(p => p.Estado == estado)
                .ToListAsync();
        }

        //obtener pedidos por fecha
        public async Task<IEnumerable<Pedido>> GetByFechaAsync(DateTime fecha)
        {
            return await _context.Pedidos
                .Include(p => p.Cliente)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(p => p.FechaCreacion.Date == fecha.Date)
                .ToListAsync();
        }

        //registrar un cobro parcial o total de un pedido y el Reporte de Caja
        public async Task RegistrarCobroAsync(int idPedido, decimal monto)
        {
            var pedido = await _context.Pedidos.FindAsync(idPedido);
            if (pedido != null)
            {
                pedido.MontoCobrado += monto;
                _context.Pedidos.Update(pedido);

                var reporte = new ReporteCaja
                {
                    IdPedido = idPedido,
                    Monto = monto,
                    Fecha = DateTime.Now,
                    Tipo = TipoMovimiento.Ingreso,
                    Categoria = CategoriaMovimiento.Venta
                };
                await _context.ReportesCaja.AddAsync(reporte);
                await _context.SaveChangesAsync();
            }
        }

        //crear un nuevo pedido
        public async Task CreateAsync(Pedido pedido)
        {
            await _context.Pedidos.AddAsync(pedido);
            await _context.SaveChangesAsync();
        }

        //actualizar un pedido existente
        public async Task UpdateAsync(Pedido pedido)
        {
            pedido.FechaModificacion = DateTime.Now;
            _context.Pedidos.Update(pedido);
            await _context.SaveChangesAsync();
        }

        //eliminar un pedido por su ID
        public async Task DeleteAsync(int id)
        {
            await _context.Pedidos.Where(p => p.Id == id).ExecuteDeleteAsync();
        }

        // Verificar si un pedido existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Pedidos.AnyAsync(p => p.Id == id);

        }
    }
}
