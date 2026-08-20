using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    public class ConfiguracionController : Controller
    {
        private readonly ICategoriaService _categoriaService;
        private readonly IFormatoService _formatoService;
        private readonly ITamanoService _tamanoService;
        private readonly IEtiquetaService _etiquetaService;

        public ConfiguracionController(
            ICategoriaService categoriaService,
            IFormatoService formatoService,
            ITamanoService tamanoService,
            IEtiquetaService etiquetaService)
        {
            _categoriaService = categoriaService;
            _formatoService = formatoService;
            _tamanoService = tamanoService;
            _etiquetaService = etiquetaService;
        }

        // GET: Configuracion
        public async Task<IActionResult> Index()
        {
            var vm = new ConfiguracionViewModel
            {
                Categorias = await _categoriaService.GetAllAsync(),
                Formatos = await _formatoService.GetAllAsync(),
                Tamanos = await _tamanoService.GetAllAsync(),
                Etiquetas = await _etiquetaService.GetAllAsync()
            };
            return View(vm);
        }

        // POST: Agregar Categoria
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearCategoria(string nombre)
        {
            if (!string.IsNullOrWhiteSpace(nombre))
            {
                await _categoriaService.CreateAsync(new CategoriaProducto
                {
                    Nombre = nombre,
                    FechaCreacion = DateTime.UtcNow
                });
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Agregar Formato
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearFormato(string descripcion)
        {
            if (!string.IsNullOrWhiteSpace(descripcion))
            {
                await _formatoService.CreateAsync(new Formato { Descripcion = descripcion });
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Agregar Tamano
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearTamano(string descripcion)
        {
            if (!string.IsNullOrWhiteSpace(descripcion))
            {
                await _tamanoService.CreateAsync(new Tamano { Descripcion = descripcion });
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Agregar Etiqueta de tienda
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearEtiqueta(string nombre, string? icono)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return RedirectToAction(nameof(Index));

            if (await _etiquetaService.ExisteNombreAsync(nombre))
            {
                TempData["EtiquetaError"] = $"Ya existe una etiqueta llamada «{nombre.Trim()}».";
                return RedirectToAction(nameof(Index));
            }

            await _etiquetaService.CreateAsync(new Etiqueta
            {
                Nombre = nombre.Trim(),
                Icono = string.IsNullOrWhiteSpace(icono) ? "grain" : icono,
                FechaCreacion = DateTime.UtcNow
            });

            return RedirectToAction(nameof(Index));
        }

        // POST: Eliminar Categoria
        [HttpPost, ActionName("EliminarCategoria")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarCategoria(int id)
        {
            await _categoriaService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // POST: Eliminar Formato
        [HttpPost, ActionName("EliminarFormato")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarFormato(int id)
        {
            await _formatoService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // POST: Eliminar Tamano
        [HttpPost, ActionName("EliminarTamano")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarTamano(int id)
        {
            await _tamanoService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        // POST: Eliminar Etiqueta (se quita también de los productos que la tenían)
        [HttpPost, ActionName("EliminarEtiqueta")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarEtiqueta(int id)
        {
            await _etiquetaService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}