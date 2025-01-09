using BlogArray.SaaS.Middleware;
using BlogArray.SaaS.Domain.Entities;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;
using BlogArray.SaaS.Domain.DTOs;

namespace BlogArray.SaaS.TenantStore;

public static class ConfigureTenantStoreServices
{
    public static IHostApplicationBuilder AddTenantStore(this IHostApplicationBuilder builder)
    {
        IConfiguration Configuration = builder.Configuration;

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
       .AddCookie(options =>
       {
           //These are the default options, we can override the options PER TENANT in ConfigurePerTenant<CookieAuthenticationOptions, AppTenantInfo>
           options.AddBlogArrayCookieAuthenticationOptions();
       })
       .AddOpenIdConnect(options =>
       {
           //https://github.com/dotnet/aspnetcore/blob/main/src/Security/Authentication/OpenIdConnect/samples/OpenIdConnectSample/Startup.cs
           //These are the default options, we can override the options PER TENANT in ConfigurePerTenant<OpenIdConnectOptions, AppTenantInfo>
           options.ClientId = "tenant";
           options.ClientSecret = "tenant";
           options.Authority = Configuration["Links:Authority"];
           options.ClaimsIssuer = Configuration["Links:Issuer"];
           options.ResponseType = OpenIdConnectResponseType.Code;

           options.Scope.Add("openid");
           options.Scope.Add("email");
           options.Scope.Add("profile");
           options.Scope.Add("roles");

           options.GetClaimsFromUserInfoEndpoint = true;

           options.Events = new OpenIdConnectEvents
           {
               //There are other events can be found here https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.openidconnect.openidconnectevents?view=aspnetcore-9.0
               OnTokenValidated = async context =>
               {
                   //Triggered after the tokens(ID token, access token) have been successfully validated. This is a common place to add or manipulate user claims.
                   //Use Cases:
                   //Performing additional validation on the tokens like tenant validation, user exists in the tenant.
                   //Adding custom claims to the user’s principal.
                   ClaimsIdentity? identity = context?.Principal?.Identity as ClaimsIdentity;

                   AppTenantInfo? multiTenantContext = context.HttpContext.RequestServices.GetRequiredService<IMultiTenantContextAccessor<AppTenantInfo>>().MultiTenantContext.TenantInfo;

                   //using IServiceScope scopeServices = builder.Services.scop.ApplicationServices.CreateScope();

                   //SaasAppDbContext dbContext = scopeServices.ServiceProvider.GetRequiredService<SaasAppDbContext>();
                   SaasAppDbContext dbContext = context.HttpContext.RequestServices.GetRequiredService<SaasAppDbContext>();

                   if (identity != null && !identity.IsAuthenticated)
                   {
                       context?.HandleResponse();
                       context?.Response.Redirect($"{multiTenantContext.Identifier}/error/accessdenied?message=Unable to retrive the User details. Please contact your administrator if the issue persists.");
                   }
                   else
                   {
                       string? email = identity?.FindFirst(ClaimTypes.Email)?.Value;

                       if (string.IsNullOrEmpty(email))
                       {
                           context?.HandleResponse();
                           context?.Response.Redirect($"{multiTenantContext.Identifier}/error/accessdenied?message=Unable to retrive the user email. Please contact your administrator if the issue persists.");
                       }

                       AppPersonnel? appuser = dbContext.AppPersonnels.SingleOrDefault(s => s.Email == email);

                       if (appuser is null || appuser.IsActive is false)
                       {
                           context?.HandleResponse();
                           context?.Response.Redirect($"{multiTenantContext.Identifier}/error/accessdenied?message=User details not found in BlogArray. Please contact your administrator if the issue persists.");
                       }

                       string? audience = identity?.FindFirst(Claims.Audience)?.Value;
                       string? name = identity?.FindFirst(Claims.Name)?.Value;
                       identity?.AddClaim(new Claim("tenant", audience ?? ""));
                       identity?.AddClaim(new Claim(ClaimTypes.GivenName, name ?? ""));
                       identity?.AddClaim(new Claim(ClaimTypes.Name, name ?? ""));
                   }
                   await Task.CompletedTask;
               },
               OnUserInformationReceived = async context =>
               {
                   //Triggered when user information is retrieved from the IdP’s userinfo endpoint.
                   //Use Cases:
                   //Adding claims from the userinfo response to the user’s identity.
                   //Logging or processing additional user information.
                   System.Text.Json.JsonElement userInfo = context.User.RootElement;

                   string? image = userInfo.GetString("image");

                   Dictionary<string, string> allClaims = userInfo.EnumerateObject()
                       .ToDictionary(p => p.Name, p => p.Value.ToString());

                   ClaimsIdentity? identity = context?.Principal?.Identity as ClaimsIdentity;

                   identity?.AddClaim(new Claim("image", image ?? ""));

                   await Task.CompletedTask;
               },
               OnAuthenticationFailed = context =>
               {
                   //Triggered when authentication fails for any reason, such as invalid tokens or a mismatch in state values.
                   //Use Cases:
                   //Logging authentication errors.
                   //Redirecting users to a custom error page.
                   AppTenantInfo? multiTenantContext = context.HttpContext.RequestServices.GetRequiredService<IMultiTenantContextAccessor<AppTenantInfo>>().MultiTenantContext.TenantInfo;

                   context.Response.Redirect($"{multiTenantContext.Identifier}/error?message=" + context.Exception.Message);
                   context.HandleResponse(); // Prevent further processing
                   return Task.CompletedTask;
               },
               OnRemoteFailure = context =>
               {
                   //Triggered when there is a failure in the remote authentication process(e.g., a network issue or IdP error).
                   //Use Cases:
                   //Logging errors related to remote authentication.
                   //Redirecting users to a fallback page.
                   AppTenantInfo? multiTenantContext = context.HttpContext.RequestServices.GetRequiredService<IMultiTenantContextAccessor<AppTenantInfo>>().MultiTenantContext.TenantInfo;

                   context.Response.Redirect($"{multiTenantContext.Identifier}/error?message=" + context.Failure?.Message);
                   context.HandleResponse();
                   return Task.CompletedTask;
               }
           };
       });

        builder.Services.AddMultiTenant<AppTenantInfo>()
            .WithRouteStrategy()
            .WithRemoteAuthenticationCallbackStrategy()
            .WithDistributedCacheStore(TimeSpan.FromMinutes(5))
            .WithPerTenantAuthentication();

        builder.Services.ConfigurePerTenant<CookieAuthenticationOptions, AppTenantInfo>(CookieAuthenticationDefaults.AuthenticationScheme, (options, tenantInfo) =>
        {
            options.Cookie.Path = $"/{tenantInfo.Identifier}";
            options.Cookie.Name = "Cookie-" + tenantInfo.Identifier;
        });

        builder.Services.ConfigurePerTenant<OpenIdConnectOptions, AppTenantInfo>(OpenIdConnectDefaults.AuthenticationScheme, (options, tenantInfo) =>
        {
            options.ClientId = tenantInfo.Identifier;
            options.ClientSecret = tenantInfo.ClientSecretPlain;
        });

        return builder;
    }
}
