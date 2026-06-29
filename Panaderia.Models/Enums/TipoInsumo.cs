using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Enums
{
    public enum TipoInsumo
    {
        [Display(Name = "Ingrediente")]
        Ingrediente = 0,

        [Display(Name = "Empaque")]
        Empaque = 1,

        [Display(Name = "Etiqueta")]
        Etiqueta = 2
    }
}
