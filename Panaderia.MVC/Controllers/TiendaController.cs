using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    [AllowAnonymous]
    public class TiendaController : Controller
    {
        private readonly IProductoService _productoService;
        private readonly IConfiguration _configuration;

        public TiendaController(IProductoService productoService, IConfiguration configuration)
        {
            _productoService = productoService;
            _configuration = configuration;
        }

        // GET: / (tienda pública)
        public async Task<IActionResult> Index(string? categoria, string? q)
        {
            var comparer = StringComparer.Create(new CultureInfo("es-AR"), ignoreCase: true);

            var productos = (await _productoService.GetAllAsync())
                .Where(p => !p.OcultoEnTienda)
                .ToList();

            // Categorías disponibles (solo las que tienen productos)
            var categorias = productos
                .Where(p => p.Categoria != null)
                .Select(p => p.Categoria!.Nombre)
                .Distinct(comparer)
                .OrderBy(n => n, comparer)
                .ToList();

            if (!string.IsNullOrWhiteSpace(categoria))
            {
                productos = productos
                    .Where(p => p.Categoria != null &&
                                string.Equals(p.Categoria.Nombre, categoria, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                productos = productos
                    .Where(p => p.NombreVisible.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var vm = new TiendaIndexViewModel
            {
                Productos = productos,
                Categorias = categorias,
                CategoriaSeleccionada = categoria,
                Busqueda = q
            };

            return View(vm);
        }

        // GET: /Tienda/Detalle/5
        public async Task<IActionResult> Detalle(int id)
        {
            var producto = await _productoService.GetByIdAsync(id);
            if (producto == null || producto.OcultoEnTienda) return NotFound();

            // Número de WhatsApp para pedidos (opcional): appsettings.json → "Tienda": { "WhatsApp": "549..." }
            ViewBag.WhatsApp = _configuration["Tienda:WhatsApp"];

            return View(producto);
        }

        // GET: /Tienda/Imagen/5 — sirve la imagen del producto guardada en la DB
        public async Task<IActionResult> Imagen(int id)
        {
            var imagen = await _productoService.GetImagenAsync(id);
            if (imagen == null) return NotFound();

            Response.Headers.CacheControl = "public, max-age=86400";
            return File(imagen.Datos, imagen.ContentType);
        }
    }
}
