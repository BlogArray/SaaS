using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlogArray.SaaS.Resources.Controllers;

[AllowAnonymous]
public class ErrorController : Controller
{
    private const int CacheDuration = 30672000;

    [ResponseCache(Duration = CacheDuration, Location = ResponseCacheLocation.Client)]
    public IActionResult Index(string message = "")
    {
        int statusCode = HttpContext.Response.StatusCode;

        return statusCode switch
        {
            404 => NotFound(message),
            403 => AccessDenied(message),
            _ => CreateErrorView("Server Error",
                 string.IsNullOrEmpty(message)
                    ? "An unexpected error occurred on the server. Please try again later."
                    : message)
        };
    }

    [ResponseCache(Duration = CacheDuration, Location = ResponseCacheLocation.Client)]
    public IActionResult NotFound(string message = "")
    {
        return CreateErrorView("Resource not found",
               string.IsNullOrEmpty(message)
                  ? "The page or resource you’re looking for doesn’t exist. Please check the URL and try again."
                  : message);
    }

    [ResponseCache(Duration = CacheDuration, Location = ResponseCacheLocation.Client)]
    public IActionResult AccessDenied(string message = "")
    {
        return CreateErrorView("Access Denied",
               string.IsNullOrEmpty(message)
                  ? "You don’t have permission to access this resource. Please contact your administrator if needed."
                  : message);
    }

    private ViewResult CreateErrorView(string title, string errorMessage)
    {
        ViewData["Title"] = title;
        ViewData["ErrorMessage"] = errorMessage;
        return View();
    }
}
