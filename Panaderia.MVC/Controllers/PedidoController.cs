using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;
using Panaderia.Models.DTOs;
using Panaderia.Models.Entities;
using Panaderia.Models.Enums;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    public class PedidoController : Controller
    {
        private readonly IPedidoService _pedidoService;
        private readonly IClienteService _clienteService;
        private readonly IProductoService _productoService;
        private readonly IRecetaService _recetaService;
        private readonly IInsumoService _insumoService;

        public PedidoController(
            IPedidoService pedidoService,
            IClienteService clienteService,
            IProductoService productoService,
            IRecetaService recetaService,
            IInsumoService insumoService)
        {
            _pedidoService = pedidoService;
            _clienteService = clienteService;
            _productoService = productoService;
            _recetaService = recetaService;
            _insumoService = insumoService;
        }

        // Cookie de sesión con los productos destildados en el dashboard de Producción.
        // Se comparte con el planificador y la impresión para que todo muestre lo mismo.
        private const string CookieExcluidos = "mv_prod_excluidos";

        private List<int> LeerProductosExcluidos()
        {
            var cookie = Request.Cookies[CookieExcluidos];
            if (string.IsNullOrWhiteSpace(cookie)) return new List<int>();

            return cookie.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private void GuardarProductosExcluidos(IEnumerable<int> ids)
        {
            var lista = ids.Distinct().ToList();
            if (!lista.Any())
            {
                Response.Cookies.Delete(CookieExcluidos);
                return;
            }

            // Cookie de sesión (sin Expires): se limpia al cerrar el navegador
            Response.Cookies.Append(CookieExcluidos, string.Join(',', lista), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
        }

        public async Task<IActionResult> Produccion()
        {
            var excluidos = LeerProductosExcluidos();

            // Sin filtrar: alimenta la tabla de selección (los excluidos siguen visibles, destildados)
            var resumenTodos = await _pedidoService.GetResumenProduccionAsync();

            // Filtrado: alimenta los totales, las sub-recetas y el agua
            var resumen = resumenTodos;
            if (excluidos.Any())
                resumen = await _pedidoService.GetResumenProduccionAsync(excluidos);

            var vm = new ProduccionViewModel
            {
                PorProducto = resumen.PorProducto,
                PorBolsa = resumen.PorBolsa,
                PorSubReceta = resumen.PorSubReceta,
                TotalAgua = resumen.TotalAgua
            };

            // Filas de pedidos pendientes
            foreach (var item in resumenTodos.PorProducto)
            {
                var receta = await _recetaService.GetByProductoIdAsync(item.IdProducto);
                if (receta != null)
                {
                    vm.ItemsSeleccionables.Add(new ItemProduccionSeleccionable
                    {
                        IdProducto = item.IdProducto,
                        IdReceta = receta.Id,
                        NombreProducto = item.NombreProducto,
                        CantidadSugerida = item.CantidadTotal,
                        CantidadAProducir = item.CantidadTotal,
                        Seleccionado = !excluidos.Contains(item.IdProducto),
                        EsStock = false,
                        IdProduccionStock = 0
                    });
                }
            }

            // Filas de producción para stock (buffer persistido)
            var buffer = await _pedidoService.GetProduccionStockAsync();
            foreach (var b in buffer)
            {
                var receta = await _recetaService.GetByProductoIdAsync(b.IdProducto);
                if (receta != null)
                {
                    vm.ItemsSeleccionables.Add(new ItemProduccionSeleccionable
                    {
                        IdProducto = b.IdProducto,
                        IdReceta = receta.Id,
                        NombreProducto = b.Producto.NombreVisible,
                        CantidadSugerida = b.Cantidad,
                        CantidadAProducir = b.Cantidad,
                        Seleccionado = !excluidos.Contains(b.IdProducto),
                        EsStock = true,
                        IdProduccionStock = b.Id
                    });
                }
            }

            ViewBag.HayExcluidos = excluidos.Any();
            ViewBag.CantidadExcluidos = excluidos.Count;

            // Dropdown de productos con receta para "producir para stock"
            var recetas = await _recetaService.GetAllAsync();
            var recetaPorProducto = recetas
                .GroupBy(r => r.IdProducto)
                .ToDictionary(g => g.Key, g => g.First().Id);
            var productos = await _productoService.GetAllAsync();
            vm.ProductosDisponibles = productos
                .Where(p => recetaPorProducto.ContainsKey(p.Id))
                .Select(p => new ProductoRecetaOption
                {
                    IdProducto = p.Id,
                    IdReceta = recetaPorProducto[p.Id],
                    NombreProducto = p.NombreVisible
                })
                .OrderBy(o => o.NombreProducto)
                .ToList();

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarProduccionStock(int idProducto, int cantidad)
        {
            if (idProducto <= 0 || cantidad < 1)
            {
                TempData["Error"] = "Elegí un producto y una cantidad válida.";
                return RedirectToAction(nameof(Produccion));
            }
            await _pedidoService.AgregarProduccionStockAsync(idProducto, cantidad);
            return RedirectToAction(nameof(Produccion));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarProduccionStock(int id)
        {
            await _pedidoService.QuitarProduccionStockAsync(id);
            return RedirectToAction(nameof(Produccion));
        }

        // POST: aplica los tildes a los cálculos (totales, sub-recetas, agua, planificador e impresión)
        // sin confirmar la producción ni descontar stock.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AplicarSeleccionProduccion(ProduccionViewModel vm)
        {
            // Un producto queda excluido solo si TODAS sus filas están destildadas
            // (puede aparecer dos veces: por pedidos y por stock).
            var excluidos = vm.ItemsSeleccionables
                .GroupBy(i => i.IdProducto)
                .Where(g => g.All(i => !i.Seleccionado))
                .Select(g => g.Key)
                .ToList();

            GuardarProductosExcluidos(excluidos);
            return RedirectToAction(nameof(Produccion));
        }

        // POST: vuelve a incluir todos los productos en los cálculos
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MostrarTodosProduccion()
        {
            GuardarProductosExcluidos(Array.Empty<int>());
            return RedirectToAction(nameof(Produccion));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarProduccion(ProduccionViewModel vm)
        {
            var itemsSeleccionados = vm.ItemsSeleccionables.Where(i => i.Seleccionado).ToList();
            if (!itemsSeleccionados.Any())
            {
                TempData["Error"] = "No seleccionaste ningún producto para producir.";
                return RedirectToAction(nameof(Produccion));
            }
            var warnings = await _pedidoService.ConfirmarProduccionAsync(itemsSeleccionados);
            if (warnings.Any())
                TempData["Warning"] = string.Join("|", warnings);
            TempData["Success"] = "Producción confirmada. Stock actualizado.";
            return RedirectToAction(nameof(Produccion));
        }

        public async Task<IActionResult> Imprimir(bool conDetalles = false)
        {
            var pedidos = await _pedidoService.GetByEstadoAsync(EstadoPedido.Pendiente);
            ViewBag.ConDetalles = conDetalles;
            return View(pedidos);
        }

        [HttpGet]
        public async Task<IActionResult> PlanificarAmasadas()
        {
            var excluidos = LeerProductosExcluidos();
            var productos = await _pedidoService.GetIngredientesProduccionAsync(excluidos);
            var (_, _, porSubReceta, _) = await _pedidoService.GetResumenProduccionAsync(excluidos);
            ViewBag.CantidadExcluidos = excluidos.Count;
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            ViewBag.ProductosJson = JsonSerializer.Serialize(productos, jsonOptions);
            ViewBag.SubRecetasJson = JsonSerializer.Serialize(porSubReceta, jsonOptions);
            return View();
        }

        public async Task<IActionResult> ImprimirProduccion(string? anteriores = null)
        {
            var excluidos = LeerProductosExcluidos();
            var porProducto = await _pedidoService.GetProduccionCombinadaResumenAsync(excluidos);
            var (_, _, porSubReceta, _) = await _pedidoService.GetResumenProduccionAsync(excluidos);

            // Cantidad "anterior" ingresada en el dashboard, por sub-receta.
            // Formato del parametro: "idSubReceta:gramos,idSubReceta:gramos"
            var anterioresMapa = new Dictionary<int, decimal>();
            if (!string.IsNullOrWhiteSpace(anteriores))
            {
                foreach (var par in anteriores.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = par.Split(':');
                    if (kv.Length == 2
                        && int.TryParse(kv[0], out var idSub)
                        && decimal.TryParse(kv[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var ant)
                        && ant > 0)
                    {
                        anterioresMapa[idSub] = ant;
                    }
                }
            }

            var vm = new ImprimirProduccionViewModel
            {
                Fecha = DateTime.Today,
                SubRecetas = porSubReceta,
                Anteriores = anterioresMapa
            };
            foreach (var item in porProducto)
            {
                var receta = await _recetaService.GetByProductoIdAsync(item.IdProducto);
                if (receta == null) continue;
                vm.Items.Add(new ImprimirProduccionItemViewModel
                {
                    NombreProducto = item.NombreProducto,
                    CantidadUnidades = item.CantidadTotal,
                    Receta = receta
                });
            }
            return View(vm);
        }

        public async Task<IActionResult> Index()
        {
            var pedidos = await _pedidoService.GetByEstadoAsync(EstadoPedido.Pendiente);
            ViewBag.TotalVendidoSemana = await _pedidoService.GetTotalVendidoSemanaAsync();
            return View(pedidos);
        }

        public async Task<IActionResult> Details(int id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null) return NotFound();

            var vm = new PedidoDetailsViewModel
            {
                Id = pedido.Id,
                NombreCliente = pedido.Cliente.NombreCompleto,
                Estado = pedido.Estado,
                FechaEntrega = pedido.FechaEntrega,
                Notas = pedido.Notas,
                MontoTotal = pedido.MontoTotal,
                MontoCobrado = pedido.MontoCobrado,
                SaldoPendiente = pedido.SaldoPendiente,
                EstaPagado = pedido.EstaPagado,
                Detalles = pedido.Detalles.Select(d => new DetallePedidoDetailsViewModel
                {
                    NombreProducto = d.Producto.NombreVisible,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Cantidad * d.PrecioUnitario
                }).ToList()
            };

            return View(vm);
        }

        private async Task CargarDropdowns()
        {
            var clientes = await _clienteService.GetAllAsync();
            var productos = await _productoService.GetAllAsync();
            var empaques = await _insumoService.GetEmpaquesAsync();

            ViewBag.Clientes = clientes;
            ViewBag.Productos = productos;
            ViewBag.Empaques = empaques;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var hoy = DateTime.UtcNow.Date;
            int dias = ((int)DayOfWeek.Saturday - (int)hoy.DayOfWeek + 7) % 7;
            var proximoSabado = hoy.AddDays(dias);
            await CargarDropdowns();
            return View(new PedidoViewModel { FechaEntrega = proximoSabado });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PedidoViewModel vm)
        {
            if (vm.Detalles == null || !vm.Detalles.Any())
                ModelState.AddModelError("", "El pedido debe tener al menos un producto.");

            if (!ModelState.IsValid)
            {
                await CargarDropdowns();
                return View(vm);
            }

            var cliente = await _clienteService.GetByIdAsync(vm.IdCliente);
            if (cliente == null)
            {
                ModelState.AddModelError("", "Cliente no válido.");
                await CargarDropdowns();
                return View(vm);
            }

            var pedido = new Pedido
            {
                IdCliente = vm.IdCliente,
                Estado = EstadoPedido.Pendiente,
                FechaEntrega = vm.FechaEntrega.HasValue
                    ? DateTime.SpecifyKind(vm.FechaEntrega.Value, DateTimeKind.Utc)
                    : null,
                Notas = vm.Notas,
                FechaCreacion = DateTime.UtcNow,
                Detalles = new List<DetallePedido>()
            };

            foreach (var d in vm.Detalles)
            {
                var producto = await _productoService.GetByIdAsync(d.IdProducto);
                if (producto == null) continue;

                var precio = cliente.Revendedor ? producto.PrecioReventa : producto.PrecioFinal;

                pedido.Detalles.Add(new DetallePedido
                {
                    IdProducto = d.IdProducto,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = precio,
                    IdEmpaque = d.IdEmpaque,
                    LlevaEtiqueta = d.LlevaEtiqueta
                });
            }

            pedido.MontoTotal = pedido.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);

            await _pedidoService.CreateAsync(pedido);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null) return NotFound();

            var vm = new PedidoViewModel
            {
                Id = pedido.Id,
                IdCliente = pedido.IdCliente,
                Estado = pedido.Estado,
                FechaEntrega = pedido.FechaEntrega,
                FechaCreacion = pedido.FechaCreacion,
                Notas = pedido.Notas,
                Detalles = pedido.Detalles.Select(d => new DetallePedidoViewModel
                {
                    IdProducto = d.IdProducto,
                    Cantidad = d.Cantidad,
                    IdEmpaque = d.IdEmpaque,
                    LlevaEtiqueta = d.LlevaEtiqueta
                }).ToList()
            };

            await CargarDropdowns();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PedidoViewModel vm)
        {
            if (vm.Detalles == null || !vm.Detalles.Any())
                ModelState.AddModelError("", "El pedido debe tener al menos un producto.");

            if (!ModelState.IsValid)
            {
                await CargarDropdowns();
                return View(vm);
            }

            var cliente = await _clienteService.GetByIdAsync(vm.IdCliente);
            if (cliente == null)
            {
                ModelState.AddModelError("", "Cliente no válido.");
                await CargarDropdowns();
                return View(vm);
            }

            var detalles = new List<DetallePedido>();
            foreach (var d in vm.Detalles)
            {
                var producto = await _productoService.GetByIdAsync(d.IdProducto);
                if (producto == null) continue;

                var precio = cliente.Revendedor ? producto.PrecioReventa : producto.PrecioFinal;
                detalles.Add(new DetallePedido
                {
                    IdProducto = d.IdProducto,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = precio,
                    IdEmpaque = d.IdEmpaque,
                    LlevaEtiqueta = d.LlevaEtiqueta
                });
            }

            var pedido = new Pedido
            {
                Id = vm.Id,
                IdCliente = vm.IdCliente,
                Estado = vm.Estado,
                FechaEntrega = vm.FechaEntrega.HasValue
                    ? DateTime.SpecifyKind(vm.FechaEntrega.Value, DateTimeKind.Utc)
                    : null,
                Notas = vm.Notas,
                MontoTotal = detalles.Sum(d => d.Cantidad * d.PrecioUnitario),
                Detalles = detalles
            };

            await _pedidoService.UpdateAsync(pedido);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarCobro(RegistrarCobroViewModel vm)
        {
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Index));

            var pedido = await _pedidoService.GetByIdAsync(vm.IdPedido);
            if (pedido == null) return NotFound();

            await _pedidoService.RegistrarCobroAsync(vm.IdPedido, vm.Monto);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarEntregado(int id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null) return NotFound();

            if (pedido.MontoCobrado < pedido.MontoTotal)
            {
                TempData["Error"] = "No se puede marcar como entregado: el pedido no está cobrado en su totalidad.";
                return RedirectToAction(nameof(Index));
            }

            await _pedidoService.MarcarEntregadoAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null) return NotFound();

            if (pedido.MontoCobrado > 0)
            {
                TempData["Error"] = "No se puede eliminar un pedido que ya tiene cobros registrados.";
                return RedirectToAction(nameof(Index));
            }

            await _pedidoService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null) return NotFound();
            await _pedidoService.AnularAsync(id);
            TempData["Success"] = "Pedido anulado correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}