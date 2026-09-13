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
            .Include(c => c.Proveedor)
            .Include(c => c.Detalles).ThenInclude(d => d.Insumo)
            .Include(c => c.Detalles).ThenInclude(d => d.UnidadCompra)
            .OrderByDescending(c => c.Fecha)
            .ToListAsync();
    }

    public async Task<CompraProveedor?> GetByIdAsync(int id)
    {
        return await _context.ComprasProveedor
            .Include(c => c.Proveedor)
            .Include(c => c.Detalles).ThenInclude(d => d.Insumo)
            .Include(c => c.Detalles).ThenInclude(d => d.UnidadCompra)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task CreateAsync(CompraProveedor compra)
    {
        ReglasCaja.CuentaValida(compra.CuentaPago);
        if (compra.ClaveOperacion == Guid.Empty || compra.Detalles.Count == 0 || compra.Detalles.Any(d => d.Cantidad <= 0 || d.PrecioUnitario < 0 || d.CostoEnvio < 0))
            throw new InvalidOperationException("Revisá los importes y cantidades de la compra.");
        if (compra.CuentaPago == CuentaCaja.ReservaMercadoPago && compra.DescontarDelReparto)
            throw new InvalidOperationException("Las compras desde la reserva no se descuentan del reparto semanal.");
        compra.Fecha = DateTime.SpecifyKind(compra.Fecha, DateTimeKind.Utc);
        await using var transaction = await ReglasCaja.IniciarAsync(_context);
        if (compra.ClaveOperacion.HasValue && await _context.ReportesCaja.AnyAsync(r => r.ClaveOperacion == compra.ClaveOperacion)) return;
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
        _context.ComprasProveedor.Add(compra);

        var proveedor = await _context.Proveedores.FindAsync(compra.IdProveedor);
        _context.ReportesCaja.Add(new ReporteCaja
        {
            Fecha       = compra.Fecha,
            Tipo        = TipoMovimiento.Egreso,
            Categoria   = CategoriaMovimiento.Proveedor,
            Monto       = compra.MontoTotal,
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
