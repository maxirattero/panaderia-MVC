using System.ComponentModel.DataAnnotations;

namespace Panaderia.MVC.Models;

public class ConfiguracionTiendaViewModel : IValidatableObject
{
    public bool RetiroHabilitado { get; set; } = true;

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Ingresá un monto entre 0 y 999.999.999.")]
    public decimal MontoMinimoPedido { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (decimal.Round(MontoMinimoPedido, 2) != MontoMinimoPedido)
            yield return new ValidationResult("El monto puede tener hasta dos decimales.", [nameof(MontoMinimoPedido)]);
    }
}
