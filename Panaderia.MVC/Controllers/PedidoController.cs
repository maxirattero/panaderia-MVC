using Microsoft.AspNetCore.Mvc;
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

        public PedidoController(
            IPedidoService pedidoService,
            IClienteService clienteService,
            IProductoService productoService,
            IRecetaService recetaService)
        {
            _pedidoService = pedidoService;
            _clienteService = clienteService;
            _productoService = productoService;
            _recetaService = recetaService;
        }

        public async Task<IActionResult> Produccion()
        {
            var (porProducto, porBolsa, porSubReceta) = await _pedidoService.GetResumenProduccionAsync();
            var vm = new ProduccionViewModel
            {
                PorProducto  = porProducto,
                PorBolsa     = porBolsa,
                PorSubReceta = porSubReceta
            };
            foreach (var item in vm.PorProducto)
            {
                var receta = await _recetaService.GetByProductoIdAsync(item.IdProducto);
                if (receta != null)
                {
                    vm.ItemsSeleccionables.Add(new ItemProduccionSeleccionable
                    {
                        IdProducto        = item.IdProducto,
                        IdReceta          = receta.Id,
                        NombreProducto    = item.NombreProducto,
                        CantidadSugerida  = item.CantidadTotal,
                        CantidadAProducir = item.CantidadTotal,
                        Seleccionado      = true
                    });
                }
            }
            return View(vm);
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

        public async Task<IActionResult> ImprimirProduccion()
        {
            var (porProducto, _, porSubReceta) = await _pedidoService.GetResumenProduccionAsync();
            var vm = new ImprimirProduccionViewModel
            {
                Fecha      = DateTime.Today,
                SubRecetas = porSubReceta
            };
            foreach (var item in porProducto)
            {
                var receta = await _recetaService.GetByProductoIdAsync(item.IdProducto);
                if (receta == null) continue;
                vm.Items.Add(new ImprimirProduccionItemViewModel
                {
                    NombreProducto   = item.NombreProducto,
                    CantidadUnidades = item.CantidadTotal,
                    Receta           = receta
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
                    Subtotal = d.Cantidad * d.PrecioUnitario,
                    Bolsa = d.Bolsa
                }).ToList()
            };

            return View(vm);
        }

        private async Task CargarDropdowns()
        {
            var clientes = await _clienteService.GetAllAsync();
            var productos = await _productoService.GetAllAsync();

            ViewBag.Clientes = clientes;
            ViewBag.Productos = productos;
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
                    Bolsa = d.Bolsa
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
                    Bolsa = d.Bolsa
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
                    Bolsa = d.Bolsa
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
