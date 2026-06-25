using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations;

public class RecetaService : IRecetaService
{
    private readonly PanaderiaContext _context;

    public RecetaService(PanaderiaContext context)
    {
        _context = context;
    }

    public async Task<Receta?> GetByProductoIdAsync(int idProducto)
    {
        return await _context.Recetas
            .Include(r => r.Detalles)
                .ThenInclude(d => d.Insumo)
            .Include(r => r.Detalles)
                .ThenInclude(d => d.SubReceta)
                    .ThenInclude(s => s.Detalles)
                        .ThenInclude(sd => sd.Insumo)
            .Include(r => r.Producto)
                .ThenInclude(p => p.Categoria)
            .Include(r => r.Producto)
                .ThenInclude(p => p.Formato)
            .FirstOrDefaultAsync(r => r.IdProducto == idProducto);
    }

    public async Task UpsertAsync(Receta receta)
    {
        receta.Detalles ??= new List<RecetaDetalle>();

        var existing = await _context.Recetas
            .Include(r => r.Detalles)
            .FirstOrDefaultAsync(r => r.IdProducto == receta.IdProducto);

        if (existing == null)
        {
            receta.FechaCreacion = DateTime.UtcNow;
            _context.Recetas.Add(receta);
        }
        else
        {
            existing.TamanioLote = receta.TamanioLote;
            existing.PesoUnitario = receta.PesoUnitario;
            existing.FechaModificacion = DateTime.UtcNow;
            _context.RecetaDetalles.RemoveRange(existing.Detalles);
            existing.Detalles.Clear();
            foreach (var d in receta.Detalles)
                existing.Detalles.Add(new RecetaDetalle
                {
                    IdInsumo           = d.IdInsumo,
                    IdSubReceta        = d.IdSubReceta,
                    PorcentajePanadero = d.PorcentajePanadero,
                    CantidadFija       = d.CantidadFija
                });
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int idReceta)
    {
        await _context.RecetaDetalles
            .Where(d => d.IdReceta == idReceta).ExecuteDeleteAsync();
        await _context.Recetas
            .Where(r => r.Id == idReceta).ExecuteDeleteAsync();
    }
}
