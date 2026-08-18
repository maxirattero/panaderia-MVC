using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Panaderia.MVC.Models;
using Panaderia.Services.Interfaces;
using System.Diagnostics;

namespace Panaderia.MVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDashboardService _dashboardService;

        public HomeController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index()
        {
            var dto = await _dashboardService.GetResumenDashboardAsync();
            var vm = new DashboardViewModel
            {
                Caja       = dto.Caja,
                Alertas    = dto.Alertas,
                Produccion = dto.Produccion
            };
            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
