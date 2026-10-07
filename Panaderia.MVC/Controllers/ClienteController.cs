using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ClienteController : Controller
    {
        private readonly IClienteService _clienteService;
        private readonly IAccesoTiendaService _accesoTiendaService;

        public ClienteController(IClienteService clienteService, IAccesoTiendaService accesoTiendaService)
        {
            _clienteService = clienteService;
            _accesoTiendaService = accesoTiendaService;
        }

        // GET: Clientes
        public async Task<IActionResult> Index()
        {
            return View(await _clienteService.GetAllAsync());
        }

        // GET: Detalles de Clientes
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cliente = await _clienteService.GetByIdAsync(id.Value);
            if (cliente == null)
            {
                return NotFound();
            }

            ViewBag.EmailAccesoTienda = await _accesoTiendaService.ObtenerEmailAsync(cliente.Id);
            ViewBag.AccesoTiendaHabilitado = await _accesoTiendaService.TieneAccesoAsync(cliente.Id);
            return View(cliente);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> CrearAcceso(int id)
        {
            var cliente = await _clienteService.GetByIdAsync(id);
            if (cliente == null) return NotFound();
            if (!cliente.Revendedor || await _accesoTiendaService.ObtenerEmailAsync(id) != null)
                return RedirectToAction(nameof(Details), new { id });
            return View(new CrearAccesoTiendaViewModel { IdCliente = id, NombreCliente = cliente.NombreCompleto });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> CrearAcceso(int id, CrearAccesoTiendaViewModel model)
        {
            var cliente = await _clienteService.GetByIdAsync(id);
            if (cliente == null) return NotFound();
            model.NombreCliente = cliente.NombreCompleto;
            model.IdCliente = id;
            if (!cliente.Revendedor)
                ModelState.AddModelError("", "Primero marcá al cliente como revendedor.");
            if (ModelState.IsValid)
            {
                var result = await _accesoTiendaService.CrearAsync(id, model.Email, model.Password);
                if (result.Succeeded)
                {
                    TempData["AccesoTiendaMsg"] = "Acceso creado. Compartí el correo y la contraseña inicial con el revendedor por un canal privado. No se envió un email automático.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            }
            // Conservar mensajes, pero nunca reenviar contraseñas al HTML ni TempData.
            var passwordErrors = ModelState.Where(e => e.Key == nameof(model.Password) || e.Key == nameof(model.ConfirmarPassword))
                .SelectMany(e => e.Value!.Errors).Select(e => e.ErrorMessage).ToList();
            ModelState.Remove(nameof(model.Password));
            ModelState.Remove(nameof(model.ConfirmarPassword));
            foreach (var error in passwordErrors) ModelState.AddModelError("", error);
            model.Password = model.ConfirmarPassword = "";
            return View(model);
        }

        //GET: Crear Cliente
        public IActionResult Create()
        {
            return View();
        }

        //POST : Crear Cliente
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(Cliente cliente)
        {
             if (ModelState.IsValid)
            {
                await _clienteService.CreateAsync(cliente);
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var cliente = await _clienteService.GetByIdAsync(id.Value);
            if (cliente == null) return NotFound();
            return View(new EditarClienteViewModel
            {
                Id = cliente.Id, Nombre = cliente.Nombre, Apellido = cliente.Apellido,
                Direccion = cliente.Direccion, Localidad = cliente.Localidad, Provincia = cliente.Provincia,
                Telefono = cliente.Telefono, Revendedor = cliente.Revendedor, PrecioDeCosto = cliente.PrecioDeCosto,
                DescuentoPorcentaje = cliente.DescuentoPorcentaje,
                EmailAcceso = await _accesoTiendaService.ObtenerEmailAsync(cliente.Id),
                AccesoTienda = await _accesoTiendaService.TieneAccesoAsync(cliente.Id)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Edit(int id, EditarClienteViewModel cliente)
        {
            if (id != cliente.Id || !await _clienteService.ExistsAsync(id)) return NotFound();
            if (ModelState.IsValid)
            {
                var result = await _accesoTiendaService.GuardarClienteAsync(cliente, cliente.AccesoTienda, cliente.EmailAcceso, cliente.NuevaPassword);
                if (result.Succeeded) return RedirectToAction(nameof(Index));
                foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            }
            var errors = ModelState.Where(e => e.Key == nameof(cliente.NuevaPassword) || e.Key == nameof(cliente.ConfirmarPassword))
                .SelectMany(e => e.Value!.Errors).Select(e => e.ErrorMessage).ToList();
            ModelState.Remove(nameof(cliente.NuevaPassword));
            ModelState.Remove(nameof(cliente.ConfirmarPassword));
            foreach (var error in errors) ModelState.AddModelError("", error);
            cliente.NuevaPassword = cliente.ConfirmarPassword = null;
            return View(cliente);
        }

        //POST: Eliminar Cliente
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (await _accesoTiendaService.ObtenerEmailAsync(id) != null)
            {
                TempData["AccesoTiendaMsg"] = "Este cliente tiene una cuenta vinculada y no se puede eliminar.";
                return RedirectToAction(nameof(Details), new { id });
            }
            await _clienteService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
