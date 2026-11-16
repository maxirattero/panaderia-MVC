using System.Text.Json;
using System.Text.Json.Serialization;

namespace Panaderia.Models.DTOs;

public enum UsoMaterial { Adicional, Bolsa, Etiqueta }

// Cantidades y costos por una unidad de producto, conservados al cargar el pedido.
public record MaterialPedidoSnapshot(int IdInsumo, string Nombre, decimal Cantidad, decimal CostoUnitario, UsoMaterial Uso, decimal CantidadIncluidaEnBolsa = 0)
{
    [JsonIgnore] public decimal CantidadAplicada => Math.Max(0, Cantidad - CantidadIncluidaEnBolsa);
    [JsonIgnore] public decimal Costo => decimal.Round(CantidadAplicada * CostoUnitario, 4, MidpointRounding.AwayFromZero);
}

public static class MaterialesPedido
{
    public static List<MaterialPedidoSnapshot> Leer(string? json) =>
        json == null ? [] : JsonSerializer.Deserialize<List<MaterialPedidoSnapshot>>(json) ?? [];
    public static string Guardar(IEnumerable<MaterialPedidoSnapshot> materiales) =>
        JsonSerializer.Serialize(materiales.OrderBy(x => x.Uso).ThenBy(x => x.IdInsumo));
    public static bool Iguales(string? a, string? b) => a == null || b == null ? a == b :
        Leer(a).OrderBy(x => x.Uso).ThenBy(x => x.IdInsumo).SequenceEqual(Leer(b).OrderBy(x => x.Uso).ThenBy(x => x.IdInsumo));
}
