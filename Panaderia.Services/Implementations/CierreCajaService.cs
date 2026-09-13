using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations;

public class CierreCajaService(PanaderiaContext db) : ICierreCajaService
{
    public Task<ConfiguracionCaja> ConfiguracionAsync() => db.ConfiguracionCaja.AsNoTracking().SingleAsync();
    public Task<List<CierreCaja>> HistorialAsync() => db.CierresCaja.AsNoTracking().OrderByDescending(c => c.InicioSemana).ThenByDescending(c => c.Id).ToListAsync();

    public async Task ConfigurarAsync(decimal porcentaje, DateTime? inicio, decimal efectivo, decimal banco, decimal reserva, string? usuario)
    {
        ValidarPorcentaje(porcentaje);
        await using var tx = await ReglasCaja.IniciarAsync(db);
        var config = await db.ConfiguracionCaja.SingleAsync();
        config.PorcentajeReserva = porcentaje;
        if (inicio.HasValue)
        {
            if (config.InicioSaldosUtc.HasValue) throw new InvalidOperationException("Los saldos de apertura ya están registrados. Usá un ajuste de caja para corregirlos.");
            if (inicio.Value.Kind != DateTimeKind.Utc || inicio > DateTime.UtcNow || efectivo < 0 || banco < 0 || reserva < 0
                || new[] { efectivo, banco, reserva }.Any(x => x != decimal.Round(x, 2)))
                throw new InvalidOperationException("Revisá la fecha y los saldos de apertura.");
            config.InicioSaldosUtc = inicio;
            config.AperturaEfectivo = efectivo;
            config.AperturaBanco = banco;
            config.AperturaReserva = reserva;
        }
        config.FechaConfiguracion = DateTime.UtcNow;
        config.UsuarioConfiguracion = usuario;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static decimal Apertura(ConfiguracionCaja c, CuentaCaja cuenta) => cuenta switch
    { CuentaCaja.Efectivo => c.AperturaEfectivo, CuentaCaja.Banco => c.AperturaBanco, _ => c.AperturaReserva };
    private static decimal Signado(ReporteCaja r) => r.Tipo == TipoMovimiento.Ingreso ? r.Monto : -r.Monto;
    public async Task<List<SaldoCuentaCaja>> SaldosAsync(DateTime? hasta = null)
    {
        var config = await ConfiguracionAsync();
        if (config.InicioSaldosUtc is not DateTime inicio || (hasta.HasValue && hasta < inicio)) return [];
        var q = db.ReportesCaja.AsNoTracking().Where(r => r.Fecha >= inicio);
        if (hasta.HasValue) q = q.Where(r => r.Fecha < hasta);
        var totales = await q.GroupBy(r => r.Cuenta).Select(g => new { Cuenta = g.Key, Total = g.Sum(r => r.Tipo == TipoMovimiento.Ingreso ? r.Monto : -r.Monto) }).ToListAsync();
        return new[] { CuentaCaja.Efectivo, CuentaCaja.Banco, CuentaCaja.ReservaMercadoPago }
            .Select(c => new SaldoCuentaCaja(c, Apertura(config, c) + (totales.FirstOrDefault(t => t.Cuenta == c)?.Total ?? 0m))).ToList();
    }

    private async Task<CajaResumen> CompletarAsync(CierreCaja cierre)
    {
        var pagos = await db.ReportesCaja.AsNoTracking().Where(r => r.IdCierreDestino == cierre.Id && r.Tipo == TipoMovimiento.Egreso).ToListAsync();
        return new CajaResumen {
            Cierre = cierre, SaldosConfigurados = (await ConfiguracionAsync()).InicioSaldosUtc.HasValue,
            SaldosActuales = await SaldosAsync(),
            ReservaPagada = pagos.Where(r => r.Destino == DestinoCierre.Reserva).Sum(r => r.Monto),
            AniPagado = pagos.Where(r => r.Destino == DestinoCierre.Ani).Sum(r => r.Monto),
            MaxiPagado = pagos.Where(r => r.Destino == DestinoCierre.Maxi).Sum(r => r.Monto)
        };
    }
    public async Task<CajaResumen> DetalleAsync(int id)
    {
        var cierre = await db.CierresCaja.AsNoTracking().Include(c => c.Costos).Include(c => c.Saldos).SingleOrDefaultAsync(c => c.Id == id)
            ?? throw new InvalidOperationException("No se encontró el cierre.");
        return await CompletarAsync(cierre);
    }
    public async Task<CajaResumen> ResumenAsync(DateOnly semana)
    {
        if (semana != CalendarioCaja.Lunes(semana) || semana > CalendarioCaja.Hoy)
            throw new InvalidOperationException("Elegí el lunes de una semana válida.");
        var existente = await db.CierresCaja.AsNoTracking().Include(c => c.Costos).Include(c => c.Saldos)
            .Where(c => c.InicioSemana == semana).OrderBy(c => c.EsHistorico).ThenBy(c => c.Id).FirstOrDefaultAsync();
        if (existente != null) return await CompletarAsync(existente);
        var config = await ConfiguracionAsync();
        var inicio = CalendarioCaja.InicioUtc(semana);
        var fin = CalendarioCaja.InicioUtc(semana.AddDays(7));
        var movimientos = await db.ReportesCaja.AsNoTracking().Where(r => r.Fecha >= inicio && r.Fecha < fin).ToListAsync();
        var ventas = movimientos.Where(r => r.Categoria == CategoriaMovimiento.Venta && r.Tipo == TipoMovimiento.Ingreso).ToList();
        var cierre = new CierreCaja {
            InicioSemana = semana, InicioUtc = inicio, FinUtc = fin, PorcentajeReserva = config.PorcentajeReserva,
            CobrosEfectivo = ventas.Where(r => r.Cuenta == CuentaCaja.Efectivo).Sum(r => r.Monto),
            CobrosBanco = ventas.Where(r => r.Cuenta == CuentaCaja.Banco).Sum(r => r.Monto),
            CobrosSinClasificar = ventas.Where(r => r.Cuenta == CuentaCaja.SinClasificar).Sum(r => r.Monto),
            Devoluciones = movimientos.Where(r => r.Tipo == TipoMovimiento.Egreso && r.Categoria == CategoriaMovimiento.Devolucion).Sum(r => r.Monto),
            GastosReparto = movimientos.Where(r => r.Tipo == TipoMovimiento.Egreso && r.DescontarDelReparto && r.Categoria != CategoriaMovimiento.Devolucion).Sum(r => r.Monto)
        };
        Repartir(cierre, config.PorcentajeReserva);
        // Las fechas programadas antiguas son fechas civiles etiquetadas UTC.
        var fechaCivil = DateTime.SpecifyKind(semana.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var finCivil = fechaCivil.AddDays(7);
        var pedidos = await db.Pedidos.AsNoTracking().Include(p => p.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.Categoria)
            .Include(p => p.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p.Formato)
            .Where(p => p.Estado == EstadoPedido.Entregado && (p.FechaEntregaReal.HasValue
                ? p.FechaEntregaReal >= inicio && p.FechaEntregaReal < fin
                : p.FechaEntrega >= fechaCivil && p.FechaEntrega < finCivil)).ToListAsync();
        var detalles = pedidos.SelectMany(p => p.Detalles).ToList();
        var ids = detalles.Select(d => d.IdProducto).Distinct().ToList();
        var recetas = await db.Recetas.AsNoTracking().Include(r => r.Detalles).ThenInclude(d => d.Insumo)
            .Include(r => r.Detalles).ThenInclude(d => d.SubReceta).ThenInclude(s => s!.Detalles).ThenInclude(d => d.Insumo)
            .Where(r => ids.Contains(r.IdProducto)).ToDictionaryAsync(r => r.IdProducto);
        cierre.TotalVendido = pedidos.Sum(p => p.MontoTotal);
        foreach (var grupo in detalles.GroupBy(d => d.IdProducto))
        {
            var fallback = recetas.GetValueOrDefault(grupo.Key)?.CostoIngredientesPorUnidad;
            var lineas = grupo.ToList();
            cierre.Costos.Add(new CierreCajaCosto {
                IdProducto = grupo.Key, Producto = lineas[0].Producto.NombreVisible, Cantidad = lineas.Sum(d => d.Cantidad),
                Ingredientes = lineas.Sum(d => d.Cantidad * (d.CostoIngredientes ?? fallback ?? 0m)),
                Empaque = lineas.Sum(d => d.Cantidad * d.CostoEmpaque),
                Reconstruido = lineas.Any(d => d.CostoIngredientes == null),
                CostoPendiente = lineas.Any(d => (d.CostoIngredientes ?? fallback ?? 0m) <= 0)
            });
        }
        cierre.CostoInsumos = cierre.Costos.Sum(c => c.Ingredientes + c.Empaque);
        if (config.InicioSaldosUtc is DateTime corte && corte < fin)
        {
            var desde = corte > inicio ? corte : inicio;
            var saldosInicio = await SaldosAsync(desde);
            var periodo = movimientos.Where(r => r.Fecha >= desde).ToList();
            foreach (var s in saldosInicio)
            {
                var entradas = periodo.Where(r => r.Cuenta == s.Cuenta && r.Tipo == TipoMovimiento.Ingreso).Sum(r => r.Monto);
                var salidas = periodo.Where(r => r.Cuenta == s.Cuenta && r.Tipo == TipoMovimiento.Egreso).Sum(r => r.Monto);
                cierre.Saldos.Add(new() { Cuenta = s.Cuenta, Inicial = s.Saldo, Entradas = entradas, Salidas = salidas, Final = s.Saldo + entradas - salidas });
            }
        }
        return new CajaResumen {
            Cierre = cierre, SaldosConfigurados = config.InicioSaldosUtc.HasValue, SaldosActuales = await SaldosAsync(),
            SinClasificar = movimientos.Where(r => r.Cuenta == CuentaCaja.SinClasificar).OrderBy(r => r.Fecha).ToList()
        };
    }

    public static void Repartir(CierreCaja cierre, decimal porcentaje)
    {
        ValidarPorcentaje(porcentaje);
        cierre.PorcentajeReserva = porcentaje;
        cierre.BaseReparto = cierre.CobrosEfectivo + cierre.CobrosBanco + cierre.CobrosSinClasificar - cierre.Devoluciones - cierre.GastosReparto;
        var basePositiva = Math.Max(0, cierre.BaseReparto);
        cierre.Reserva = decimal.Round(basePositiva * porcentaje / 100m, 2, MidpointRounding.AwayFromZero);
        cierre.Ani = decimal.Round((basePositiva - cierre.Reserva) / 2m, 2, MidpointRounding.AwayFromZero);
        cierre.Maxi = basePositiva - cierre.Reserva - cierre.Ani;
    }
    private static void ValidarPorcentaje(decimal porcentaje)
    {
        if (porcentaje < 0 || porcentaje > 100 || porcentaje != decimal.Round(porcentaje, 2))
            throw new InvalidOperationException("El porcentaje debe estar entre 0 y 100, con hasta dos decimales.");
    }

    public async Task<int> CerrarAsync(DateOnly semana, decimal porcentaje, string? notas, string? usuario, bool aceptarCostosPendientes)
    {
        await using var tx = await ReglasCaja.IniciarAsync(db);
        var resumen = await ResumenAsync(semana);
        if (resumen.Guardado) throw new InvalidOperationException("Esa semana ya tiene un cierre registrado.");
        if (semana.AddDays(6) > CalendarioCaja.Hoy) throw new InvalidOperationException("Podés confirmar el cierre desde el domingo de esa semana.");
        if (resumen.SinClasificar.Any()) throw new InvalidOperationException("Clasificá el origen de los movimientos de la semana antes de cerrar.");
        if (resumen.Cierre.Costos.Any(c => c.CostoPendiente) && !aceptarCostosPendientes)
            throw new InvalidOperationException("Hay productos sin costo completo. Revisalos o confirmá que querés guardar el cierre con esa advertencia.");
        Repartir(resumen.Cierre, porcentaje);
        resumen.Cierre.RegistradoUtc = DateTime.UtcNow;
        resumen.Cierre.Usuario = usuario;
        resumen.Cierre.Notas = notas?.Trim();
        db.CierresCaja.Add(resumen.Cierre);
        await db.SaveChangesAsync();
        await db.ReportesCaja.Where(r => r.Fecha >= resumen.Cierre.InicioUtc && r.Fecha < resumen.Cierre.FinUtc && r.FechaInicioPeriodo == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IdCierre, resumen.Cierre.Id));
        var config = await db.ConfiguracionCaja.SingleAsync();
        config.PorcentajeReserva = porcentaje;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return resumen.Cierre.Id;
    }

    private async Task FondosAsync(CuentaCaja cuenta, decimal monto, DateTime fecha)
    {
        var config = await ConfiguracionAsync();
        if (config.InicioSaldosUtc is not DateTime inicio || fecha < inicio)
            throw new InvalidOperationException("Configurá los saldos iniciales y elegí una fecha posterior al inicio de esos saldos.");
        if (await db.ReportesCaja.AnyAsync(r => r.Fecha >= inicio && r.Cuenta == CuentaCaja.SinClasificar))
            throw new InvalidOperationException("Hay movimientos sin clasificar desde la apertura. Identificá sus cuentas antes de registrar transferencias o retiros.");
        // Una operación retroactiva no puede dejar saldos intermedios negativos.
        var movimientos = await db.ReportesCaja.AsNoTracking().Where(r => r.Cuenta == cuenta && r.Fecha >= inicio).OrderBy(r => r.Fecha).ThenBy(r => r.Id).ToListAsync();
        var saldo = Apertura(config, cuenta) + movimientos.Where(r => r.Fecha <= fecha).Sum(Signado);
        if (saldo < monto) throw new InvalidOperationException("No alcanza el saldo de la cuenta elegida en esa fecha.");
        saldo -= monto;
        foreach (var r in movimientos.Where(r => r.Fecha > fecha))
        {
            saldo += Signado(r);
            if (saldo < 0) throw new InvalidOperationException("Ese pago dejaría sin fondos una operación posterior. Revisá fecha y cuenta.");
        }
    }

    public async Task RegistrarPagoAsync(int id, DestinoCierre destino, CuentaCaja cuenta, decimal monto, DateTime fecha, Guid clave)
    {
        ReglasCaja.CuentaValida(cuenta, true); ReglasCaja.ImporteValido(monto);
        if (!Enum.IsDefined(destino) || clave == Guid.Empty) throw new InvalidOperationException("Revisá el destino del pago.");
        await using var tx = await ReglasCaja.IniciarAsync(db);
        if (await db.ReportesCaja.AnyAsync(r => r.ClaveOperacion == clave)) return;
        var resumen = await DetalleAsync(id);
        if (resumen.Cierre.EsHistorico) throw new InvalidOperationException("El cierre histórico no tiene un reparto individual registrado.");
        if (fecha.Kind != DateTimeKind.Utc || fecha > DateTime.UtcNow.AddMinutes(1) || fecha < resumen.Cierre.RegistradoUtc)
            throw new InvalidOperationException("Usá una fecha posterior al registro del cierre y no futura.");
        if (await db.CierresCaja.AnyAsync(c => c.Id != id && fecha >= c.InicioUtc && fecha < c.FinUtc))
            throw new InvalidOperationException("La fecha pertenece a otro período cerrado. Registrá el pago en una fecha abierta.");
        if (monto > resumen.Pendiente(destino)) throw new InvalidOperationException("El importe supera lo pendiente para ese destino.");
        await FondosAsync(cuenta, monto, fecha);
        db.ReportesCaja.Add(new() { Fecha = fecha, Tipo = TipoMovimiento.Egreso,
            Categoria = destino == DestinoCierre.Reserva ? CategoriaMovimiento.Transferencia : CategoriaMovimiento.Recaudacion,
            Cuenta = cuenta, Monto = monto, IdCierreDestino = id, Destino = destino, ClaveOperacion = clave,
            IdTransferencia = destino == DestinoCierre.Reserva ? clave : null, Descripcion = $"Cierre {resumen.Cierre.InicioSemana:dd/MM/yyyy} · {destino}" });
        if (destino == DestinoCierre.Reserva)
            db.ReportesCaja.Add(new() { Fecha = fecha, Tipo = TipoMovimiento.Ingreso, Categoria = CategoriaMovimiento.Transferencia,
                Cuenta = CuentaCaja.ReservaMercadoPago, Monto = monto, IdTransferencia = clave, IdCierreDestino = id, Destino = destino,
                Descripcion = $"Reserva del cierre {resumen.Cierre.InicioSemana:dd/MM/yyyy}" });
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }

    public async Task TransferirAsync(CuentaCaja origen, CuentaCaja destino, decimal monto, DateTime fecha, Guid clave)
    {
        ReglasCaja.CuentaValida(origen); ReglasCaja.CuentaValida(destino); ReglasCaja.ImporteValido(monto);
        if (origen == destino || clave == Guid.Empty) throw new InvalidOperationException("Elegí dos cuentas distintas.");
        await using var tx = await ReglasCaja.IniciarAsync(db);
        if (await db.ReportesCaja.AnyAsync(r => r.ClaveOperacion == clave)) return;
        await ReglasCaja.PeriodoAbiertoAsync(db, fecha);
        await FondosAsync(origen, monto, fecha);
        db.ReportesCaja.AddRange(
            new ReporteCaja { Fecha = fecha, Cuenta = origen, Tipo = TipoMovimiento.Egreso, Categoria = CategoriaMovimiento.Transferencia, Monto = monto, IdTransferencia = clave, ClaveOperacion = clave, Descripcion = $"Transferencia a {destino}" },
            new ReporteCaja { Fecha = fecha, Cuenta = destino, Tipo = TipoMovimiento.Ingreso, Categoria = CategoriaMovimiento.Transferencia, Monto = monto, IdTransferencia = clave, Descripcion = $"Transferencia desde {origen}" });
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task ClasificarAsync(int idMovimiento, CuentaCaja cuenta)
    {
        ReglasCaja.CuentaValida(cuenta);
        await using var tx = await ReglasCaja.IniciarAsync(db);
        var r = await db.ReportesCaja.SingleOrDefaultAsync(r => r.Id == idMovimiento) ?? throw new InvalidOperationException("Movimiento inexistente.");
        if (r.Cuenta != CuentaCaja.SinClasificar || r.IdCierre.HasValue) throw new InvalidOperationException("El movimiento ya está clasificado o incluido en un cierre nuevo.");
        ReglasCaja.CuentaValida(cuenta, r.Categoria == CategoriaMovimiento.Venta);
        r.Cuenta = cuenta;
        if (cuenta == CuentaCaja.ReservaMercadoPago) r.DescontarDelReparto = false;
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
}
