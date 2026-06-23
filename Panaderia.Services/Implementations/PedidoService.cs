using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
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

                bool yaExiste = await _context.ReportesCaja
                    .AnyAsync(r => r.IdPedido == pedido.Id && r.Categoria == CategoriaMovimiento.Venta);
                if (!yaExiste)
                {
                    _context.ReportesCaja.Add(new ReporteCaja
                    {
                        Fecha = DateTime.UtcNow,
                        Tipo = TipoMovimiento.Ingreso,
                        Categoria = CategoriaMovimiento.Venta,
                        Monto = pedido.MontoCobrado,
                        Descripcion = $"Venta - Pedido #{pedido.Id}",
                        IdPedido = pedido.Id
                    });
                }
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
            var existing = await _context.Pedidos
                .Include(p => p.Detalles)
                .FirstOrDefaultAsync(p => p.Id == pedido.Id);
            if (existing == null) return;

            existing.IdCliente = pedido.IdCliente;
            existing.FechaEntrega = pedido.FechaEntrega;
            existing.Notas = pedido.Notas;
            existing.MontoTotal = pedido.MontoTotal;
            existing.FechaModificacion = DateTime.UtcNow;

            _context.DetallesPedido.RemoveRange(existing.Detalles);
            existing.Detalles.Clear();
            foreach (var d in pedido.Detalles)
            {
                existing.Detalles.Add(new DetallePedido
                {
                    IdProducto = d.IdProducto,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Bolsa = d.Bolsa
                });
            }

            await _context.SaveChangesAsync();
        }

        //eliminar un pedido por su ID        
        public async Task DeleteAsync(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Detalles)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pedido == null) return;

            _context.Pedidos.Remove(pedido);
            await _context.SaveChangesAsync();
        }

        // Verificar si un pedido existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Pedidos.AnyAsync(p => p.Id == id);

        }

        // Anular pedido
        public async Task AnularAsync(int id)
        {
            var pedido = await _context.Pedidos.FindAsync(id);
            if (pedido == null) return;

            pedido.Anulado = true;

            var repVenta = await _context.ReportesCaja
                .FirstOrDefaultAsync(r => r.IdPedido == id && r.Categoria == CategoriaMovimiento.Venta);
            if (repVenta != null)
                _context.ReportesCaja.Remove(repVenta);

            await _context.SaveChangesAsync();
        }

        // Marcar pedido como entregado
        public async Task MarcarEntregadoAsync(int id)
        {
            var pedido = await _context.Pedidos.FindAsync(id);
            if (pedido == null) return;

            pedido.Estado = EstadoPedido.Entregado;
            pedido.FechaModificacion = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<decimal> GetTotalVendidoSemanaAsync()
        {
            var hoy = DateTime.UtcNow.Date;
            int diasDesdeDomingo = (int)hoy.DayOfWeek;
            var inicioSemana = DateTime.SpecifyKind(hoy.AddDays(-diasDesdeDomingo), DateTimeKind.Utc);
            var finSemana = inicioSemana.AddDays(7);

            return await _context.Pedidos
                .Where(p => p.FechaEntrega >= inicioSemana && p.FechaEntrega < finSemana)
                .SumAsync(p => (decimal?)p.MontoTotal) ?? 0m;
        }

        // Resumen de producción (pedidos no entregados, anulados excluidos por query filter)
        public async Task<(List<ResumenProductoItem> PorProducto, List<ResumenBolsaItem> PorBolsa)> GetResumenProduccionAsync()
        {
            var detalles = await _context.DetallesPedido
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Categoria)
                .Where(d => d.Pedido.Estado != EstadoPedido.Entregado)
                .ToListAsync();

            var porProducto = detalles
                .GroupBy(d => d.IdProducto)
                .Select(g => new
                {
                    Producto = g.First().Producto,
                    Cantidad = g.Sum(d => d.Cantidad)
                })
                .OrderBy(x => x.Producto.Categoria?.Nombre)
                .ThenBy(x => x.Producto.Masa)
                .Select(x => new ResumenProductoItem(x.Producto.Nombre, x.Cantidad))
                .ToList();

            var porBolsa = detalles
                .GroupBy(d => d.Bolsa)
                .OrderBy(g => g.Key)
                .Select(g => new ResumenBolsaItem(g.Key, g.Sum(d => d.Cantidad)))
                .ToList();

            return (porProducto, porBolsa);
        }
    }
}
