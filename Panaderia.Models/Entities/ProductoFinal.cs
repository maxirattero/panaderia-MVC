namespace Panaderia.Models.Entities
{
    public class ProductoFinal
    {
        public int Id { get; set; }
        public int IdProductoBase { get; set; }
        public int IdTamano { get; set; }
        public int IdFormato { get; set; }
        public decimal PrecioFinal { get; set; }
        public decimal PrecioReventa { get; set; }
        public int Stock { get; set; }
        public string? ImagenURL { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public ProductoBase ProductoBase { get; set; } = null!;
        public Tamano Tamano { get; set; } = null!;
        public Formato Formato { get; set; } = null!;
    }
}
