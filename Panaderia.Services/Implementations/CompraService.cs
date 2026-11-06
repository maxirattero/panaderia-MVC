using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations;

public class CompraService : ICompraService
{
    private readonly PanaderiaContext _context;

    public CompraService(PanaderiaContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CompraProveedor>> GetAllAsync()
    {
        return await _context.ComprasProveedor
            .Include(c => c.Aportes)
            .Include(c => c.Proveedor)
            .Include(c => c.Detalles).ThenInclude(d => d.Insumo)
            .Include(c => c.Detalles).ThenInclude(d => d.UnidadCompra)
            .OrderByDescending(c => c.Fecha)
            .ToListAsync();
    }

    public async Task<CompraProveedor?> GetByIdAsync(int id)
    {
        return await _context.ComprasProveedor
            .Include(c => c.Aportes)
            .Include(c => c.Proveedor)
            .Include(c => c.Detalles).ThenInclude(d => d.Insumo)
            .Include(c => c.Detalles).ThenInclude(d => d.UnidadCompra)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task CreateAsync(CompraProveedor compra)
    {
        ReglasCaja.CuentaValida(compra.CuentaPago);
        if (!compra.ClaveOperacion.HasValue || compra.ClaveOperacion == Guid.Empty || compra.Detalles.Count == 0 || compra.Detalles.Any(d => d.Cantidad <= 0 || d.PrecioUnitario < 0 || d.CostoEnvio < 0))
            throw new InvalidOperationException("Revisá los importes y cantidades de la compra.");
        if (compra.CuentaPago == CuentaCaja.ReservaMercadoPago && compra.DescontarDelReparto)
            throw new InvalidOperationException("Las compras desde la reserva no se descuentan del reparto semanal.");
        compra.Fecha = DateTime.SpecifyKind(compra.Fecha, DateTimeKind.Utc);
        await using var transaction = await ReglasCaja.IniciarAsync(_context);
        if (await _context.ComprasProveedor.AnyAsync(r => r.ClaveOperacion == compra.ClaveOperacion)
            || await _context.ReportesCaja.AnyAsync(r => r.ClaveOperacion == compra.ClaveOperacion)) return;
        await ReglasCaja.PeriodoAbiertoAsync(_context, compra.Fecha);

        foreach (var detalle in compra.Detalles)
        {
            var insumo = await _context.Insumos.FindAsync(detalle.IdInsumo);
            var unidad = await _context.UnidadesCompra.FindAsync(detalle.IdUnidadCompra);

            if (insumo == null || unidad == null || unidad.IdInsumo != detalle.IdInsumo)
                throw new InvalidOperationException("La unidad de compra seleccionada no corresponde al insumo.");

            detalle.Subtotal = detalle.Cantidad * detalle.PrecioUnitario + detalle.CostoEnvio;

            insumo.StockActual += detalle.Cantidad * unidad.FactorConversion;
            var costoEnvioPorUnidad = detalle.Cantidad > 0 ? detalle.CostoEnvio / detalle.Cantidad : 0m;
            insumo.PrecioCompra = detalle.PrecioUnitario + costoEnvioPorUnidad;
            insumo.CantidadRendimiento = unidad.FactorConversion;
        }

        compra.MontoTotal = compra.Detalles.Sum(d => d.Subtotal);
        ReglasCaja.ImporteValido(compra.MontoTotal);
        // Sin selección explícita se conserva el comportamiento de compras anteriores.
        if (compra.Aportes.Count == 0)
            compra.Aportes.Add(new() { Origen = OrigenFondos.MasaViva, Cuenta = compra.CuentaPago, Monto = compra.MontoTotal });
        if (compra.Aportes.Select(a => a.Origen).Distinct().Count() != compra.Aportes.Count || compra.Aportes.Sum(a => a.Monto) != compra.MontoTotal)
            throw new InvalidOperationException("Los aportes deben sumar exactamente el total de la compra, incluido el envío.");
        foreach (var aporte in compra.Aportes)
        {
            ReglasCaja.ImporteValido(aporte.Monto);
            if (!Enum.IsDefined(aporte.Origen) || (aporte.Origen != OrigenFondos.MasaViva && aporte.Cuenta.HasValue))
                throw new InvalidOperationException("Revisá el origen de los aportes personales.");
            if (aporte.Origen == OrigenFondos.MasaViva)
            {
                if (!aporte.Cuenta.HasValue || aporte.Cuenta != compra.CuentaPago)
                    throw new InvalidOperationException("Elegí la cuenta de Masa Viva utilizada en la compra.");
                ReglasCaja.CuentaValida(aporte.Cuenta.Value);
            }
        }
        _context.ComprasProveedor.Add(compra);

        var proveedor = await _context.Proveedores.FindAsync(compra.IdProveedor);
        var fondosMasaViva = compra.Aportes.SingleOrDefault(a => a.Origen == OrigenFondos.MasaViva);
        if (fondosMasaViva != null) _context.ReportesCaja.Add(new ReporteCaja
        {
            Fecha       = compra.Fecha,
            Tipo        = TipoMovimiento.Egreso,
            Categoria   = CategoriaMovimiento.Proveedor,
            Monto       = fondosMasaViva.Monto,
            IdProveedor = compra.IdProveedor,
            Compra = compra,
            Cuenta = compra.CuentaPago,
            ClaveOperacion = compra.ClaveOperacion,
            DescontarDelReparto = compra.DescontarDelReparto,
            Descripcion = $"Compra - {proveedor?.Nombre ?? "Proveedor"}"
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
