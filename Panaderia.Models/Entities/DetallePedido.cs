namespace Panaderia.Models.Entities
{
    public class DetallePedido
    {
        public int Id { get; set; }
        public int IdPedido { get; set; }
        public int IdProductoFinal { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public Pedido Pedido { get; set; } = null!;
        public ProductoFinal ProductoFinal { get; set; } = null!;
    }
}
