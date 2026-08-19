using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface IProductoService
    {
        //Obtener todos los Productos
        Task<IEnumerable<Producto>> GetAllAsync();

        //Obtener Producto por Id
        Task<Producto?> GetByIdAsync(int id);

        //Crear nuevo Producto
        Task CreateAsync(Producto producto);

        //Actualizar Producto existente
        Task UpdateAsync(Producto producto);

        //Eliminar Producto por Id
        Task DeleteAsync(int id);

        //Verificar si Producto existe por Id
        Task<bool> ExistsAsync(int id);

        //Mostrar/ocultar Producto en la tienda pública
        Task ToggleOcultoEnTiendaAsync(int id);

        //Marcar/desmarcar Producto como sin stock en la tienda
        Task ToggleSinStockAsync(int id);

        //Marcar/desmarcar Producto como "por encargo" en la tienda
        Task TogglePorEncargoAsync(int id);

        //Guardar (crear o reemplazar) la imagen del Producto
        Task GuardarImagenAsync(int idProducto, byte[] datos, string contentType);

        //Obtener la imagen del Producto (o null si no tiene)
        Task<ProductoImagen?> GetImagenAsync(int idProducto);
    }
}