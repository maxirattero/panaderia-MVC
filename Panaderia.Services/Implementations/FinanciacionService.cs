using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations;

public class FinanciacionService(PanaderiaContext db) : IFinanciacionService
{
    public async Task CrearLoteAsync(LoteFinanciado lote)
    {
        await using var tx = await ReglasCaja.IniciarAsync(db);
        if (lote.ClaveOperacion == Guid.Empty) throw new InvalidOperationException("Falta la identificación del lote.");
        if (await db.LotesFinanciados.AnyAsync(x => x.ClaveOperacion == lote.ClaveOperacion)) return;
        if (string.IsNullOrWhiteSpace(lote.Nombre) || lote.Nombre.Trim().Length > 120 || lote.Unidades <= 0
            || !await db.Productos.AnyAsync(p => p.Id == lote.IdProducto))
            throw new InvalidOperationException("Indicá nombre, producto y cantidad de unidades del lote.");
        await ReglasCaja.PeriodoAbiertoAsync(db, lote.DesdeUtc);
        lote.Nombre = lote.Nombre.Trim();
        // El formulario no puede insertar asignaciones ni productos a través del lote.
        lote.Asignaciones = [];
        lote.Producto = null!;
        db.LotesFinanciados.Add(lote);
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }

    public async Task AsignarAsync(int compra, int lote, decimal monto, Guid clave)
    {
        ReglasCaja.ImporteValido(monto);
        if (clave == Guid.Empty) throw new InvalidOperationException("Falta la identificación de la asignación.");
        await using var tx = await ReglasCaja.IniciarAsync(db);
        if (await db.AsignacionesCompraLote.AnyAsync(x => x.ClaveOperacion == clave)) return;
        var c = await db.ComprasProveedor.Include(x => x.Aportes).SingleOrDefaultAsync(x => x.Id == compra)
            ?? throw new InvalidOperationException("Compra inexistente.");
        var l = await db.LotesFinanciados.SingleOrDefaultAsync(x => x.Id == lote)
            ?? throw new InvalidOperationException("Lote inexistente.");
        if (c.Aportes.Count == 0) throw new InvalidOperationException("Esta compra histórica no tiene origen de fondos registrado. No se presume deuda personal.");
        if (c.Fecha > l.DesdeUtc) throw new InvalidOperationException("El lote debe comenzar después de la compra.");
        await ReglasCaja.PeriodoAbiertoAsync(db, l.DesdeUtc);
        if (await db.VentasLote.AnyAsync(x => x.IdLote == lote))
            throw new InvalidOperationException("El lote ya tiene ventas registradas en un cierre; sus costos están congelados.");
        if (await db.AsignacionesCompraLote.AnyAsync(x => x.IdCompra == compra && x.IdLote == lote))
            throw new InvalidOperationException("La compra ya está asignada a ese lote.");
        var usado = await db.AsignacionesCompraLote.Where(x => x.IdCompra == compra).SumAsync(x => x.Monto);
        if (usado + monto > c.MontoTotal) throw new InvalidOperationException("La asignación supera el costo sin asignar de esa compra.");
        db.AsignacionesCompraLote.Add(new() { IdCompra = compra, IdLote = lote, Monto = monto, ClaveOperacion = clave });
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }

