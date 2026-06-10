using Microsoft.AspNetCore.Mvc;
using Panaderia.Models.Entities;
using Panaderia.Services.Interfaces;

namespace Panaderia.MVC.Controllers
{
    public class ProveedorController : Controller
    {
        private readonly IProveedorService _proveedorService;

        public ProveedorController(IProveedorService proveedorService)
        {
            _proveedorService = proveedorService;
        }

        // GET: Proveedores
        public async Task<IActionResult> Index()
        {
            return View(await _proveedorService.GetAllAsync());
        }

        // GET: Detalles de proveedores
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var proveedor = await _proveedorService.GetByIdAsync(id.Value);
            if (proveedor == null)
            {
                return NotFound();
            }

            return View(proveedor);
        }

        // GET: Crear proveedor
        public IActionResult Create()
        {
            return View();
        }

        // POST: Crear proveedor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Proveedor proveedor)
        {
            if (ModelState.IsValid)
            {
                await _proveedorService.CreateAsync(proveedor);
                return RedirectToAction(nameof(Index));
            }
            return View(proveedor);
        }

        // GET: Editar proveedor
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var proveedor = await _proveedorService.GetByIdAsync(id.Value);
            if (proveedor == null)
            {
                return NotFound();
            }
            return View(proveedor);
        }

        // POST: Editar proveedor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Proveedor proveedor)
        {
            if (id != proveedor.Id) return NotFound();

            if (ModelState.IsValid)
            {
                await _proveedorService.UpdateAsync(proveedor);
                return RedirectToAction(nameof(Index));
            }
            return View(proveedor);
        }

        // GET: Eliminar proveedor
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var proveedor = await _proveedorService.GetByIdAsync(id.Value);
            if (proveedor == null)
            {
                return NotFound();
            }

            return View(proveedor);
        }

        // POST: Eliminar proveedor
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _proveedorService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}