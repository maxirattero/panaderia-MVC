namespace Panaderia.MVC.Models
{
    public class RegistrarCobroViewModel
    {
        public int IdPedido { get; set; }
        public decimal Monto { get; set; }
        [System.ComponentModel.DataAnnotations.Range(1, 2)]
        public Panaderia.Models.Enums.CuentaCaja Cuenta { get; set; }
        public DateTime? Fecha { get; set; }
        public Guid Clave { get; set; }
    }
}
