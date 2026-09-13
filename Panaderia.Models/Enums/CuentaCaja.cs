using System.ComponentModel.DataAnnotations;

namespace Panaderia.Models.Enums;

public enum CuentaCaja
{
    [Display(Name = "Histórico sin clasificar")] SinClasificar = 0,
    [Display(Name = "Efectivo")] Efectivo = 1,
    [Display(Name = "Banco / transferencia")] Banco = 2,
    [Display(Name = "Mercado Pago · reserva de insumos")] ReservaMercadoPago = 3
}

public enum DestinoCierre { Reserva = 0, Ani = 1, Maxi = 2 }
