namespace Panaderia.Models.Entities;

public class ConfiguracionTienda
{
    public int Id { get; set; } = 1;
    public bool RetiroHabilitado { get; set; } = true;
    public decimal MontoMinimoPedido { get; set; }
}
