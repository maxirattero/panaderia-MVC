using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Panaderia.Models.Entities;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    public class ConfiguracionController : Controller
    {
        private const string CatalogoMaterialSymbolsUrl = "https://fonts.google.com/metadata/icons?incomplete=true";
        private const string CacheKeyIconosMaterialSymbols = "configuracion-iconos-material-symbols";

        private readonly ICategoriaService _categoriaService;
        private readonly IFormatoService _formatoService;
        private readonly ITamanoService _tamanoService;
        private readonly IEtiquetaService _etiquetaService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;

        public ConfiguracionController(
            ICategoriaService categoriaService,
            IFormatoService formatoService,
            ITamanoService tamanoService,
            IEtiquetaService etiquetaService,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache)
        {
            _categoriaService = categoriaService;
            _formatoService = formatoService;
            _tamanoService = tamanoService;
            _etiquetaService = etiquetaService;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
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

        // Catálogo oficial de Google. Se carga solo al abrir el selector y queda
        // en caché para no agregar peso ni demora a la pantalla de Configuración.
        [HttpGet]
        public async Task<IActionResult> IconosMaterialSymbols()
        {
            if (_cache.TryGetValue(CacheKeyIconosMaterialSymbols, out IReadOnlyList<IconoMaterialDto>? iconos))
                return Json(iconos);

            try
            {
                var cliente = _httpClientFactory.CreateClient();
                cliente.Timeout = TimeSpan.FromSeconds(15);

                var respuesta = await cliente.GetStringAsync(CatalogoMaterialSymbolsUrl, HttpContext.RequestAborted);
                var inicioJson = respuesta.IndexOf('{'); // Google antepone un prefijo anti-XSSI.
                if (inicioJson < 0)
                    throw new JsonException("El catálogo de íconos no contiene JSON válido.");

                using var documento = JsonDocument.Parse(respuesta[inicioJson..]);
                iconos = documento.RootElement
                    .GetProperty("icons")
                    .EnumerateArray()
                    .Where(EsMaterialSymbol)
                    .Select(icono => new IconoMaterialDto(
                        icono.GetProperty("name").GetString() ?? string.Empty,
                        ObtenerTexto(icono, "categories"),
                        ObtenerTexto(icono, "tags"),
                        icono.TryGetProperty("popularity", out var popularidad) ? popularidad.GetInt32() : 0))
                    .Where(icono => icono.Nombre.Length is > 0 and <= 40)
                    .GroupBy(icono => icono.Nombre, StringComparer.Ordinal)
                    .Select(grupo => grupo.OrderByDescending(icono => icono.Popularidad).First())
                    .OrderByDescending(icono => icono.Popularidad)
                    .ToList();

                _cache.Set(CacheKeyIconosMaterialSymbols, iconos, TimeSpan.FromDays(7));
                return Json(iconos);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    mensaje = "No se pudo cargar el catálogo de íconos. Probá nuevamente en unos minutos."
                });
            }
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

            if (!string.IsNullOrWhiteSpace(icono)
                && (icono.Length > 40 || !icono.All(caracter => char.IsAsciiLetterOrDigit(caracter) || caracter == '_')))
            {
                TempData["EtiquetaError"] = "El ícono seleccionado no es válido.";
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

        private static bool EsMaterialSymbol(JsonElement icono)
        {
            if (!icono.TryGetProperty("unsupported_families", out var familiasNoCompatibles))
                return true;

            return !familiasNoCompatibles
                .EnumerateArray()
                .Any(familia => familia.GetString() == "Material Symbols");
        }

        private static string ObtenerTexto(JsonElement icono, string propiedad) =>
            icono.TryGetProperty(propiedad, out var valores)
                ? string.Join(' ', valores.EnumerateArray().Select(valor => valor.GetString()))
                : string.Empty;

        private sealed record IconoMaterialDto(string Nombre, string Categoria, string Busqueda, int Popularidad);
    }
}
