using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Panaderia.Models.Entities;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    public class PedidoController : Controller
    {
        private readonly IPedidoService _pedidoService;
        private readonly IClienteService _clienteService;
        private readonly IProductoService _productoService;

        public PedidoController(
            IPedidoService pedidoService,
            IClienteService clienteService,
            IProductoService productoService)
        {
            _pedidoService = pedidoService;
            _clienteService = clienteService;
            _productoService = productoService;
        }

        public async Task<IActionResult> Index()
        {
            var pedidos = await _pedidoService.GetAllAsync();
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
                    NombreProducto = d.Producto.Nombre,
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
            await CargarDropdowns();
            return View(new PedidoViewModel());
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
                Estado = vm.Estado,
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
        public async Task<IActionResult> Delete(int id)
        {
            var pedido = await _pedidoService.GetByIdAsync(id);
            if (pedido == null) return NotFound();

            if (pedido.MontoCobrado > 0)
            {
                TempData["Error"] = "No se puede eliminar un pedido que ya tiene cobros registrados.";
                return RedirectToAction(nameof(Index));
            }

            await _pedidoService.AnularAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
