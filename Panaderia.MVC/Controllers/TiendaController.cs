using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    [AllowAnonymous]
    public class TiendaController : Controller
    {
        private const string CookieCarrito = "mv_carrito";
        private const string CookieDatosCliente = "mv_datos_cliente";

        private readonly IProductoService _productoService;
        private readonly IClienteService _clienteService;
        private readonly IPedidoService _pedidoService;
        private readonly IConfiguration _configuration;

        public TiendaController(
            IProductoService productoService,
            IClienteService clienteService,
            IPedidoService pedidoService,
            IConfiguration configuration)
        {
            _productoService = productoService;
            _clienteService = clienteService;
            _pedidoService = pedidoService;
            _configuration = configuration;
        }

        // GET: / (tienda pública)
        public async Task<IActionResult> Index(string? categoria, string? q, int? etiqueta)
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

            // Etiquetas disponibles (solo las asignadas a algún producto visible)
            var etiquetas = productos
                .SelectMany(p => p.Etiquetas)
                .GroupBy(e => e.Id)
                .Select(g => g.First())
                .OrderBy(e => e.Nombre, comparer)
                .ToList();

            if (etiqueta.HasValue)
            {
                productos = productos
                    .Where(p => p.Etiquetas.Any(e => e.Id == etiqueta.Value))
                    .ToList();
            }

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
                Busqueda = q,
                Etiquetas = etiquetas,
                EtiquetaSeleccionada = etiqueta
            };

            return View(vm);
        }

        // GET: /Tienda/Detalle/5
        public async Task<IActionResult> Detalle(int id)
        {
            var producto = await _productoService.GetByIdAsync(id);
            if (producto == null || producto.OcultoEnTienda) return NotFound();

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

        // POST: /Tienda/Agregar — suma un producto al carrito (cookie).
        // origen "tienda" vuelve al catálogo (manteniendo filtros); si no, va al carrito.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Agregar(int id, int cantidad = 1, string? origen = null, string? categoria = null, string? q = null, int? etiqueta = null)
        {
            var producto = await _productoService.GetByIdAsync(id);
            if (producto == null || producto.OcultoEnTienda) return NotFound();

            if (producto.SinStock)
            {
                TempData["TiendaMsg"] = $"{producto.NombreVisible} está sin stock por el momento.";
                return RedirectToAction(nameof(Detalle), new { id });
            }

            if (cantidad < 1) cantidad = 1;
            if (cantidad > 50) cantidad = 50;

            var carrito = LeerCarrito();
            carrito.TryGetValue(id, out var actual);
            carrito[id] = Math.Min(actual + cantidad, 50);
            GuardarCarrito(carrito);

            TempData["TiendaMsg"] = $"{producto.NombreVisible} agregado al carrito.";

            if (origen == "tienda")
            {
                var url = Url.Action(nameof(Index), new { categoria, q, etiqueta });
                return Redirect(url + "#productos");
            }

            return RedirectToAction(nameof(Carrito));
        }

        // GET: /Tienda/Carrito
        public async Task<IActionResult> Carrito()
        {
            var vm = await ArmarCarritoAsync();
            return View(vm);
        }

        // POST: /Tienda/Actualizar — cambia la cantidad de un producto (0 = quitar)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Actualizar(int id, int cantidad)
        {
            var carrito = LeerCarrito();

            if (cantidad <= 0)
                carrito.Remove(id);
            else
                carrito[id] = Math.Min(cantidad, 50);

            GuardarCarrito(carrito);
            return RedirectToAction(nameof(Carrito));
        }

        // POST: /Tienda/Quitar — elimina un producto del carrito
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Quitar(int id)
        {
            var carrito = LeerCarrito();
            carrito.Remove(id);
            GuardarCarrito(carrito);
            return RedirectToAction(nameof(Carrito));
        }

        // GET: /Tienda/Checkout
        public async Task<IActionResult> Checkout()
        {
            var carrito = await ArmarCarritoAsync();
            if (!carrito.Items.Any()) return RedirectToAction(nameof(Carrito));

            var vm = new CheckoutViewModel
            {
                Carrito = carrito,
                FechaEntrega = ProximoSabado()
            };

            // Precargar con los datos de la compra anterior (guardados en el equipo del cliente)
            var recordados = LeerDatosCliente();
            if (recordados != null)
            {
                vm.Nombre = recordados.Nombre;
                vm.Apellido = recordados.Apellido;
                vm.Telefono = recordados.Telefono;
                vm.Direccion = recordados.Direccion;
                vm.Entrega = string.IsNullOrWhiteSpace(recordados.Entrega) ? "delivery" : recordados.Entrega;
                vm.MedioPago = string.IsNullOrWhiteSpace(recordados.MedioPago) ? "efectivo" : recordados.MedioPago;
                vm.DatosRecordados = true;

                // El admin es la fuente de verdad: si Maxi corrigió el nombre o la dirección,
                // gana lo que está en la ficha del cliente.
                if (!string.IsNullOrWhiteSpace(recordados.Telefono))
                {
                    var cliente = await _clienteService.GetByTelefonoAsync(recordados.Telefono);
                    if (cliente != null)
                    {
                        vm.Nombre = cliente.Nombre;
                        vm.Apellido = cliente.Apellido;
                        if (!string.IsNullOrWhiteSpace(cliente.Direccion))
                            vm.Direccion = cliente.Direccion;
                    }
                }
            }

            return View(vm);
        }

        // POST: /Tienda/OlvidarDatos — limpia los datos guardados en el equipo del cliente
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult OlvidarDatos()
        {
            Response.Cookies.Delete(CookieDatosCliente);
            return RedirectToAction(nameof(Checkout));
        }

        // POST: /Tienda/Confirmar — crea el pedido real
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmar(CheckoutViewModel model)
        {
            var carrito = await ArmarCarritoAsync();
            if (!carrito.Items.Any()) return RedirectToAction(nameof(Carrito));

            var esDelivery = model.Entrega == "delivery";
            if (esDelivery && string.IsNullOrWhiteSpace(model.Direccion))
                ModelState.AddModelError(nameof(model.Direccion), "Indicanos la dirección para el delivery.");

            if (!ModelState.IsValid)
            {
                model.Carrito = carrito;
                model.FechaEntrega = ProximoSabado();
                return View("Checkout", model);
            }

            // Cliente invitado: buscar por teléfono o crear uno nuevo
            var cliente = await _clienteService.GetByTelefonoAsync(model.Telefono);
            if (cliente == null)
            {
                cliente = new Cliente
                {
                    Nombre = model.Nombre.Trim(),
                    Apellido = string.IsNullOrWhiteSpace(model.Apellido) ? null : model.Apellido.Trim(),
                    Telefono = model.Telefono.Trim(),
                    Direccion = esDelivery ? model.Direccion?.Trim() : null,
                    FechaCreacion = DateTime.UtcNow
                };
                await _clienteService.CreateAsync(cliente);
            }
            else if (esDelivery && string.IsNullOrWhiteSpace(cliente.Direccion) && !string.IsNullOrWhiteSpace(model.Direccion))
            {
                cliente.Direccion = model.Direccion.Trim();
                await _clienteService.UpdateAsync(cliente);
            }

            var entregaTexto = esDelivery
                ? $"Delivery sin cargo — {model.Direccion?.Trim()}"
                : "Retiro en Kiosco Suyay (San Martín 888)";

            var pagoTexto = model.MedioPago == "transferencia"
                ? "Transferencia (alias masaviva.pan)"
                : "Efectivo";

            var notas = $"[Tienda] {entregaTexto} · Pago: {pagoTexto}";
            if (!string.IsNullOrWhiteSpace(model.Notas))
                notas += $" · Nota del cliente: {model.Notas.Trim()}";

            var pedido = new Pedido
            {
                IdCliente = cliente.Id,
                FechaEntrega = ProximoSabado(),
                MontoTotal = carrito.Total,
                Notas = notas,
                FechaCreacion = DateTime.UtcNow,
                Detalles = carrito.Items.Select(i => new DetallePedido
                {
                    IdProducto = i.Producto.Id,
                    Cantidad = i.Cantidad,
                    PrecioUnitario = i.Producto.PrecioFinal
                }).ToList()
            };

            await _pedidoService.CreateAsync(pedido);

            // Vaciar el carrito
            GuardarCarrito(new Dictionary<int, int>());

            // Recordar los datos para el próximo pedido
            GuardarDatosCliente(new DatosClienteRecordados
            {
                Nombre = model.Nombre.Trim(),
                Apellido = string.IsNullOrWhiteSpace(model.Apellido) ? null : model.Apellido.Trim(),
                Telefono = model.Telefono.Trim(),
                Direccion = esDelivery ? model.Direccion?.Trim() : null,
                Entrega = model.Entrega,
                MedioPago = model.MedioPago
            });

            TempData["PedidoId"] = pedido.Id;
            TempData["PedidoFechaEntrega"] = pedido.FechaEntrega?.ToString("O");
            TempData["PedidoEntrega"] = model.Entrega;
            TempData["PedidoMedioPago"] = model.MedioPago;
            TempData["PedidoWhatsApp"] = ArmarMensajeWhatsApp(pedido, cliente, carrito, entregaTexto, pagoTexto, model.Notas);
            return RedirectToAction(nameof(Confirmacion));
        }

        // GET: /Tienda/Confirmacion
        public IActionResult Confirmacion()
        {
            if (TempData["PedidoId"] == null) return RedirectToAction(nameof(Index));

            ViewBag.PedidoId = TempData["PedidoId"];
            ViewBag.FechaEntrega = TempData["PedidoFechaEntrega"] is string fecha
                ? DateTime.Parse(fecha, null, DateTimeStyles.RoundtripKind)
                : (DateTime?)null;
            ViewBag.Entrega = TempData["PedidoEntrega"] as string;
            ViewBag.MedioPago = TempData["PedidoMedioPago"] as string;

            // Link de WhatsApp hacia la panadería con el detalle del pedido.
            // Lo inicia el cliente, así que no requiere la API paga de Meta.
            var numero = new string((_configuration["Tienda:WhatsApp"] ?? "").Where(char.IsDigit).ToArray());
            var mensaje = TempData["PedidoWhatsApp"] as string;
            ViewBag.LinkWhatsApp = (!string.IsNullOrEmpty(numero) && !string.IsNullOrEmpty(mensaje))
                ? $"https://wa.me/{numero}?text={Uri.EscapeDataString(mensaje)}"
                : null;

            return View();
        }

        // Mensaje que el cliente le envía a la panadería al confirmar
        private static string ArmarMensajeWhatsApp(
            Pedido pedido, Cliente cliente, CarritoViewModel carrito,
            string entregaTexto, string pagoTexto, string? notaCliente)
        {
            var ar = new CultureInfo("es-AR");
            var sb = new System.Text.StringBuilder();

            // Sin número de pedido: es un dato interno, el cliente no lo necesita ver.
            sb.AppendLine("¡Hola Masa Viva! Confirmo mi pedido 🍞");
            sb.AppendLine();
            sb.AppendLine($"*Cliente:* {cliente.NombreCompleto}");
            sb.AppendLine($"*Entrega:* {pedido.FechaEntrega:dd/MM} — {entregaTexto}");
            sb.AppendLine($"*Pago:* {pagoTexto}");
            sb.AppendLine();
            sb.AppendLine("*Pedido:*");

            foreach (var item in carrito.Items)
                sb.AppendLine($"• {item.Cantidad}x {item.Producto.NombreVisible} — ${item.Subtotal.ToString("N0", ar)}");

            sb.AppendLine();
            sb.AppendLine($"*TOTAL: ${carrito.Total.ToString("N0", ar)}*");

            if (!string.IsNullOrWhiteSpace(notaCliente))
            {
                sb.AppendLine();
                sb.AppendLine($"_Nota: {notaCliente.Trim()}_");
            }

            return sb.ToString();
        }

        // ---------- Helpers ----------

        // La entrega es fija: sábados de 10:30 a 12:30. Si hoy es sábado, va al siguiente.
        private static DateTime ProximoSabado()
        {
            var hoyArgentina = DateTime.UtcNow.AddHours(-3).Date;
            var dias = ((int)DayOfWeek.Saturday - (int)hoyArgentina.DayOfWeek + 7) % 7;
            if (dias == 0) dias = 7;
            return DateTime.SpecifyKind(hoyArgentina.AddDays(dias), DateTimeKind.Utc);
        }

        // Datos del cliente guardados en su propio equipo para no recargarlos en cada compra
        private class DatosClienteRecordados
        {
            public string Nombre { get; set; } = string.Empty;
            public string? Apellido { get; set; }
            public string Telefono { get; set; } = string.Empty;
            public string? Direccion { get; set; }
            public string? Entrega { get; set; }
            public string? MedioPago { get; set; }
        }

        private DatosClienteRecordados? LeerDatosCliente()
        {
            var cookie = Request.Cookies[CookieDatosCliente];
            if (string.IsNullOrEmpty(cookie)) return null;

            try
            {
                var datos = JsonSerializer.Deserialize<DatosClienteRecordados>(cookie);
                return string.IsNullOrWhiteSpace(datos?.Telefono) ? null : datos;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private void GuardarDatosCliente(DatosClienteRecordados datos)
        {
            Response.Cookies.Append(CookieDatosCliente, JsonSerializer.Serialize(datos), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(180)
            });
        }

        private Dictionary<int, int> LeerCarrito()
        {
            var cookie = Request.Cookies[CookieCarrito];
            if (string.IsNullOrEmpty(cookie)) return new Dictionary<int, int>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<int, int>>(cookie) ?? new Dictionary<int, int>();
            }
            catch (JsonException)
            {
                return new Dictionary<int, int>();
            }
        }

        private void GuardarCarrito(Dictionary<int, int> carrito)
        {
            if (!carrito.Any())
            {
                Response.Cookies.Delete(CookieCarrito);
                return;
            }

            Response.Cookies.Append(CookieCarrito, JsonSerializer.Serialize(carrito), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
        }

        private async Task<CarritoViewModel> ArmarCarritoAsync()
        {
            var carrito = LeerCarrito();
            var vm = new CarritoViewModel();
            if (!carrito.Any()) return vm;

            var productos = (await _productoService.GetAllAsync())
                .Where(p => !p.OcultoEnTienda && !p.SinStock)
                .ToDictionary(p => p.Id);

            var huboCambios = false;
            foreach (var (idProducto, cantidad) in carrito.ToList())
            {
                if (productos.TryGetValue(idProducto, out var producto))
                {
                    vm.Items.Add(new CarritoItemViewModel { Producto = producto, Cantidad = cantidad });
                }
                else
                {
                    // El producto fue ocultado, eliminado o quedó sin stock: sale del carrito
                    carrito.Remove(idProducto);
                    huboCambios = true;
                }
            }

            if (huboCambios) GuardarCarrito(carrito);

            return vm;
        }
    }
}
