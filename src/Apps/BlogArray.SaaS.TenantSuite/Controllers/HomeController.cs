using Microsoft.AspNetCore.Authorization;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToActionPermanent("Index", "Dashboard");
    }
}
