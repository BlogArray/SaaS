using Microsoft.AspNetCore.Authorization;

namespace BlogArray.SaaS.Identity.Controllers
{
    [Authorize]
    public class HomeController(ILogger<HomeController> logger) : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
