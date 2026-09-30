using Microsoft.EntityFrameworkCore;
using Panaderia.Models.Data;
using Panaderia.Models.Entities;

namespace Panaderia.Services.Implementations;

// Carga el grafo completo en una consulta, sin fijar un límite de niveles.
public static class GrafoSubRecetas
{
    public static async Task ValidarAsync(PanaderiaContext context, SubReceta receta)
    {
        if (string.IsNullOrWhiteSpace(receta.Nombre) || receta.MargenSeguridad is < 0 or > 1)
            throw new InvalidOperationException("Indicá un nombre y un margen de seguridad entre 0 y 1.");
        if (receta.Detalles == null || receta.Detalles.Count == 0)
            throw new InvalidOperationException("Agregá al menos un ingrediente o una sub-receta.");
        var insumos = await context.Insumos.AsNoTracking().ToDictionaryAsync(i => i.Id);
        var ids = await context.SubRecetas.AsNoTracking().Select(s => s.Id).ToListAsync();
        foreach (var d in receta.Detalles)
        {
            if (d.IdInsumo.HasValue == d.IdSubRecetaIngrediente.HasValue)
                throw new InvalidOperationException("Cada fila debe tener un insumo o una sub-receta.");
            if (d.IdSubRecetaIngrediente is int id && !ids.Contains(id))
                throw new InvalidOperationException("Una de las sub-recetas seleccionadas no existe.");
            if (d.IdInsumo is int insumoId && (!insumos.TryGetValue(insumoId, out var insumo) || insumo.TipoInsumo == Panaderia.Models.Enums.TipoInsumo.Consumible))
                throw new InvalidOperationException("Uno de los insumos seleccionados no es válido para una sub-receta.");
            var fija = d.IdInsumo is int iid && insumos[iid].UnidadBase == Panaderia.Models.Enums.UnidadMedida.Unidades;
            if (fija ? d.CantidadFija is not > 0 || d.PorcentajePanadero.HasValue
                     : d.PorcentajePanadero is not > 0 || d.CantidadFija.HasValue)
                throw new InvalidOperationException("Ingresá una cantidad positiva para los insumos por unidad y un porcentaje positivo para los demás ingredientes y sub-recetas.");
        }
        var enlaces = await context.SubRecetaDetalles.AsNoTracking()
            .Where(d => d.IdSubRecetaIngrediente.HasValue && d.IdSubReceta != receta.Id)
            .Select(d => new { d.IdSubReceta, Hija = d.IdSubRecetaIngrediente!.Value }).ToListAsync();
        var grafo = enlaces.GroupBy(d => d.IdSubReceta).ToDictionary(g => g.Key, g => g.Select(d => d.Hija).ToList());
        grafo[receta.Id] = receta.Detalles.Where(d => d.IdSubRecetaIngrediente.HasValue).Select(d => d.IdSubRecetaIngrediente!.Value).ToList();
        var camino = new HashSet<int>();
        var visitados = new HashSet<int>();
        void Visitar(int id)
        {
            if (camino.Contains(id)) throw new InvalidOperationException("Las sub-recetas no pueden incluirse a sí mismas ni formar referencias circulares.");
            if (!visitados.Add(id)) return;
            camino.Add(id);
            foreach (var hija in grafo.GetValueOrDefault(id) ?? []) Visitar(hija);
            camino.Remove(id);
        }
        Visitar(receta.Id);
    }

    public static async Task<Dictionary<int, SubReceta>> CargarAsync(PanaderiaContext context)
    {
        var recetas = await context.SubRecetas
            .Include(s => s.Detalles).ThenInclude(d => d.Insumo)
            .OrderBy(s => s.Nombre).ToDictionaryAsync(s => s.Id);
        foreach (var detalle in recetas.Values.SelectMany(s => s.Detalles))
            if (detalle.IdSubRecetaIngrediente is int id)
                detalle.SubRecetaIngrediente = recetas.GetValueOrDefault(id)
                    ?? throw new InvalidOperationException("No se encontró una sub-receta ingrediente.");
        return recetas;
    }

    public static async Task CompletarAsync(PanaderiaContext context, IEnumerable<Receta> recetas)
    {
        var detalles = recetas.SelectMany(r => r.Detalles).Where(d => d.IdSubReceta.HasValue).ToList();
        if (detalles.Count == 0) return;
        var grafo = await CargarAsync(context);
        foreach (var detalle in detalles)
            detalle.SubReceta = grafo[detalle.IdSubReceta!.Value];
    }
}
