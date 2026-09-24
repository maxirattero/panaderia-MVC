using Panaderia.Models.Entities;

namespace Panaderia.Services.Implementations;

public static class DisponibilidadSemanal
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
    public const string MensajeCierre = "Pedidos cerrados momentáneamente. Volvemos a tomar pedidos el sábado a las 12:00 (hora de Argentina).";
    public const string MensajeCarrito = "Hay productos con pedidos semanales cerrados. Retiralos del carrito para continuar con los demás productos. Volvemos a tomar pedidos el sábado a las 12:00 (hora de Argentina).";

    public static bool EstaCerrada(DateTimeOffset ahora)
    {
        var local = TimeZoneInfo.ConvertTime(ahora, Zona);
        return local.DayOfWeek == DayOfWeek.Friday
            || (local.DayOfWeek == DayOfWeek.Saturday && local.TimeOfDay < TimeSpan.FromHours(12));
    }

    public static bool EstaBloqueado(Producto producto, DateTimeOffset ahora) =>
        producto.TieneDisponibilidadSemanal && EstaCerrada(ahora);

    public static void ValidarPedido(IEnumerable<Producto> productos, DateTimeOffset ahora)
    {
        if (productos.Any(p => EstaBloqueado(p, ahora)))
            throw new InvalidOperationException(MensajeCarrito);
    }
}
