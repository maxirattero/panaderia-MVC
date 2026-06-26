using Panaderia.Models.Enums;

namespace Panaderia.Models.DTOs
{
    public record ResumenProductoItem(int IdProducto, string NombreProducto, int CantidadTotal);
    public record ResumenBolsaItem(TipoBolsa Bolsa, int CantidadTotal);

    public class ItemProduccionSeleccionable
    {
        public int IdProducto { get; set; }
        public int IdReceta { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public decimal CantidadSugerida { get; set; }
        public decimal CantidadAProducir { get; set; }
        public bool Seleccionado { get; set; } = true;
    }

    public class ResumenSubRecetaItem
    {
        public int IdSubReceta { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal TotalGramos { get; set; }
        public List<ResumenSubRecetaIngrediente> Ingredientes { get; set; } = new();
    }

    public class ResumenSubRecetaIngrediente
    {
        public string NombreInsumo { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public string Unidad { get; set; } = string.Empty;
    }

    public class ProduccionProductoDetalle
    {
        public int IdProducto { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public int CantidadUnidades { get; set; }
        public decimal PesoMasaTotal { get; set; }
        public List<ProduccionIngredienteDetalle> Ingredientes { get; set; } = new();
    }

    public class ProduccionIngredienteDetalle
    {
        public int? IdInsumo { get; set; }
        public int? IdSubReceta { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal Gramos { get; set; }
        public string Unidad { get; set; } = string.Empty;
        public bool EsSubReceta { get; set; }
    }
}
