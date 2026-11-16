using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;

namespace Panaderia.Services.Implementations;

public static class PresentacionProducto
{
    public static List<MaterialPedidoSnapshot> Calcular(Producto producto, IReadOnlyDictionary<int, Insumo> insumos,
        int? bolsa, bool etiqueta)
    {
        var resultado = new List<MaterialPedidoSnapshot>();
        void Agregar(int id, decimal cantidad, UsoMaterial uso, decimal incluidaEnBolsa = 0)
        {
            if (cantidad <= 0) return;
            if (!insumos.TryGetValue(id, out var insumo) || !insumo.Activo || insumo.CantidadRendimiento <= 0)
                throw new InvalidOperationException("Hay materiales de presentación sin costo válido o desactivados. Revisá los insumos del producto.");
            resultado.Add(new(id, insumo.Nombre, cantidad, decimal.Round(insumo.CostoPorUnidadBase, 4, MidpointRounding.AwayFromZero), uso, incluidaEnBolsa));
        }
        foreach (var material in producto.Materiales.OrderBy(x => x.IdInsumo))
        {
            if (material.Cantidad <= 0 || material.Cantidad > 999999.9999m)
                throw new InvalidOperationException("Revisá las cantidades de los materiales del producto.");
            if (!insumos.TryGetValue(material.IdInsumo, out var insumo))
                throw new InvalidOperationException("Uno de los materiales del producto ya no está disponible.");
            if (insumo.TipoInsumo == TipoInsumo.Etiqueta) continue;
            // Si se elige como bolsa principal un adicional, esa unidad ya está incluida.
            Agregar(material.IdInsumo, material.Cantidad, UsoMaterial.Adicional, bolsa == material.IdInsumo ? Math.Min(1, material.Cantidad) : 0);
        }
        if (bolsa.HasValue)
        {
            if (!insumos.TryGetValue(bolsa.Value, out var insumo) || insumo.TipoInsumo != TipoInsumo.Empaque)
                throw new InvalidOperationException("Elegí un insumo de tipo empaque para la bolsa del pedido.");
            Agregar(bolsa.Value, 1, UsoMaterial.Bolsa);
        }
        if (etiqueta)
        {
            var elegida = producto.Materiales.FirstOrDefault(m => insumos.TryGetValue(m.IdInsumo, out var i) && i.TipoInsumo == TipoInsumo.Etiqueta);
            var id = elegida?.IdInsumo ?? insumos.Values.Where(i => i.Activo && i.TipoInsumo == TipoInsumo.Etiqueta).OrderBy(i => i.Id).Select(i => (int?)i.Id).FirstOrDefault();
            if (id.HasValue) Agregar(id.Value, elegida?.Cantidad ?? 1, UsoMaterial.Etiqueta);
        }
        return resultado;
    }
}
