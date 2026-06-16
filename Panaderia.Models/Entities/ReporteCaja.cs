using Panaderia.Models.Enums;

namespace Panaderia.Models.Entities
{
    public class ReporteCaja
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public TipoMovimiento Tipo { get; set; } = TipoMovimiento.Ingreso;
        public CategoriaMovimiento Categoria { get; set; } = CategoriaMovimiento.Otro;
        public decimal Monto { get; set; }
        public string? Descripcion { get; set; }
        public int? IdPedido { get; set; }
        public Pedido? Pedido { get; set; }
        public int? IdProveedor { get; set; }
        public Proveedor? Proveedor { get; set; }
    }
}