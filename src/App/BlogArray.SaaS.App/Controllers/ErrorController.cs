using Microsoft.AspNetCore.Authorization;

namespace BlogArray.SaaS.App.Controllers;

[AllowAnonymous]
public class ErrorController : Controller
{
    [ResponseCache(Duration = 30672000, Location = ResponseCacheLocation.Client)]
    public IActionResult Index(string message = "")
    {
        TempData["ErrorMessage"] = string.IsNullOrEmpty(message) ? "An unexpected error occurred on the server. Please try again later." : message;
        int statusCode = HttpContext.Response.StatusCode;
        return statusCode == 404 ? NotFound(message) : statusCode == 403 ? AccessDenied(message) : View();
    }

    [ResponseCache(Duration = 30672000, Location = ResponseCacheLocation.Client)]
    public IActionResult NotFound(string message = "")
    {
        TempData["ErrorMessage"] = string.IsNullOrEmpty(message) ? "The page or resource you’re looking for doesn’t exist. Please check the URL and try again." : message;
        return View();
    }

    [ResponseCache(Duration = 30672000, Location = ResponseCacheLocation.Client)]
    public IActionResult AccessDenied(string message = "")
    {
        TempData["ErrorMessage"] = string.IsNullOrEmpty(message) ? "You don’t have permission to access this resource. Please contact your administrator if needed." : message;
        return View();
    }
}
