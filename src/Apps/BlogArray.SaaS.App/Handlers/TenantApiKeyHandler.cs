using BlogArray.SaaS.Domain.DTOs;
using Finbuckle.MultiTenant.Abstractions;

namespace BlogArray.SaaS.App.Handlers;

public class TenantApiKeyHandler(IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor) : DelegatingHandler
{
    private const string ApiKeyHeaderName = "X-API-Key";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? apiKey = tenantAccessor.MultiTenantContext?.TenantInfo?.APIKey;

        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Add(ApiKeyHeaderName, apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
