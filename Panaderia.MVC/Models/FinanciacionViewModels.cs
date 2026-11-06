using System.ComponentModel.DataAnnotations;
using Panaderia.Models.Enums;

namespace Panaderia.MVC.Models;

public class LoteFinanciadoViewModel
{
    [Required, StringLength(120)] public string Nombre { get; set; } = "";
    [Range(1, int.MaxValue)] public int IdProducto { get; set; }
    [Range(1, int.MaxValue)] public int Unidades { get; set; }
    [Required] public DateTime? Desde { get; set; }
    public Guid Clave { get; set; }
}

public class PagoReintegroViewModel
{
    [Range(1, int.MaxValue)] public int Aporte { get; set; }
    [Range(1, int.MaxValue)] public int? Reserva { get; set; }
    [Range(1, 2)] public CuentaCaja Cuenta { get; set; }
    [Range(typeof(decimal), "0.01", "99999999999999")] public decimal Monto { get; set; }
    public Guid Clave { get; set; }
}
