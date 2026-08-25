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
        Etiqueta = 2,

        // Todo lo que se compra y se consume pero no entra en una receta ni empaqueta
        // el producto: film, papel de horno, guantes, artículos de limpieza, etc.
        // Queda fuera del selector de insumos de recetas y del costo por unidad.
        [Display(Name = "Consumible")]
        Consumible = 3
    }
}
