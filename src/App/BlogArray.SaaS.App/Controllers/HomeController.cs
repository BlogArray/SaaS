using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;

namespace BlogArray.SaaS.App.Controllers
{
    public class HomeController(IMultiTenantStore<AppTenantInfo> tenantStore, ILogger<HomeController> logger) : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