    public async Task<FinanciacionResumen> ResumenAsync(DateTime? hasta = null, DateTime? desde = null)
    {
        var fin = hasta ?? DateTime.UtcNow.AddTicks(1);
        var lotes = await db.LotesFinanciados.AsNoTracking().Include(x => x.Producto).ThenInclude(x => x.Categoria)
            .Include(x => x.Producto).ThenInclude(x => x.Formato)
            .Include(x => x.Asignaciones).ThenInclude(x => x.Compra).ThenInclude(x => x.Aportes)
            .Where(x => x.DesdeUtc < fin).OrderBy(x => x.DesdeUtc).ThenBy(x => x.Id).ToListAsync();
        var aportes = await db.AportesCompra.AsNoTrackingWithIdentityResolution().Include(x => x.Compra).ThenInclude(x => x.Proveedor)
            .Include(x => x.Compra).ThenInclude(x => x.Aportes)
            .Where(x => x.Origen != OrigenFondos.MasaViva && x.Compra.Fecha < fin).OrderBy(x => x.Id).ToListAsync();
        var reservas = await db.ReservasReintegro.AsNoTracking().Include(x => x.Aporte).Include(x => x.Cierre).OrderBy(x => x.Id).ToListAsync();
        var pagos = await db.PagosReintegro.AsNoTracking().Include(x => x.Movimiento).Include(x => x.Aporte)
            .OrderByDescending(x => x.Movimiento.Fecha).ThenByDescending(x => x.Id).ToListAsync();
        // El consumo confirmado se conserva aun si se corrige otro dato del pedido.
        var previas = await db.VentasLote.IgnoreQueryFilters().AsNoTracking().Include(x => x.Cierre).ToListAsync();
        var ventasCorte = previas.Where(x => !hasta.HasValue || x.Cierre.FinUtc <= fin).ToList();
        var r = new FinanciacionResumen { Lotes = lotes, Reservas = reservas, Pagos = pagos,
            AdelantosPeriodo = pagos.Where(x => x.IdReserva == null && x.Movimiento.Fecha < fin && (!desde.HasValue || x.Movimiento.Fecha >= desde)).Sum(x => x.Monto) };
        if (lotes.Count > 0)
        {
            var productoIds = lotes.Select(x => x.IdProducto).Distinct().ToList();
            // Solo entregas reales y totalmente cobradas al corte. Un anticipo no consume unidades.
            var detalles = await db.DetallesPedido.AsNoTracking().Include(x => x.Pedido)
                .Where(x => productoIds.Contains(x.IdProducto) && x.Pedido.Estado == EstadoPedido.Entregado
                    && x.Pedido.FechaEntregaReal.HasValue && x.Pedido.FechaEntregaReal < fin)
                .OrderBy(x => x.Pedido.FechaEntregaReal).ThenBy(x => x.Id).ToListAsync();
            var pedidoIds = detalles.Select(x => x.IdPedido).Distinct().ToList();
            var cobros = await db.ReportesCaja.AsNoTracking().Where(x => x.IdPedido.HasValue && pedidoIds.Contains(x.IdPedido.Value)
                    && x.Fecha < fin && (x.Categoria == CategoriaMovimiento.Venta || x.Categoria == CategoriaMovimiento.Devolucion))
                .GroupBy(x => x.IdPedido!.Value).Select(g => new { Id = g.Key, Neto = g.Sum(x => x.Tipo == TipoMovimiento.Ingreso ? x.Monto : -x.Monto) }).ToDictionaryAsync(x => x.Id, x => x.Neto);
            var usadoDetalle = previas.GroupBy(x => x.IdDetallePedido).ToDictionary(g => g.Key, g => g.Sum(x => x.Unidades));
            foreach (var lote in lotes.Where(x => x.Asignaciones.Count > 0))
            {
                var quedan = lote.Unidades - previas.Where(x => x.IdLote == lote.Id).Sum(x => x.Unidades);
                foreach (var d in detalles.Where(x => x.IdProducto == lote.IdProducto && x.Pedido.FechaEntregaReal >= lote.DesdeUtc))
                {
                    if (quedan <= 0) break;
                    if (cobros.GetValueOrDefault(d.IdPedido) < d.Pedido.MontoTotal || d.Pedido.MontoTotal <= 0) continue;
                    var cantidad = Math.Min(quedan, d.Cantidad - usadoDetalle.GetValueOrDefault(d.Id));
                    if (cantidad <= 0) continue;
                    r.VentasNuevas.Add(new() { IdLote = lote.Id, IdDetallePedido = d.Id, Unidades = cantidad });
                    usadoDetalle[d.Id] = usadoDetalle.GetValueOrDefault(d.Id) + cantidad;
                    quedan -= cantidad;
                }
            }
        }
        r.UnidadesVendidas = ventasCorte.Concat(r.VentasNuevas)
            .GroupBy(x => x.IdLote).ToDictionary(g => g.Key, g => g.Sum(x => x.Unidades));
        foreach (var a in aportes)
        {
            decimal Generado(bool nuevas)
            {
                decimal costo = 0;
                foreach (var l in lotes)
                {
                    var unidades = ventasCorte.Where(x => x.IdLote == l.Id).Sum(x => x.Unidades)
                        + (nuevas ? r.VentasNuevas.Where(x => x.IdLote == l.Id).Sum(x => x.Unidades) : 0);
                    costo += l.Asignaciones.Where(x => x.IdCompra == a.IdCompra).Sum(x => x.Monto) * Math.Min(l.Unidades, unidades) / l.Unidades;
                }
                // Redondeo por acumulados: un centavo nunca se atribuye a dos aportantes.
                var anterior = a.Compra.Aportes.Where(x => x.Origen < a.Origen).Sum(x => x.Monto);
                decimal Parte(decimal acumulado) => decimal.Round(costo * acumulado / a.Compra.MontoTotal, 2, MidpointRounding.AwayFromZero);
                return Math.Min(a.Monto, Parte(anterior + a.Monto) - Parte(anterior));
            }
            var generado = Generado(true);
            var pagado = pagos.Where(x => x.IdAporte == a.Id).Sum(x => x.Monto);
            var reservado = reservas.Where(x => x.IdAporte == a.Id).Sum(x => x.Monto)
                - pagos.Where(x => x.IdAporte == a.Id && x.IdReserva.HasValue).Sum(x => x.Monto);
            r.Deudas.Add(new(a, generado, pagado, reservado));
            r.GeneradoNuevo += generado - Generado(false);
        }
        // Los adelantos de una persona compensan también sus otras compras.
        // Así no se propone un reintegro mientras aún tenga anticipos por absorber.
        foreach (var persona in new[] { OrigenFondos.Maxi, OrigenFondos.Ani })
        {
            var credito = r.Deudas.Where(x => x.Aporte.Origen == persona).Sum(x => Math.Max(0, x.Pagado + x.Reservado - x.Generado));
            for (var i = 0; i < r.Deudas.Count && credito > 0; i++)
            {
                var deuda = r.Deudas[i];
                if (deuda.Aporte.Origen != persona) continue;
                var aplicado = Math.Min(credito, deuda.Sugerido);
                r.Deudas[i] = deuda with { CreditoAdelantosAplicado = aplicado };
                credito -= aplicado;
            }
        }
        return r;
    }

