using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Panaderia.Models.Enums;

namespace Panaderia.Models.Entities
{
    public class DetallePedido
    {
        public int Id { get; set; }
        public int IdPedido { get; set; }
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public TipoBolsa Bolsa { get; set; } = TipoBolsa.Sellado;
        public int? IdEmpaque { get; set; }
        public bool LlevaEtiqueta { get; set; } = false;
        public decimal CostoEmpaque { get; set; } = 0m;

        public Pedido Pedido { get; set; } = null!;
        public Producto Producto { get; set; } = null!;

        [ValidateNever]
        public Insumo? Empaque { get; set; }
    }
}
