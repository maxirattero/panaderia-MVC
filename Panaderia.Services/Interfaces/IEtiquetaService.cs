using Panaderia.Models.Entities;

namespace Panaderia.Services.Interfaces
{
    public interface IEtiquetaService
    {
        //Obtener todas las Etiquetas
        Task<IEnumerable<Etiqueta>> GetAllAsync();

        //Obtener Etiqueta por Id
        Task<Etiqueta?> GetByIdAsync(int id);

        //Crear nueva Etiqueta
        Task CreateAsync(Etiqueta etiqueta);

        //Actualizar Etiqueta existente
        Task UpdateAsync(Etiqueta etiqueta);

        //Eliminar Etiqueta por Id (borra en cascada las asignaciones)
        Task DeleteAsync(int id);

        //Verificar si Etiqueta existe por Id
        Task<bool> ExistsAsync(int id);

        //Verificar si ya existe una Etiqueta con ese nombre (ignora mayúsculas)
        Task<bool> ExisteNombreAsync(string nombre);

        //Ids de las Etiquetas asignadas a un Producto
        Task<List<int>> GetIdsPorProductoAsync(int idProducto);

        //Reemplazar las Etiquetas asignadas a un Producto
        Task AsignarAProductoAsync(int idProducto, IEnumerable<int> idsEtiquetas);
    }
}
