using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    public class ProductoController : Controller
    {
        private readonly IProductoService _productoService;
        private readonly ICategoriaService _categoriaService;
        private readonly IFormatoService _formatoService;
        private readonly ITamanoService _tamanoService;
        private readonly IRecetaService _recetaService;
        private readonly IEtiquetaService _etiquetaService;

        public ProductoController(
            IProductoService productoService,
            ICategoriaService categoriaService,
            IFormatoService formatoService,
            ITamanoService tamanoService,
            IRecetaService recetaService,
            IEtiquetaService etiquetaService)
        {
            _productoService = productoService;
            _categoriaService = categoriaService;
            _formatoService = formatoService;
            _tamanoService = tamanoService;
            _recetaService = recetaService;
            _etiquetaService = etiquetaService;
        }

        private async Task CargarDropdowns(Producto? producto = null, IEnumerable<int>? etiquetasSeleccionadas = null)
        {
            ViewBag.Categorias = new SelectList(await _categoriaService.GetAllAsync(), "Id", "Nombre", producto?.IdCategoria);
            ViewBag.Formatos = new SelectList(await _formatoService.GetAllAsync(), "Id", "Descripcion", producto?.IdFormato);
            ViewBag.Tamanos = new SelectList(await _tamanoService.GetAllAsync(), "Id", "Descripcion", producto?.IdTamano);

            ViewBag.Etiquetas = await _etiquetaService.GetAllAsync();
            ViewBag.EtiquetasSeleccionadas = etiquetasSeleccionadas?.ToList()
                ?? (producto is { Id: > 0 }
                        ? await _etiquetaService.GetIdsPorProductoAsync(producto.Id)
                        : new List<int>());
        }

        // GET: Productos
        public async Task<IActionResult> Index()
        {
            return View(await _productoService.GetAllAsync());
        }

        // GET: Detalles de Productos
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _productoService.GetByIdAsync(id.Value);
            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }

        //GET: Crear Producto
        public async Task<IActionResult> Create()
        {
            await CargarDropdowns();
            ViewBag.CostoUnidad = 0m;
            return View();
        }

        //POST : Crear Producto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(Producto producto, int[]? idsEtiquetas, IFormFile? archivoImagen)
        {
            ModelState.Remove(nameof(Producto.Nombre));
            if (ModelState.IsValid)
            {
                producto.FechaCreacion = DateTime.UtcNow;
                await _productoService.CreateAsync(producto);
                await _etiquetaService.AsignarAProductoAsync(producto.Id, idsEtiquetas ?? Array.Empty<int>());

                var errorImagen = await GuardarImagenSubidaAsync(producto.Id, archivoImagen);
                if (errorImagen != null) TempData["ImagenError"] = errorImagen;

                return RedirectToAction(nameof(Index));
            }
            await CargarDropdowns(producto, idsEtiquetas);
            ViewBag.CostoUnidad = 0m;
            return View(producto);
        }

        //GET: Editar Producto
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _productoService.GetByIdAsync(id.Value);
            if (producto == null)
            {
                return NotFound();
            }
            await CargarDropdowns(producto);
            var receta = await _recetaService.GetByProductoIdAsync(producto.Id);
            ViewBag.CostoUnidad = receta?.CostoPorUnidad ?? 0m;
            return View(producto);
        }

        //POST: Editar Producto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Producto producto, int[]? idsEtiquetas, IFormFile? archivoImagen)
        {
            if (id != producto.Id) return NotFound();

            ModelState.Remove(nameof(Producto.Nombre));
            if (ModelState.IsValid)
            {
                var existe = await _productoService.GetByIdAsync(id);
                if (existe == null) return NotFound();

                await _productoService.UpdateAsync(producto);
                await _etiquetaService.AsignarAProductoAsync(id, idsEtiquetas ?? Array.Empty<int>());

                // Si subió un archivo, pisa la ImagenURL del form (GuardarImagenAsync la reescribe)
                var errorImagen = await GuardarImagenSubidaAsync(id, archivoImagen);
                if (errorImagen != null) TempData["ImagenError"] = errorImagen;

                return RedirectToAction(nameof(Index));
            }
            await CargarDropdowns(producto, idsEtiquetas);
            var recetaEdit = await _recetaService.GetByProductoIdAsync(producto.Id);
            ViewBag.CostoUnidad = recetaEdit?.CostoPorUnidad ?? 0m;
            return View(producto);
        }

        //POST: Eliminar Producto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicar(int id)
        {
            var copia = await _productoService.DuplicarAsync(id);
            if (copia == null) return NotFound();

            TempData["Success"] = "Producto duplicado. Revisá sus datos antes de publicarlo en la tienda.";
            return RedirectToAction(nameof(Edit), new { id = copia.Id });
        }

        //POST: Eliminar Producto
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _productoService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        //POST: Mostrar/Ocultar producto en la tienda pública
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleOculto(int id)
        {
            await _productoService.ToggleOcultoEnTiendaAsync(id);
            return RedirectToAction(nameof(Index));
        }

        //POST: Marcar/desmarcar producto como sin stock en la tienda
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSinStock(int id)
        {
            await _productoService.ToggleSinStockAsync(id);
            return RedirectToAction(nameof(Index));
        }

        //POST: Marcar/desmarcar producto como "por encargo" en la tienda
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePorEncargo(int id)
        {
            await _productoService.TogglePorEncargoAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // Valida y guarda una imagen subida. Devuelve el mensaje de error, o null si salió bien
        // (o si no vino archivo: es válido, significa que no se quiso cambiar la imagen).
        private async Task<string?> GuardarImagenSubidaAsync(int idProducto, IFormFile? imagen)
        {
            if (imagen == null || imagen.Length == 0) return null;

            var formatosPermitidos = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!formatosPermitidos.Contains(imagen.ContentType))
                return "Formato no soportado. Usá JPG, PNG o WebP.";

            const int maxBytes = 2 * 1024 * 1024; // 2 MB
            if (imagen.Length > maxBytes)
                return "La imagen supera los 2 MB. Achicala o comprimila e intentá de nuevo.";

            using var ms = new MemoryStream();
            await imagen.CopyToAsync(ms);
            await _productoService.GuardarImagenAsync(idProducto, ms.ToArray(), imagen.ContentType);

            return null;
        }

        //POST: Subir imagen de producto desde el listado (se guarda en la DB, tabla ProductoImagenes)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubirImagen(int id, IFormFile? imagen)
        {
            if (imagen == null || imagen.Length == 0)
            {
                TempData["ImagenError"] = "No seleccionaste ninguna imagen.";
                return RedirectToAction(nameof(Index));
            }

            var error = await GuardarImagenSubidaAsync(id, imagen);
            if (error != null) TempData["ImagenError"] = error;

            return RedirectToAction(nameof(Index));
        }

    }
}
