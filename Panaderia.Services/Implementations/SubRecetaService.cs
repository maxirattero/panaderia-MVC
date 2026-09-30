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
        return (await GrafoSubRecetas.CargarAsync(_context)).Values;
    }

    public async Task<SubReceta?> GetByIdAsync(int id)
    {
        return (await GrafoSubRecetas.CargarAsync(_context)).GetValueOrDefault(id);
    }

    public async Task CreateAsync(SubReceta subReceta)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(72631, 2)");
        await GrafoSubRecetas.ValidarAsync(_context, subReceta);
        _context.SubRecetas.Add(subReceta);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task UpdateAsync(SubReceta subReceta)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(72631, 2)");
        await GrafoSubRecetas.ValidarAsync(_context, subReceta);
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
                IdSubRecetaIngrediente = d.IdSubRecetaIngrediente,
                PorcentajePanadero = d.PorcentajePanadero,
                CantidadFija       = d.CantidadFija
            });

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var tx = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(72631, 2)");
        if (await _context.RecetaDetalles.AnyAsync(d => d.IdSubReceta == id) ||
            await _context.SubRecetaDetalles.AnyAsync(d => d.IdSubRecetaIngrediente == id))
            throw new InvalidOperationException("No se puede eliminar una sub-receta utilizada por otra receta o sub-receta.");
        await _context.SubRecetaDetalles
            .Where(d => d.IdSubReceta == id).ExecuteDeleteAsync();
        await _context.SubRecetas
            .Where(s => s.Id == id).ExecuteDeleteAsync();
        await tx.CommitAsync();
    }
}