    // El llamador guarda todo junto con el cierre, bajo el mismo bloqueo de caja.
    public static void PrepararCierre(PanaderiaContext db, CierreCaja cierre, FinanciacionResumen resumen, bool guardar = true)
    {
        var disponible = Math.Max(0, cierre.CobrosEfectivo + cierre.CobrosBanco + cierre.CobrosSinClasificar - cierre.Devoluciones - cierre.GastosReparto);
        cierre.ReintegrosProvisionados = 0;
        cierre.ReintegrosGenerados = resumen.GeneradoNuevo;
        cierre.AdelantosPersonales = resumen.AdelantosPeriodo;
        foreach (var deuda in resumen.Deudas)
        {
            var monto = Math.Min(disponible, deuda.Sugerido);
            if (monto <= 0) continue;
            if (guardar) db.ReservasReintegro.Add(new() { IdAporte = deuda.Aporte.Id, Cierre = cierre, Monto = monto });
            disponible -= monto;
            cierre.ReintegrosProvisionados += monto;
        }
        if (guardar) foreach (var venta in resumen.VentasNuevas) { venta.Cierre = cierre; db.VentasLote.Add(venta); }
    }

    public async Task PagarAsync(int aporte, int? reserva, CuentaCaja cuenta, decimal monto, Guid clave, string? usuario, DateTime? fechaPago = null)
    {
        ReglasCaja.ImporteValido(monto); ReglasCaja.CuentaValida(cuenta, true);
        if (clave == Guid.Empty) throw new InvalidOperationException("Falta la identificación del pago.");
        await using var tx = await ReglasCaja.IniciarAsync(db);
        if (await db.ReportesCaja.AnyAsync(x => x.ClaveOperacion == clave)) return;
        var r = await ResumenAsync();
        var deuda = r.Deudas.SingleOrDefault(x => x.Aporte.Id == aporte) ?? throw new InvalidOperationException("Aporte personal inexistente.");
        var fecha = fechaPago ?? DateTime.UtcNow;
        if (fecha.Kind != DateTimeKind.Utc || fecha > DateTime.UtcNow.AddMinutes(1) || fecha < deuda.Aporte.Compra.Fecha)
            throw new InvalidOperationException("La fecha del pago debe ser posterior a la compra y no futura.");
        if (reserva.HasValue)
        {
            var asignada = r.Reservas.SingleOrDefault(x => x.Id == reserva && x.IdAporte == aporte)
                ?? throw new InvalidOperationException("Reintegro del cierre inexistente.");
            var pendiente = asignada.Monto - r.Pagos.Where(x => x.IdReserva == reserva).Sum(x => x.Monto);
            if (monto > pendiente) throw new InvalidOperationException("El pago supera el reintegro pendiente del cierre.");
            if (fecha < asignada.Cierre.RegistradoUtc) throw new InvalidOperationException("El pago debe ser posterior al registro del cierre.");
            if (await db.CierresCaja.AnyAsync(x => x.Id != asignada.IdCierre && fecha >= x.InicioUtc && fecha < x.FinUtc))
                throw new InvalidOperationException("La fecha actual pertenece a otro período cerrado.");
        }
        else
        {
            await ReglasCaja.PeriodoAbiertoAsync(db, fecha);
            if (monto > deuda.DisponibleAdelanto) throw new InvalidOperationException("El adelanto supera la deuda sin reservar. Los reintegros de cierres se pagan desde su pendiente.");
        }
        if (monto > deuda.Deuda) throw new InvalidOperationException("El pago supera la deuda personal.");
        await new CierreCajaService(db).FondosAsync(cuenta, monto, fecha);
        var movimiento = new ReporteCaja { Fecha = fecha, Monto = monto, Cuenta = cuenta, Tipo = TipoMovimiento.Egreso,
            Categoria = CategoriaMovimiento.ReintegroPersonal, ClaveOperacion = clave,
            DescontarDelReparto = !reserva.HasValue,
            Descripcion = $"{(reserva.HasValue ? "Reintegro de cierre" : "Adelanto de insumos")} · {deuda.Aporte.Origen} · compra {deuda.Aporte.IdCompra}" };
        db.PagosReintegro.Add(new() { IdAporte = aporte, IdReserva = reserva, Movimiento = movimiento, Monto = monto, Usuario = usuario });
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
