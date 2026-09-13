namespace Panaderia.Models.Entities;

public class ConfiguracionCaja
{
    public int Id { get; set; } = 1;
    public decimal PorcentajeReserva { get; set; } = 30m;
    public DateTime? InicioSaldosUtc { get; set; }
    public decimal AperturaEfectivo { get; set; }
    public decimal AperturaBanco { get; set; }
    public decimal AperturaReserva { get; set; }
    public DateTime? FechaConfiguracion { get; set; }
    public string? UsuarioConfiguracion { get; set; }
}
