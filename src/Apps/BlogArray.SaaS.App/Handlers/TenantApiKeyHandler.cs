using Microsoft.AspNetCore.DataProtection;

namespace BlogArray.SaaS.App.Handlers;

public class TenantApiKeyHandler(IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor, IDataProtector protector) : DelegatingHandler
{
    private const string ApiKeyHeaderName = "X-API-Key";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // The tenant store carries the API key protected; it is unprotected only here, in
        // memory, for the outgoing request.
        string? protectedApiKey = tenantAccessor.MultiTenantContext?.TenantInfo?.APIKeyProtected;

        if (!string.IsNullOrEmpty(protectedApiKey))
        {
            request.Headers.Add(ApiKeyHeaderName, protector.Unprotect(protectedApiKey));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
