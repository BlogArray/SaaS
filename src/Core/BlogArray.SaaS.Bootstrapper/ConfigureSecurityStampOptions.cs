//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BlogArray.SaaS.Bootstrapper;

public class ConfigureSecurityStampOptions : IConfigureOptions<SecurityStampValidatorOptions>
{
    public void Configure(SecurityStampValidatorOptions options)
    {
        // Validate the security stamp periodically (framework default: 30 minutes). A short
        // interval means password changes / security-stamp updates terminate existing
        // sessions almost immediately; five minutes balances that protection against the
        // per-request database cost of revalidating every authenticated request.
        options.ValidationInterval = TimeSpan.FromMinutes(5);

        // When refreshing the principal, ensure custom claims that
        // might have been set with an external identity continue
        // to flow through to this new one.
        options.OnRefreshingPrincipal = refreshingPrincipal =>
        {
            ClaimsIdentity? newIdentity = refreshingPrincipal.NewPrincipal?.Identities.First();
            ClaimsIdentity? currentIdentity = refreshingPrincipal.CurrentPrincipal?.Identities.First();

            if (currentIdentity is not null && newIdentity is not null)
            {
                // Carry over only the claims the sign-in added (profile, amr, session_id,
                // saml_*). Claim types the principal factory issues - identity, security stamp
                // and roles - are authoritative from the fresh principal: merging them back
                // would resurrect a removed role, which the new principal simply omits.
                HashSet<string> authoritativeTypes = [.. newIdentity.Claims.Select(c => c.Type), newIdentity.RoleClaimType, ClaimTypes.Role];

                IEnumerable<Claim> currentClaimsNotInNewIdentity = currentIdentity.Claims.Where(c => !authoritativeTypes.Contains(c.Type));

                foreach (Claim claim in currentClaimsNotInNewIdentity)
                {
                    newIdentity.AddClaim(claim);
                }
            }

            return Task.CompletedTask;
        };
    }
}
