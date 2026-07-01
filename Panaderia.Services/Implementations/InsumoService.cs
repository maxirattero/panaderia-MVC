using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations;

public class InsumoService : IInsumoService
{
    private readonly PanaderiaContext _context;

    public InsumoService(PanaderiaContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Insumo>> GetAllAsync()
    {
        return await _context.Insumos
            .Include(i => i.Proveedor)
            .Include(i => i.UnidadesCompra)
            .OrderBy(i => i.Nombre)
            .ToListAsync();
    }

    public async Task<Insumo?> GetByIdAsync(int id)
    {
        return await _context.Insumos
            .Include(i => i.Proveedor)
            .Include(i => i.UnidadesCompra)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task CreateAsync(Insumo insumo)
    {
        await _context.Insumos.AddAsync(insumo);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Insumo insumo)
    {
        var existe = await _context.Insumos
            .Include(i => i.UnidadesCompra)
            .FirstOrDefaultAsync(i => i.Id == insumo.Id);
        if (existe == null) return;

        existe.Nombre = insumo.Nombre;
        existe.UnidadBase = insumo.UnidadBase;
        existe.PrecioCompra = insumo.PrecioCompra;
        existe.CantidadRendimiento = insumo.CantidadRendimiento;
        existe.IdProveedor = insumo.IdProveedor;
        existe.Notas = insumo.Notas;
        existe.TipoInsumo = insumo.TipoInsumo;
        existe.Activo = insumo.Activo;
        existe.StockActual = insumo.StockActual;
        existe.StockMinimo = insumo.StockMinimo;
        existe.FechaModificacion = DateTime.UtcNow;

        var entrantes = insumo.UnidadesCompra ?? new();
        var idsEntrantes = entrantes.Where(u => u.Id != 0).Select(u => u.Id).ToHashSet();

        var aEliminar = existe.UnidadesCompra
            .Where(u => !idsEntrantes.Contains(u.Id))
            .ToList();

        if (aEliminar.Count > 0)
        {
            var idsAEliminar = aEliminar.Select(u => u.Id).ToHashSet();
            var tieneCompras = await _context.ComprasDetalle
                .AnyAsync(d => idsAEliminar.Contains(d.IdUnidadCompra));
            if (tieneCompras)
                throw new InvalidOperationException(
                    "No se puede eliminar una unidad de compra que ya tiene compras registradas. " +
                    "Podés desactivar el insumo o simplemente dejar de usar esa unidad en nuevas compras.");
            _context.UnidadesCompra.RemoveRange(aEliminar);
        }

        foreach (var u in entrantes)
        {
            if (u.Id != 0)
            {
                var existente = existe.UnidadesCompra.FirstOrDefault(e => e.Id == u.Id);
                if (existente != null)
                {
                    existente.Nombre           = u.Nombre;
                    existente.FactorConversion = u.FactorConversion;
                    existente.EsPorDefecto     = u.EsPorDefecto;
                }
            }
            else
            {
                existe.UnidadesCompra.Add(new UnidadCompra
                {
                    IdInsumo         = existe.Id,
                    Nombre           = u.Nombre,
                    FactorConversion = u.FactorConversion,
                    EsPorDefecto     = u.EsPorDefecto
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _context.Insumos.Where(i => i.Id == id).ExecuteDeleteAsync();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Insumos.AnyAsync(i => i.Id == id);
    }

    public async Task<IEnumerable<Insumo>> GetEmpaquesAsync()
    {
        return await _context.Insumos
            .Where(i => i.TipoInsumo == TipoInsumo.Empaque && i.Activo)
            .OrderBy(i => i.Nombre)
            .ToListAsync();
    }

    public async Task<decimal> GetCostoEtiquetaAsync()
    {
        var etiqueta = await _context.Insumos
            .FirstOrDefaultAsync(i => i.TipoInsumo == TipoInsumo.Etiqueta);
        if (etiqueta == null || etiqueta.CantidadRendimiento == 0) return 0m;
        return etiqueta.PrecioCompra / etiqueta.CantidadRendimiento;
    }
}
