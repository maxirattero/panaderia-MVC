namespace Panaderia.Models.DTOs;

public record TotalesPedidosSemana(
    DateOnly InicioSemana,
    int CantidadPedidos,
    int Moldes,
    int Campos,
    int Bolsas,
    int BolsasPapel)
{
    public DateOnly FinSemana => InicioSemana.AddDays(6);
}
