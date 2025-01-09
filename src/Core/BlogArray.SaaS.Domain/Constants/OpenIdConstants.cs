using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.Domain.Constants;

public static class OpenIdConstants
{
    public static HashSet<string> OpenIdPermissions()
    {
        return
        [
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.EndSession,
            Permissions.Endpoints.Token,

            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,

            Permissions.ResponseTypes.Code,

            Permissions.Scopes.Email,
            Permissions.Scopes.Profile,
            Permissions.Scopes.Roles,
            Permissions.Prefixes.Scope + "api"
        ];
    }

    public static HashSet<string> OpenIdRequirements()
    {
        return
        [
            Requirements.Features.ProofKeyForCodeExchange
        ];
    }
}
