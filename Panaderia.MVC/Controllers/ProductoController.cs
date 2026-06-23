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

        public ProductoController(
            IProductoService productoService,
            ICategoriaService categoriaService,
            IFormatoService formatoService,
            ITamanoService tamanoService,
            IRecetaService recetaService)
        {
            _productoService = productoService;
            _categoriaService = categoriaService;
            _formatoService = formatoService;
            _tamanoService = tamanoService;
            _recetaService = recetaService;
        }

        private async Task CargarDropdowns(Producto? producto = null)
        {
            ViewBag.Categorias = new SelectList(await _categoriaService.GetAllAsync(), "Id", "Nombre", producto?.IdCategoria);
            ViewBag.Formatos = new SelectList(await _formatoService.GetAllAsync(), "Id", "Descripcion", producto?.IdFormato);
            ViewBag.Tamanos = new SelectList(await _tamanoService.GetAllAsync(), "Id", "Descripcion", producto?.IdTamano);
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
        public async Task<ActionResult> Create(Producto producto)
        {
            if (ModelState.IsValid)
            {
                producto.FechaCreacion = DateTime.UtcNow;
                await _productoService.CreateAsync(producto);
                return RedirectToAction(nameof(Index));
            }
            await CargarDropdowns(producto);
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
        public async Task<IActionResult> Edit(int id, Producto producto)
        {
            if (id != producto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var existe = await _productoService.GetByIdAsync(id);
                if (existe == null) return NotFound();

                await _productoService.UpdateAsync(producto);
                return RedirectToAction(nameof(Index));
            }
            await CargarDropdowns(producto);
            var recetaEdit = await _recetaService.GetByProductoIdAsync(producto.Id);
            ViewBag.CostoUnidad = recetaEdit?.CostoPorUnidad ?? 0m;
            return View(producto);
        }

        //POST: Eliminar Producto
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _productoService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}