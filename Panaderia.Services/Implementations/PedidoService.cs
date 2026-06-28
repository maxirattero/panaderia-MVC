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
                        .ThenInclude(p => p.Categoria)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.Formato)
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
                        .ThenInclude(p => p.Categoria)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.Formato)
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
            var pedido = await _context.Pedidos
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == idPedido);
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
                        Descripcion = $"Venta - {pedido.Cliente.NombreCompleto}",
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

        public async Task<bool> ExisteCierreSemanalAsync(DateTime inicioSemana, DateTime finSemana)
        {
            return await _context.ReportesCaja
                .AnyAsync(r => r.FechaInicioPeriodo == inicioSemana && r.FechaFinPeriodo == finSemana);
        }

        public async Task<ResumenCierreSemanal> GetResumenCierreSemanalAsync(DateTime inicioSemana)
        {
            var finSemana = inicioSemana.AddDays(7);

            // Movimientos de caja de la semana, excluyendo cierres registrados
            var movimientos = await _context.ReportesCaja
                .Where(r => r.Fecha >= inicioSemana
                         && r.Fecha < finSemana
                         && !r.FechaInicioPeriodo.HasValue)
                .ToListAsync();

            decimal totalIngresos = movimientos.Where(r => r.Tipo == TipoMovimiento.Ingreso).Sum(r => r.Monto);
            decimal totalEgresos  = movimientos.Where(r => r.Tipo == TipoMovimiento.Egreso).Sum(r => r.Monto);

            // Costo estimado desde recetas (informativo)
            var pedidos = await _context.Pedidos
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.Categoria)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.Formato)
                .Where(p => p.Estado == EstadoPedido.Entregado
                         && p.FechaEntrega >= inicioSemana
                         && p.FechaEntrega < finSemana)
                .ToListAsync();

            var gruposPorProducto = pedidos
                .SelectMany(p => p.Detalles)
                .GroupBy(d => d.IdProducto)
                .ToList();

            var detalles = new List<CostoProductoItem>();
            decimal costoTotal = 0m;

            foreach (var grupo in gruposPorProducto)
            {
                var primerDetalle = grupo.First();
                int cantidadTotal = grupo.Sum(d => d.Cantidad);

                var receta = await _context.Recetas
                    .Include(r => r.Detalles)
                        .ThenInclude(rd => rd.Insumo)
                    .FirstOrDefaultAsync(r => r.IdProducto == grupo.Key);

                decimal costoUnitario = receta != null && receta.TamanioLote > 0
                    ? receta.CostoPorUnidad
                    : 0m;

                detalles.Add(new CostoProductoItem
                {
                    NombreProducto  = primerDetalle.Producto.NombreVisible,
                    CantidadVendida = cantidadTotal,
                    CostoUnitario   = costoUnitario
                });

                costoTotal += costoUnitario * cantidadTotal;
            }

            return new ResumenCierreSemanal
            {
                TotalIngresos = totalIngresos,
                TotalEgresos  = totalEgresos,
                CostoInsumos  = costoTotal,
                DetallesCosto = detalles
            };
        }

        // Confirmar producción y descontar stock de insumos
        public async Task<List<string>> ConfirmarProduccionAsync(List<ItemProduccionSeleccionable> items)
        {
            var warnings = new List<string>();

            foreach (var item in items.Where(i => i.Seleccionado))
            {
                var receta = await _context.Recetas
                    .Include(r => r.Detalles).ThenInclude(d => d.Insumo)
                    .FirstOrDefaultAsync(r => r.Id == item.IdReceta);

                if (receta == null) continue;

                decimal vecesReceta = item.CantidadAProducir / receta.TamanioLote;

                foreach (var d in receta.Detalles)
                {
                    decimal cantidadNecesaria;
                    if (d.PorcentajePanadero.HasValue)
                    {
                        if (receta.SumaPorcentajes == 0) continue;
                        cantidadNecesaria = (receta.TamanioLote * receta.PesoUnitario / receta.SumaPorcentajes)
                                            * d.PorcentajePanadero.Value * vecesReceta;
                    }
                    else
                    {
                        cantidadNecesaria = d.CantidadFija!.Value * receta.TamanioLote * vecesReceta;
                    }

                    var insumo = await _context.Insumos.FindAsync(d.IdInsumo);
                    if (insumo == null) continue;

                    if (insumo.StockActual < cantidadNecesaria)
                        warnings.Add($"Stock insuficiente: {insumo.Nombre} – necesitás {cantidadNecesaria:0.###} {insumo.UnidadBase}, tenés {insumo.StockActual:0.###}");

                    insumo.StockActual -= cantidadNecesaria;
                }
            }

            await _context.SaveChangesAsync();
            return warnings;
        }

        // Resumen de producción (pedidos no entregados, anulados excluidos por query filter)
        public async Task<(List<ResumenProductoItem> PorProducto, List<ResumenBolsaItem> PorBolsa, List<ResumenSubRecetaItem> PorSubReceta, decimal TotalAgua)> GetResumenProduccionAsync()
        {
            var detalles = await _context.DetallesPedido
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Categoria)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Formato)
                .Where(d => d.Pedido.Estado != EstadoPedido.Entregado)
                .ToListAsync();

            var porProducto = detalles
                .GroupBy(d => d.IdProducto)
                .Select(g => new
                {
                    IdProducto = g.Key,
                    Producto = g.First().Producto,
                    Cantidad = g.Sum(d => d.Cantidad)
                })
                .OrderBy(x => x.Producto.Categoria?.Nombre)
                .ThenBy(x => x.Producto.Masa)
                .Select(x => new ResumenProductoItem(x.IdProducto, x.Producto.NombreVisible, x.Cantidad))
                .ToList();

            var porBolsa = detalles
                .GroupBy(d => d.Bolsa)
                .OrderBy(g => g.Key)
                .Select(g => new ResumenBolsaItem(g.Key, g.Sum(d => d.Cantidad)))
                .ToList();

            // Build sub-receta totals
            var porSubReceta = new List<ResumenSubRecetaItem>();
            decimal totalAgua = 0m;

            foreach (var productoItem in porProducto)
            {
                var receta = await _context.Recetas
                    .Include(r => r.Detalles)
                        .ThenInclude(d => d.Insumo)
                    .Include(r => r.Detalles)
                        .ThenInclude(d => d.SubReceta)
                            .ThenInclude(s => s.Detalles)
                                .ThenInclude(sd => sd.Insumo)
                    .FirstOrDefaultAsync(r => r.IdProducto == productoItem.IdProducto);

                if (receta == null) continue;

                decimal vecesReceta = (decimal)productoItem.CantidadTotal / receta.TamanioLote;

                foreach (var det in receta.Detalles.Where(d => d.IdSubReceta.HasValue && d.SubReceta != null))
                {
                    if (!det.PorcentajePanadero.HasValue || receta.SumaPorcentajes == 0) continue;

                    decimal gramosBase = (receta.TamanioLote * receta.PesoUnitario
                                          / receta.SumaPorcentajes)
                                         * det.PorcentajePanadero.Value * vecesReceta;

                    decimal gramosConMargen = gramosBase * (1 + det.SubReceta!.MargenSeguridad);

                    var existing = porSubReceta.FirstOrDefault(s => s.IdSubReceta == det.SubReceta.Id);
                    if (existing == null)
                    {
                        existing = new ResumenSubRecetaItem
                        {
                            IdSubReceta = det.SubReceta.Id,
                            Nombre      = det.SubReceta.Nombre
                        };
                        porSubReceta.Add(existing);
                    }
                    existing.TotalGramos += gramosConMargen;
                }

                // Accumulate water from direct recipe ingredients
                foreach (var det in receta.Detalles.Where(d => d.IdInsumo.HasValue && d.Insumo != null))
                {
                    if (det.Insumo!.Nombre.Equals("Agua corriente", StringComparison.OrdinalIgnoreCase)
                        && det.PorcentajePanadero.HasValue && receta.SumaPorcentajes > 0)
                    {
                        decimal gramosAgua = (receta.TamanioLote * receta.PesoUnitario
                                             / receta.SumaPorcentajes)
                                            * det.PorcentajePanadero.Value * vecesReceta;
                        totalAgua += gramosAgua;
                    }
                }
            }

            // Build ingredient breakdown for each sub-receta
            foreach (var srItem in porSubReceta)
            {
                var subReceta = await _context.SubRecetas
                    .Include(s => s.Detalles).ThenInclude(d => d.Insumo)
                    .FirstOrDefaultAsync(s => s.Id == srItem.IdSubReceta);

                if (subReceta == null) continue;

                var sumaPctSub = subReceta.Detalles
                    .Where(d => d.PorcentajePanadero.HasValue)
                    .Sum(d => d.PorcentajePanadero!.Value);

                if (sumaPctSub == 0) continue;

                foreach (var sd in subReceta.Detalles)
                {
                    if (sd.Insumo == null) continue;

                    decimal cantidad;
                    string unidad;

                    if (sd.PorcentajePanadero.HasValue)
                    {
                        cantidad = srItem.TotalGramos / sumaPctSub * sd.PorcentajePanadero.Value;
                        unidad   = sd.Insumo.UnidadBase switch
                        {
                            Panaderia.Models.Enums.UnidadMedida.Mililitros => "ml",
                            Panaderia.Models.Enums.UnidadMedida.Unidades   => "u",
                            _                                               => "g"
                        };
                    }
                    else if (sd.CantidadFija.HasValue)
                    {
                        cantidad = sd.CantidadFija.Value * (srItem.TotalGramos / 100m);
                        unidad   = "u";
                    }
                    else continue;

                    srItem.Ingredientes.Add(new ResumenSubRecetaIngrediente
                    {
                        NombreInsumo = sd.Insumo.Nombre,
                        Cantidad     = cantidad,
                        Unidad       = unidad
                    });
                }
            }

            return (porProducto, porBolsa, porSubReceta, totalAgua);
        }

        public async Task<List<ProduccionProductoDetalle>> GetIngredientesProduccionAsync()
        {
            var detalles = await _context.DetallesPedido
                .Include(d => d.Producto).ThenInclude(p => p.Categoria)
                .Include(d => d.Producto).ThenInclude(p => p.Formato)
                .Where(d => d.Pedido.Estado != EstadoPedido.Entregado)
                .ToListAsync();

            var porProducto = detalles
                .GroupBy(d => d.IdProducto)
                .Select(g => new
                {
                    IdProducto = g.Key,
                    Producto = g.First().Producto,
                    Cantidad = g.Sum(d => d.Cantidad)
                })
                .OrderBy(x => x.Producto.Categoria?.Nombre)
                .ThenBy(x => x.Producto.Masa)
                .ToList();

            var resultado = new List<ProduccionProductoDetalle>();

            foreach (var item in porProducto)
            {
                var receta = await _context.Recetas
                    .Include(r => r.Detalles).ThenInclude(d => d.Insumo)
                    .Include(r => r.Detalles).ThenInclude(d => d.SubReceta)
                    .FirstOrDefaultAsync(r => r.IdProducto == item.IdProducto);

                if (receta == null) continue;

                decimal vecesReceta = (decimal)item.Cantidad / receta.TamanioLote;
                decimal pesoMasaTotal = receta.PesoUnitario * item.Cantidad;

                var ingredientes = new List<ProduccionIngredienteDetalle>();

                foreach (var det in receta.Detalles)
                {
                    if (det.IdInsumo.HasValue && det.Insumo != null)
                    {
                        decimal gramos;
                        string unidad;

                        if (det.PorcentajePanadero.HasValue && receta.SumaPorcentajes > 0)
                        {
                            gramos = (receta.TamanioLote * receta.PesoUnitario
                                      / receta.SumaPorcentajes)
                                     * det.PorcentajePanadero.Value * vecesReceta;
                            unidad = det.Insumo.UnidadBase switch
                            {
                                Panaderia.Models.Enums.UnidadMedida.Mililitros => "ml",
                                Panaderia.Models.Enums.UnidadMedida.Unidades   => "u",
                                _                                               => "g"
                            };
                        }
                        else if (det.CantidadFija.HasValue)
                        {
                            gramos = det.CantidadFija.Value * receta.TamanioLote * vecesReceta;
                            unidad = "u";
                        }
                        else continue;

                        ingredientes.Add(new ProduccionIngredienteDetalle
                        {
                            IdInsumo    = det.IdInsumo,
                            Nombre      = det.Insumo.Nombre,
                            Gramos      = gramos,
                            Unidad      = unidad,
                            EsSubReceta = false
                        });
                    }
                    else if (det.IdSubReceta.HasValue && det.SubReceta != null)
                    {
                        if (!det.PorcentajePanadero.HasValue || receta.SumaPorcentajes == 0) continue;

                        decimal gramos = (receta.TamanioLote * receta.PesoUnitario
                                          / receta.SumaPorcentajes)
                                         * det.PorcentajePanadero.Value * vecesReceta;

                        ingredientes.Add(new ProduccionIngredienteDetalle
                        {
                            IdSubReceta = det.IdSubReceta,
                            Nombre      = det.SubReceta.Nombre,
                            Gramos      = gramos,
                            Unidad      = "g",
                            EsSubReceta = true
                        });
                    }
                }

                resultado.Add(new ProduccionProductoDetalle
                {
                    IdProducto       = item.IdProducto,
                    NombreProducto   = item.Producto.NombreVisible,
                    CantidadUnidades = item.Cantidad,
                    PesoMasaTotal    = pesoMasaTotal,
                    Ingredientes     = ingredientes
                });
            }

            return resultado;
        }
    }
}
