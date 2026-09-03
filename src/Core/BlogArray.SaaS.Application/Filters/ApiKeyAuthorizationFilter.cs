using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Domain.Helpers;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace BlogArray.SaaS.Application.Filters;

public class ApiKeyAuthorizationFilter(OpenIdDbContext context) : IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-API-Key";

    /// <summary>
    /// HttpContext.Items key under which the application resolved from the presented API key is stored,
    /// so downstream actions operate only on the tenant that owns the key.
    /// </summary>
    public const string TenantApplicationItemKey = "BlogArray.ApiKeyTenantApplication";

    public async Task OnActionExecutionAsync(ActionExecutingContext actionContext, ActionExecutionDelegate next)
    {
        if (!actionContext.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out StringValues apiKey)
            || string.IsNullOrWhiteSpace(apiKey))
        {
            actionContext.Result = new UnauthorizedObjectResult("Missing API key.");
            return;
        }

        // Plaintext keys are never stored: hash the presented key and match it against the
        // stored SHA-256 hash. Resolve the application that owns the key so the request can
        // be constrained to that tenant; never trust a caller-supplied tenant identifier.
        OpenIdApplication? application = await context.Applications
            .FirstOrDefaultAsync(a => a.APIKeyHash == ApiKeyHasher.Hash(apiKey.ToString()));

        if (application is null)
        {
            actionContext.Result = new UnauthorizedObjectResult("Invalid API key.");
            return;
        }

        actionContext.HttpContext.Items[TenantApplicationItemKey] = application;

        await next();
    }
}
