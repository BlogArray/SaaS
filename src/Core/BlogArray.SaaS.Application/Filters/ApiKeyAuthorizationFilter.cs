using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace BlogArray.SaaS.Application.Filters;

public class ApiKeyAuthorizationFilter(OpenIdDbContext context) : IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-API-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext actionContext, ActionExecutionDelegate next)
    {
        if (!actionContext.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out StringValues apiKey)
            || string.IsNullOrWhiteSpace(apiKey))
        {
            actionContext.Result = new UnauthorizedObjectResult("Missing API key.");
            return;
        }

        bool isValidApiKey = await context.Applications.AnyAsync(a => a.APIKey == apiKey.ToString());

        if (!isValidApiKey)
        {
            actionContext.Result = new UnauthorizedObjectResult("Invalid API key.");
            return;
        }

        await next();
    }
}
