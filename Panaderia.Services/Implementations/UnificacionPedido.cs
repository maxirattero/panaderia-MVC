using Panaderia.Models.Entities;
using Panaderia.Models.Enums;

namespace Panaderia.Services.Implementations;

public static class UnificacionPedido
{
    public static bool PuedeUnificar(Pedido existente, Pedido nuevo) =>
        !existente.Anulado && existente.Estado != EstadoPedido.Entregado &&
        existente.IdCliente == nuevo.IdCliente && nuevo.FechaEntrega.HasValue &&
        nuevo.FechaEntrega.Value.DayOfWeek == DayOfWeek.Saturday &&
        existente.FechaEntrega?.Date == nuevo.FechaEntrega.Value.Date;

    // Solo recibe las líneas nuevas, ya reservadas por el servicio.
    public static void Agregar(Pedido existente, Pedido nuevo)
    {
        if (!PuedeUnificar(existente, nuevo)) throw new InvalidOperationException("Los pedidos no se pueden unificar.");
        var importeAgregado = nuevo.Detalles.Sum(d => d.PrecioUnitario * d.Cantidad)
            * (1m - existente.DescuentoPorcentaje / 100m);
        foreach (var linea in nuevo.Detalles)
        {
            // No alterar precios, empaque o reservas históricas si cambiaron.
            var anterior = existente.Detalles.FirstOrDefault(d => d.IdProducto == linea.IdProducto &&
                d.PrecioUnitario == linea.PrecioUnitario && d.IdEmpaque == linea.IdEmpaque &&
                d.LlevaEtiqueta == linea.LlevaEtiqueta && d.CostoEmpaque == linea.CostoEmpaque &&
                d.ReservaStock == linea.ReservaStock);
            if (anterior == null) existente.Detalles.Add(linea);
            else anterior.Cantidad = checked(anterior.Cantidad + linea.Cantidad);
        }
        existente.MontoTotal += importeAgregado;
        existente.Notas = string.Join("\n\n", new[] { existente.Notas,
            $"[Ampliación {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC]\n{nuevo.Notas}" }.Where(s => !string.IsNullOrWhiteSpace(s)));
        existente.FechaModificacion = DateTime.UtcNow;
        existente.Estado = EstadoPedido.Pendiente;
    }
}
