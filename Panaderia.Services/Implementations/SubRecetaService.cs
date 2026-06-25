using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.Services.Implementations;

public class SubRecetaService : ISubRecetaService
{
    private readonly PanaderiaContext _context;

    public SubRecetaService(PanaderiaContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SubReceta>> GetAllAsync()
    {
        return await _context.SubRecetas
            .Include(s => s.Detalles).ThenInclude(d => d.Insumo)
            .OrderBy(s => s.Nombre)
            .ToListAsync();
    }

    public async Task<SubReceta?> GetByIdAsync(int id)
    {
        return await _context.SubRecetas
            .Include(s => s.Detalles).ThenInclude(d => d.Insumo)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task CreateAsync(SubReceta subReceta)
    {
        _context.SubRecetas.Add(subReceta);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SubReceta subReceta)
    {
        var existe = await _context.SubRecetas
            .Include(s => s.Detalles)
            .FirstOrDefaultAsync(s => s.Id == subReceta.Id);

        if (existe == null) return;

        existe.Nombre          = subReceta.Nombre;
        existe.Notas           = subReceta.Notas;
        existe.MargenSeguridad = subReceta.MargenSeguridad;

        _context.SubRecetaDetalles.RemoveRange(existe.Detalles);
        existe.Detalles.Clear();

        foreach (var d in subReceta.Detalles ?? new())
            existe.Detalles.Add(new SubRecetaDetalle
            {
                IdInsumo           = d.IdInsumo,
                PorcentajePanadero = d.PorcentajePanadero,
                CantidadFija       = d.CantidadFija
            });

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _context.SubRecetaDetalles
            .Where(d => d.IdSubReceta == id).ExecuteDeleteAsync();
        await _context.SubRecetas
            .Where(s => s.Id == id).ExecuteDeleteAsync();
    }
}
