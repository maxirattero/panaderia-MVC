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
        compra.Fecha = DateTime.SpecifyKind(compra.Fecha, DateTimeKind.Utc);

        foreach (var detalle in compra.Detalles)
        {
            var insumo = await _context.Insumos.FindAsync(detalle.IdInsumo);
            var unidad = await _context.UnidadesCompra.FindAsync(detalle.IdUnidadCompra);

            detalle.Subtotal = detalle.Cantidad * detalle.PrecioUnitario;

            if (insumo != null && unidad != null)
            {
                insumo.StockActual += detalle.Cantidad * unidad.FactorConversion;
                insumo.PrecioCompra = detalle.PrecioUnitario / unidad.FactorConversion;
            }
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
            Descripcion = $"Compra - {proveedor?.Nombre ?? "Proveedor"}"
        });

        await _context.SaveChangesAsync();
    }
}
