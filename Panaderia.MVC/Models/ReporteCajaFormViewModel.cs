using Microsoft.AspNetCore.Mvc.Rendering;
using Panaderia.Models.Enums;

namespace Panaderia.MVC.Models
{
    public class ReporteCajaFormViewModel
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public TipoMovimiento Tipo { get; set; }
        public CategoriaMovimiento Categoria { get; set; }
        public decimal Monto { get; set; }
        public string? Descripcion { get; set; }
        public int? IdProveedor { get; set; }
        public int? IdPedido { get; set; }
        public SelectList? Proveedores { get; set; }
    }
}
