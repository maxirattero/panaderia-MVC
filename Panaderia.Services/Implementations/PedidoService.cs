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
                // El empaque define si el renglón va en bolsa de papel o sellada:
                // lo usa la impresión con detalles.
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Empaque)
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
            if (monto <= 0)
                throw new InvalidOperationException("El cobro debe ser mayor a cero.");

            var pedido = await _context.Pedidos
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == idPedido);
            if (pedido != null)
            {
                if (monto > pedido.SaldoPendiente)
                    throw new InvalidOperationException("El cobro no puede superar el saldo pendiente.");

                pedido.MontoCobrado += monto;
                _context.ReportesCaja.Add(new ReporteCaja
                {
                    Fecha = DateTime.UtcNow,
                    Tipo = TipoMovimiento.Ingreso,
                    Categoria = CategoriaMovimiento.Venta,
                    Monto = monto,
                    Descripcion = $"Venta - {pedido.Cliente.NombreCompleto}",
                    IdPedido = pedido.Id
                });
                await _context.SaveChangesAsync();
            }
        }

        //crear un nuevo pedido
        public async Task CreateAsync(Pedido pedido)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await AplicarCostoEmpaqueAsync(pedido.Detalles);
            await ReservarStockAsync(pedido.Detalles);
            await _context.Pedidos.AddAsync(pedido);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        //actualizar un pedido existente
        public async Task UpdateAsync(Pedido pedido)
        {
            var existing = await _context.Pedidos
                .Include(p => p.Detalles)
                .FirstOrDefaultAsync(p => p.Id == pedido.Id);
            if (existing == null) return;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            existing.IdCliente = pedido.IdCliente;
            existing.FechaEntrega = pedido.FechaEntrega;
            existing.Notas = pedido.Notas;
            existing.DescuentoPorcentaje = pedido.DescuentoPorcentaje;
            existing.MontoTotal = pedido.MontoTotal;
            existing.FechaModificacion = DateTime.UtcNow;

            // Libera la reserva anterior dentro de la misma transacción: si la nueva
            // selección no alcanza, todo se revierte y el pedido queda intacto.
            await RestituirStockReservadoAsync(existing.Detalles);
            await AplicarCostoEmpaqueAsync(pedido.Detalles);
            await ReservarStockAsync(pedido.Detalles);

            _context.DetallesPedido.RemoveRange(existing.Detalles);
            existing.Detalles.Clear();
            foreach (var d in pedido.Detalles)
            {
                existing.Detalles.Add(new DetallePedido
                {
                    IdProducto = d.IdProducto,
                    Cantidad = d.Cantidad,
                    ReservaStock = d.ReservaStock,
                    PrecioUnitario = d.PrecioUnitario,
                    IdEmpaque = d.IdEmpaque,
                    LlevaEtiqueta = d.LlevaEtiqueta,
                    CostoEmpaque = d.CostoEmpaque
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        //eliminar un pedido por su ID        
        public async Task DeleteAsync(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Detalles)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pedido == null) return;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            await RestituirStockReservadoAsync(pedido.Detalles);
            _context.Pedidos.Remove(pedido);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Verificar si un pedido existe por su ID
        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Pedidos.AnyAsync(p => p.Id == id);

        }

        // Anular pedido
        public async Task AnularAsync(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Detalles)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (pedido == null) return;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            await RestituirStockReservadoAsync(pedido.Detalles);
            pedido.Anulado = true;

            var reportesVenta = await _context.ReportesCaja
                .Where(r => r.IdPedido == id && r.Categoria == CategoriaMovimiento.Venta)
                .ToListAsync();
            _context.ReportesCaja.RemoveRange(reportesVenta);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
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

        public async Task<decimal> GetTotalVendidoAsync(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var pedidos = _context.Pedidos.AsQueryable();

            if (fechaInicio.HasValue)
                pedidos = pedidos.Where(p => p.FechaEntrega >= fechaInicio.Value);
            if (fechaFin.HasValue)
                pedidos = pedidos.Where(p => p.FechaEntrega < fechaFin.Value);

            return await pedidos.SumAsync(p => (decimal?)p.MontoTotal) ?? 0m;
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
            decimal totalEgresos = movimientos.Where(r => r.Tipo == TipoMovimiento.Egreso).Sum(r => r.Monto);

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
                    NombreProducto = primerDetalle.Producto.NombreVisible,
                    CantidadVendida = cantidadTotal,
                    CostoUnitario = costoUnitario
                });

                costoTotal += costoUnitario * cantidadTotal;
            }

            return new ResumenCierreSemanal
            {
                TotalIngresos = totalIngresos,
                TotalEgresos = totalEgresos,
                CostoInsumos = costoTotal,
                DetallesCosto = detalles
            };
        }

        // Confirmar producción y descontar stock de insumos.
        // Los items marcados como stock (EsStock) suman Producto.Stock y limpian su fila del buffer.
        public async Task<List<string>> ConfirmarProduccionAsync(List<ItemProduccionSeleccionable> items)
        {
            var warnings = new List<string>();
            var bufferIdsAEliminar = new List<int>();
            var producciones = new List<(ItemProduccionSeleccionable Item, Receta Receta)>();
            var necesidades = new Dictionary<int, decimal>();

            foreach (var item in items.Where(i => i.Seleccionado))
            {
                var receta = await _context.Recetas
                    .Include(r => r.Detalles).ThenInclude(d => d.Insumo)
                    .FirstOrDefaultAsync(r => r.Id == item.IdReceta);

                if (receta == null)
                {
                    warnings.Add($"No se encontró la receta de {item.NombreProducto}.");
                    continue;
                }
                if (receta.TamanioLote <= 0)
                {
                    warnings.Add($"La receta de {item.NombreProducto} no tiene un tamaño de lote válido.");
                    continue;
                }

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

                    if (!d.IdInsumo.HasValue || d.IdInsumo.Value <= 0) continue;
                    necesidades.TryGetValue(d.IdInsumo.Value, out var acumulada);
                    necesidades[d.IdInsumo.Value] = acumulada + cantidadNecesaria;
                }

                producciones.Add((item, receta));
            }

            var insumos = await _context.Insumos
                .Where(i => necesidades.Keys.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id);

            foreach (var (idInsumo, cantidadNecesaria) in necesidades)
            {
                if (!insumos.TryGetValue(idInsumo, out var insumo))
                {
                    warnings.Add("Un insumo de la receta ya no existe.");
                    continue;
                }

                if (insumo.StockActual < cantidadNecesaria)
                    warnings.Add($"Stock insuficiente: {insumo.Nombre} – necesitás {cantidadNecesaria:0.###} {insumo.UnidadBase}, tenés {insumo.StockActual:0.###}");
            }

            // Si falta algo, no persiste ningún descuento ni la producción parcial.
            if (warnings.Any()) return warnings;

            foreach (var (idInsumo, cantidadNecesaria) in necesidades)
                insumos[idInsumo].StockActual -= cantidadNecesaria;

            foreach (var (item, _) in producciones)
            {
                // Producción para stock: suma unidades al inventario del producto y marca el buffer para limpieza
                if (item.EsStock)
                {
                    var producto = await _context.Productos.FindAsync(item.IdProducto);
                    if (producto != null)
                    {
                        producto.Stock += (int)item.CantidadAProducir;
                        producto.SinStock = !producto.PorEncargo && producto.Stock <= 0;
                    }

                    if (item.IdProduccionStock > 0)
                        bufferIdsAEliminar.Add(item.IdProduccionStock);
                }
            }

            // Cuando se confirma el lote completo de pedidos pendientes, estos salen de
            // la próxima planificación pero permanecen disponibles para la entrega.
            var pendientesPorProducto = await _context.DetallesPedido
                .Where(d => d.Pedido.Estado == EstadoPedido.Pendiente)
                .GroupBy(d => d.IdProducto)
                .Select(g => new { IdProducto = g.Key, Cantidad = g.Sum(d => d.Cantidad) })
                .ToListAsync();
            var confirmadosPorProducto = producciones
                .Where(p => !p.Item.EsStock)
                .GroupBy(p => p.Item.IdProducto)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Item.CantidadAProducir));

            if (pendientesPorProducto.Any()
                && pendientesPorProducto.All(p => confirmadosPorProducto.TryGetValue(p.IdProducto, out var cantidad)
                                             && cantidad >= p.Cantidad))
            {
                var pedidosPendientes = await _context.Pedidos
                    .Where(p => p.Estado == EstadoPedido.Pendiente)
                    .ToListAsync();
                foreach (var pedidoPendiente in pedidosPendientes)
                    pedidoPendiente.Estado = EstadoPedido.EnProduccion;
            }

            if (bufferIdsAEliminar.Any())
            {
                var filas = await _context.ProduccionStock
                    .Where(s => bufferIdsAEliminar.Contains(s.Id))
                    .ToListAsync();
                _context.ProduccionStock.RemoveRange(filas);
            }

            await _context.SaveChangesAsync();
            return warnings;
        }

        // ─── Buffer de producción para stock ────────────────────────────────

        public async Task AgregarProduccionStockAsync(int idProducto, int cantidad)
        {
            if (cantidad < 1) return;

            var existente = await _context.ProduccionStock
                .FirstOrDefaultAsync(s => s.IdProducto == idProducto);

            if (existente != null)
            {
                existente.Cantidad += cantidad;
            }
            else
            {
                _context.ProduccionStock.Add(new ProduccionStock
                {
                    IdProducto = idProducto,
                    Cantidad = cantidad,
                    Fecha = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<ProduccionStock>> GetProduccionStockAsync()
        {
            return await _context.ProduccionStock
                .Include(s => s.Producto).ThenInclude(p => p.Categoria)
                .Include(s => s.Producto).ThenInclude(p => p.Formato)
                .OrderBy(s => s.Fecha)
                .ToListAsync();
        }

        public async Task QuitarProduccionStockAsync(int id)
        {
            var fila = await _context.ProduccionStock.FindAsync(id);
            if (fila == null) return;

            _context.ProduccionStock.Remove(fila);
            await _context.SaveChangesAsync();
        }

        // Producción combinada: pedidos pendientes + buffer de stock, sumado por producto.
        // Los productos destildados en el dashboard de Producción quedan fuera.
        private async Task<List<(int IdProducto, Producto Producto, int Cantidad)>> GetProduccionCombinadaAsync(IEnumerable<int>? productosExcluidos = null)
        {
            var excluidos = productosExcluidos?.ToHashSet() ?? new HashSet<int>();
            var detalles = await _context.DetallesPedido
                .Include(d => d.Producto).ThenInclude(p => p.Categoria)
                .Include(d => d.Producto).ThenInclude(p => p.Formato)
                .Where(d => d.Pedido.Estado == EstadoPedido.Pendiente)
                .ToListAsync();

            var acumulado = new Dictionary<int, (Producto Producto, int Cantidad)>();

            foreach (var d in detalles)
            {
                if (acumulado.TryGetValue(d.IdProducto, out var actual))
                    acumulado[d.IdProducto] = (actual.Producto, actual.Cantidad + d.Cantidad);
                else
                    acumulado[d.IdProducto] = (d.Producto, d.Cantidad);
            }

            var stock = await _context.ProduccionStock
                .Include(s => s.Producto).ThenInclude(p => p.Categoria)
                .Include(s => s.Producto).ThenInclude(p => p.Formato)
                .ToListAsync();

            foreach (var s in stock)
            {
                if (acumulado.TryGetValue(s.IdProducto, out var actual))
                    acumulado[s.IdProducto] = (actual.Producto, actual.Cantidad + s.Cantidad);
                else
                    acumulado[s.IdProducto] = (s.Producto, s.Cantidad);
            }

            return acumulado
                .Where(kv => !excluidos.Contains(kv.Key))
                .Select(kv => (IdProducto: kv.Key, kv.Value.Producto, kv.Value.Cantidad))
                .OrderBy(x => x.Producto.Categoria?.Nombre)
                .ThenBy(x => x.Producto.Masa)
                .ToList();
        }

        // Resumen combinado por producto (pedidos + stock) para impresión de plan
        public async Task<List<ResumenProductoItem>> GetProduccionCombinadaResumenAsync(IEnumerable<int>? productosExcluidos = null)
        {
            var combinada = await GetProduccionCombinadaAsync(productosExcluidos);
            return combinada
                .Select(x => new ResumenProductoItem(x.IdProducto, x.Producto.NombreVisible, x.Cantidad))
                .ToList();
        }

        // Resumen de producción (pedidos no entregados, anulados excluidos por query filter).
        // PorProducto y PorBolsa son solo de pedidos; sub-recetas y agua reflejan produccion completa (pedidos + stock).
        public async Task<(List<ResumenProductoItem> PorProducto, List<ResumenBolsaItem> PorBolsa, List<ResumenSubRecetaItem> PorSubReceta, decimal TotalAgua)> GetResumenProduccionAsync(IEnumerable<int>? productosExcluidos = null)
        {
            var excluidos = productosExcluidos?.ToHashSet() ?? new HashSet<int>();

            var detalles = (await _context.DetallesPedido
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Categoria)
                .Include(d => d.Producto)
                    .ThenInclude(p => p.Formato)
                .Include(d => d.Empaque)
                .Where(d => d.Pedido.Estado == EstadoPedido.Pendiente)
                .ToListAsync())
                .Where(d => !excluidos.Contains(d.IdProducto))
                .ToList();

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

            // El tipo de bolsa sale del empaque elegido: los marcados como "bolsa de papel"
            // suman en Papel y el resto en Sellado. Los renglones sin empaque no cuentan.
            var porBolsa = detalles
                .Where(d => d.Empaque != null)
                .GroupBy(d => d.Empaque!.EsBolsaPapel ? TipoBolsa.Papel : TipoBolsa.Sellado)
                .OrderBy(g => g.Key)
                .Select(g => new ResumenBolsaItem(g.Key, g.Sum(d => d.Cantidad)))
                .ToList();

            // Produccion completa (pedidos + stock) para sub-recetas y agua
            var combinada = await GetProduccionCombinadaAsync(productosExcluidos);

            var porSubReceta = new List<ResumenSubRecetaItem>();
            decimal totalAgua = 0m;

            foreach (var prod in combinada)
            {
                var receta = await _context.Recetas
                    .Include(r => r.Detalles)
                        .ThenInclude(d => d.Insumo)
                    .Include(r => r.Detalles)
                        .ThenInclude(d => d.SubReceta)
                            .ThenInclude(s => s.Detalles)
                                .ThenInclude(sd => sd.Insumo)
                    .FirstOrDefaultAsync(r => r.IdProducto == prod.IdProducto);

                if (receta == null) continue;

                decimal vecesReceta = (decimal)prod.Cantidad / receta.TamanioLote;

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
                            Nombre = det.SubReceta.Nombre
                        };
                        porSubReceta.Add(existing);
                    }
                    existing.TotalGramos += gramosConMargen;
                }

                // Accumulate water from direct recipe ingredients
                foreach (var det in receta.Detalles.Where(d => d.IdInsumo.HasValue && d.Insumo != null
                    && d.Insumo.TipoInsumo == TipoInsumo.Ingrediente))
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
                        unidad = sd.Insumo.UnidadBase switch
                        {
                            Panaderia.Models.Enums.UnidadMedida.Mililitros => "ml",
                            Panaderia.Models.Enums.UnidadMedida.Unidades => "u",
                            _ => "g"
                        };
                    }
                    else if (sd.CantidadFija.HasValue)
                    {
                        cantidad = sd.CantidadFija.Value * (srItem.TotalGramos / 100m);
                        unidad = "u";
                    }
                    else continue;

                    srItem.Ingredientes.Add(new ResumenSubRecetaIngrediente
                    {
                        NombreInsumo = sd.Insumo.Nombre,
                        Cantidad = cantidad,
                        Unidad = unidad
                    });
                }
            }

            return (porProducto, porBolsa, porSubReceta, totalAgua);
        }

        // Reserva unidades al registrar el pedido. ExecuteUpdate hace que la condición
        // Stock >= cantidad se aplique en la base, evitando sobreventas simultáneas.
        private async Task ReservarStockAsync(IEnumerable<DetallePedido> detalles)
        {
            var detallesLista = detalles.Where(d => d.Cantidad > 0).ToList();
            foreach (var detalle in detallesLista)
                detalle.ReservaStock = false;

            var cantidadesPorProducto = detallesLista
                .GroupBy(d => d.IdProducto)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));
            if (!cantidadesPorProducto.Any()) return;

            var productos = await _context.Productos
                .Where(p => cantidadesPorProducto.Keys.Contains(p.Id))
                .Select(p => new { p.Id, p.Nombre, p.PorEncargo })
                .ToDictionaryAsync(p => p.Id);

            foreach (var (idProducto, cantidad) in cantidadesPorProducto)
            {
                if (!productos.TryGetValue(idProducto, out var producto))
                    throw new InvalidOperationException("Uno de los productos del pedido ya no existe.");

                // Panes, crackers y cualquier producto por encargo no consumen stock.
                if (producto.PorEncargo) continue;

                var filasActualizadas = await _context.Productos
                    .Where(p => p.Id == idProducto && !p.PorEncargo && p.Stock >= cantidad)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(p => p.Stock, p => p.Stock - cantidad)
                        .SetProperty(p => p.SinStock, p => p.Stock - cantidad <= 0));

                if (filasActualizadas == 0)
                {
                    var nombre = string.IsNullOrWhiteSpace(producto.Nombre) ? "el producto seleccionado" : producto.Nombre;
                    throw new InvalidOperationException($"No hay stock suficiente de {nombre}. Volvé al carrito para ajustar el pedido.");
                }

                foreach (var detalle in detallesLista.Where(d => d.IdProducto == idProducto))
                    detalle.ReservaStock = true;
            }
        }

        // Devuelve únicamente las unidades que esta app había reservado al crear o editar el pedido.
        private async Task RestituirStockReservadoAsync(IEnumerable<DetallePedido> detalles)
        {
            var cantidadesPorProducto = detalles
                .Where(d => d.ReservaStock && d.Cantidad > 0)
                .GroupBy(d => d.IdProducto)
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad));

            foreach (var (idProducto, cantidad) in cantidadesPorProducto)
            {
                await _context.Productos
                    .Where(p => p.Id == idProducto)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(p => p.Stock, p => p.Stock + cantidad)
                        .SetProperty(p => p.SinStock, p => !p.PorEncargo && p.Stock + cantidad <= 0));
            }
        }

        private async Task AplicarCostoEmpaqueAsync(IEnumerable<DetallePedido> detalles)
        {
            decimal costoEtiqueta = 0m;
            bool etiquetaCargada = false;

            foreach (var detalle in detalles)
            {
                decimal costoEmpaque = 0m;
                if (detalle.IdEmpaque.HasValue)
                {
                    var insumoEmpaque = await _context.Insumos.FindAsync(detalle.IdEmpaque.Value);
                    if (insumoEmpaque != null && insumoEmpaque.CantidadRendimiento > 0)
                        costoEmpaque = insumoEmpaque.PrecioCompra / insumoEmpaque.CantidadRendimiento;
                }

                if (detalle.LlevaEtiqueta)
                {
                    if (!etiquetaCargada)
                    {
                        var insumoEtiqueta = await _context.Insumos
                            .FirstOrDefaultAsync(i => i.TipoInsumo == TipoInsumo.Etiqueta);
                        if (insumoEtiqueta != null && insumoEtiqueta.CantidadRendimiento > 0)
                            costoEtiqueta = insumoEtiqueta.PrecioCompra / insumoEtiqueta.CantidadRendimiento;
                        etiquetaCargada = true;
                    }
                    costoEmpaque += costoEtiqueta;
                }

                detalle.CostoEmpaque = costoEmpaque;
            }
        }

        public async Task<List<ProduccionProductoDetalle>> GetIngredientesProduccionAsync(IEnumerable<int>? productosExcluidos = null)
        {
            var combinada = await GetProduccionCombinadaAsync(productosExcluidos);

            var resultado = new List<ProduccionProductoDetalle>();

            foreach (var item in combinada)
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
                    if (det.IdInsumo.HasValue && det.Insumo != null
                        && det.Insumo.TipoInsumo == TipoInsumo.Ingrediente)
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
                                Panaderia.Models.Enums.UnidadMedida.Unidades => "u",
                                _ => "g"
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
                            IdInsumo = det.IdInsumo,
                            Nombre = det.Insumo.Nombre,
                            Gramos = gramos,
                            Unidad = unidad,
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
                            Nombre = det.SubReceta.Nombre,
                            Gramos = gramos,
                            Unidad = "g",
                            EsSubReceta = true
                        });
                    }
                }

                var masaKey = $"{(int)item.Producto.Masa}-{(item.Producto.Variedad.HasValue ? ((int)item.Producto.Variedad.Value).ToString() : "")}";
                var nombreMasa = item.Producto.Masa.ToString() + (item.Producto.Variedad.HasValue ? " " + item.Producto.Variedad.Value.ToString() : "");

                resultado.Add(new ProduccionProductoDetalle
                {
                    IdProducto = item.IdProducto,
                    NombreProducto = item.Producto.NombreVisible,
                    MasaKey = masaKey,
                    NombreMasa = nombreMasa,
                    NombreCategoria = item.Producto.Categoria?.Nombre ?? "",
                    ObservacionesElaboracion = item.Producto.ObservacionesElaboracion,
                    CantidadUnidades = item.Cantidad,
                    PesoMasaTotal = pesoMasaTotal,
                    Ingredientes = ingredientes
                });
            }

            return resultado;
        }
    }
}
