using System.ComponentModel.DataAnnotations;

namespace Panaderia.MVC.Models;

public class SeleccionPedidosViewModel
{
    [MinLength(1)]
    [MaxLength(1000)]
    public int[] Ids { get; set; } = [];

    [Required]
    [RegularExpression("^(cobrar|entregar|cobrar-entregar)$")]
    public string Accion { get; set; } = "";
}
