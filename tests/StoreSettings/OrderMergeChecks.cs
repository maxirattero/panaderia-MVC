using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.Services.Implementations;

static class OrderMergeChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool result, string message)
        {
            if (!result) throw new Exception(message);
            Console.WriteLine("PASS: " + message);
            count++;
        }
        var saturday = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc);
        Pedido New(int customer = 1) => new() { IdCliente = customer, FechaEntrega = saturday, Notas = "puerta azul" };
        DetallePedido Line(int product, int quantity, decimal price = 100, bool stock = true) =>
            new() { IdProducto = product, Cantidad = quantity, PrecioUnitario = price, ReservaStock = stock };
        var old = New();
        var incoming = New();
        Check(UnificacionPedido.PuedeUnificar(old, incoming), "Same customer and Saturday can merge");
        Check(!UnificacionPedido.PuedeUnificar(old, New(2)), "Different customer never merges");
        incoming.FechaEntrega = saturday.AddDays(7);
        Check(!UnificacionPedido.PuedeUnificar(old, incoming), "Different Saturday never merges");
        incoming.FechaEntrega = old.FechaEntrega = saturday.AddDays(1);
        Check(!UnificacionPedido.PuedeUnificar(old, incoming), "Non-Saturday orders do not merge");
        incoming.FechaEntrega = old.FechaEntrega = saturday;
        old.Estado = EstadoPedido.Entregado;
        Check(!UnificacionPedido.PuedeUnificar(old, incoming), "Delivered order is immutable");
        old.Estado = EstadoPedido.Pendiente;
        old.Anulado = true;
        Check(!UnificacionPedido.PuedeUnificar(old, incoming), "Cancelled order is excluded");
        old.Anulado = false;
        old.Estado = EstadoPedido.EnProduccion;
        old.DescuentoPorcentaje = 10;
        old.MontoTotal = 180;
        old.MontoCobrado = 100;
        old.Detalles.Add(Line(1, 2));
        old.Detalles.First().CantidadProducida = 2;
        incoming.Detalles.Add(Line(1, 3));
        incoming.Detalles.Add(Line(2, 1, 200));
        UnificacionPedido.Agregar(old, incoming);
        Check(old.Detalles.Count == 2 && old.Detalles.First().Cantidad == 5, "Repeated product sums quantities; new product added");
        Check(old.MontoTotal == 630 && old.MontoCobrado == 100 && old.SaldoPendiente == 530, "Existing discount and partial payment preserved, balance increased");
        Check(old.Detalles.First().CantidadProducida == 2 && old.Estado == EstadoPedido.Pendiente, "Only the new quantities return to production planning");
        Check(old.Detalles.Sum(d => d.Cantidad - d.CantidadProducida) == 4, "Previous production is not counted again");
        Check(old.Detalles.Where(d => d.ReservaStock).Sum(d => d.Cantidad) == 6, "Cancellation can restore all reserved units exactly once");
        Check(old.Reportes.Count == 0, "Merging does not create a cash movement");
        Check(old.Notas!.Contains("Ampliación") && old.FechaModificacion != null, "Notes retained with dated addition");
        var differentPrice = New();
        differentPrice.Detalles.Add(Line(1, 1, 150));
        UnificacionPedido.Agregar(old, differentPrice);
        Check(old.Detalles.Count == 3 && old.Detalles.First().PrecioUnitario == 100, "Price change does not rewrite historical unit price");
        var differentPackage = New();
        var packaged = Line(1, 1); packaged.LlevaEtiqueta = true;
        differentPackage.Detalles.Add(packaged);
        UnificacionPedido.Agregar(old, differentPackage);
        Check(old.Detalles.Count == 4, "Different packaging or label remains separate");
        var differentReservation = New();
        differentReservation.Detalles.Add(Line(1, 1, 100, false));
        UnificacionPedido.Agregar(old, differentReservation);
        Check(old.Detalles.Count == 5, "Stock policy change does not corrupt reservation flags");
        var duplicate = New();
        duplicate.Detalles.Add(Line(8, 2)); duplicate.Detalles.Add(Line(8, 3));
        var empty = New();
        UnificacionPedido.Agregar(empty, duplicate);
        Check(empty.MontoTotal == 500 && empty.Detalles.Single().Cantidad == 5, "Duplicate incoming lines count once in total");
        foreach (var stock in new[] { -1, 0, 1, 2, 5, 6 })
        {
            var product = new Producto { Stock = stock };
            Check((product.AvisoUltimasUnidades != null) == (stock >= 1 && stock <= 5), $"Low stock threshold: {stock}");
            product.PorEncargo = true;
            Check(product.AvisoUltimasUnidades == null, $"Made-to-order never shows scarcity: {stock}");
        }
        Check(new Producto { Stock = 1 }.AvisoUltimasUnidades == "Última unidad", "Singular stock wording");
        Check(new Producto { Stock = 5 }.AvisoUltimasUnidades == "Últimas 5 unidades", "Plural stock wording");
        Console.WriteLine($"{count} order merge and stock notice checks passed.");
    }
}
