using Panaderia.Models.Enums;

namespace Panaderia.Models.DTOs
{
    public record ResumenProductoItem(string NombreProducto, int CantidadTotal);
    public record ResumenBolsaItem(TipoBolsa Bolsa, int CantidadTotal);
}
