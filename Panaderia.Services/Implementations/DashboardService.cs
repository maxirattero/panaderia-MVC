using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Enums;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations
{
    public class DashboardService : IDashboardService
    {
        private readonly PanaderiaContext _context;

        public DashboardService(PanaderiaContext context)
        {
            _context = context;
        }

        public async Task<DashboardResumen> GetResumenDashboardAsync()
        {
            var now = DateTime.UtcNow;
            var hoy = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            // --- Caja del mes ---
            var inicioMes = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var movimientos = await _context.ReportesCaja
                .Where(r => r.Fecha >= inicioMes && r.Fecha <= now)
                .ToListAsync();

            var caja = new CajaMesResumen
            {
                Mes           = now.Month,
                Anio          = now.Year,
                TotalIngresos = movimientos.Where(r => r.Tipo == TipoMovimiento.Ingreso).Sum(r => r.Monto),
                TotalEgresos  = movimientos.Where(r => r.Tipo == TipoMovimiento.Egreso).Sum(r => r.Monto)
            };

            // --- Alertas del día ---
            // Global query filter already excludes Anulado from Pedidos.
            var manana = hoy.AddDays(1);

            var pedidosHoy = await _context.Pedidos
                .Where(p => p.FechaEntrega >= hoy && p.FechaEntrega < manana)
                .ToListAsync();

            var saldos = await _context.Pedidos
                .Where(p => p.MontoCobrado < p.MontoTotal)
                .Select(p => p.MontoTotal - p.MontoCobrado)
                .ToListAsync();

            var insumosBajos = await _context.Insumos
                .Where(i => i.Activo && i.StockMinimo.HasValue && i.StockActual < i.StockMinimo.Value)
                .Select(i => i.Nombre)
                .ToListAsync();

            var alertas = new AlertasDiaResumen
            {
                PedidosHoyTotal            = pedidosHoy.Count,
                PedidosHoyPendientes       = pedidosHoy.Count(p => p.Estado == EstadoPedido.Pendiente),
                PedidosHoyListos           = pedidosHoy.Count(p => p.Estado == EstadoPedido.Entregado),
                SaldoPendienteTotal        = saldos.Sum(),
                PedidosSaldoPendienteCount = saldos.Count,
                InsumosBajoStockCount      = insumosBajos.Count,
                InsumosBajoStockNombres    = insumosBajos.Take(3).ToList()
            };

            // --- Producción de la semana (Lunes a Domingo) ---
            int diasDesdeInicio = ((int)hoy.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var lunesActual = hoy.AddDays(-diasDesdeInicio);
            var finSemana   = lunesActual.AddDays(7);

            // Global query filter already excludes Anulado from DetallesPedido.
            var detallesSemana = await _context.DetallesPedido
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Categoria)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Formato)
                .Where(d => d.Pedido.Estado == EstadoPedido.Pendiente
                         && d.Pedido.FechaEntrega >= lunesActual
                         && d.Pedido.FechaEntrega < finSemana)
                .ToListAsync();

            var porProducto = detallesSemana
                .GroupBy(d => d.IdProducto)
                .Select(g => new ResumenProductoItem(
                    g.Key,
                    g.First().Producto.NombreVisible,
                    g.Sum(d => d.Cantidad)))
                .OrderByDescending(x => x.CantidadTotal)
                .ToList();

            return new DashboardResumen
            {
                Caja      = caja,
                Alertas   = alertas,
                Produccion = new ProduccionSemanaResumen
                {
                    PorProducto   = porProducto,
                    LunesActual   = lunesActual,
                    DomingoActual = lunesActual.AddDays(6)
                }
            };
        }
    }
}
