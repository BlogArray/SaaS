using BlogArray.SaaS.Application.Filters;
using BlogArray.SaaS.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using static OpenIddict.Abstractions.OpenIddictConstants;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext());

ConfigurationManager Configuration = builder.Configuration;

string? connectionString = Configuration.GetConnectionString("IdentityContext");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("No connection string was provided.");
}

builder.AddBlogArrayServices();

builder.AddBlogArrayCacheServices();

builder.AddOpenIdCore(connectionString);

builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    options.AddBlogArrayCookieAuthenticationOptions();
}).AddOpenIdConnect(options =>
{
    options.ClientId = Configuration["OIDC:ClientId"];
    options.ClientSecret = Configuration["OIDC:ClientSecret"];
    options.Authority = Configuration["OIDC:Authority"];
    options.ClaimsIssuer = Configuration["OIDC:Issuer"];

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
            //Adding custom claims to the user�s principal.
            ClaimsIdentity? identity = context?.Principal?.Identity as ClaimsIdentity;

            if (identity != null && !identity.IsAuthenticated)
            {
                context?.HandleResponse();
                context?.Response.Redirect("/error?message=User is not authenticated.");
            }
            else
            {
                string? audience = identity?.FindFirst(Claims.Audience)?.Value;
                string? name = identity?.FindFirst(Claims.Name)?.Value;

                identity?.AddClaim(new Claim(Claims.Audience, audience ?? ""));
                identity?.AddClaim(new Claim(ClaimTypes.GivenName, name ?? ""));
            }
            await Task.CompletedTask;
        },
        OnUserInformationReceived = async context =>
        {
            //Triggered when user information is retrieved from the IdP�s userinfo endpoint.
            //Use Cases:
            //Adding claims from the userinfo response to the user�s identity.
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
            context.Response.Redirect("/error?message=" + context.Exception.Message);
            context.HandleResponse(); // Prevent further processing
            return Task.CompletedTask;
        },
        OnRemoteFailure = context =>
        {
            //Triggered when there is a failure in the remote authentication process(e.g., a network issue or IdP error).
            //Use Cases:
            //Logging errors related to remote authentication.
            //Redirecting users to a fallback page.
            context.Response.Redirect("/error?message=" + context.Failure?.Message);
            context.HandleResponse();
            return Task.CompletedTask;
        }
    };
});

builder.AddIdentityCore();

builder.Services.AddHttpClient();

builder.Services.AddScoped(container =>
{
    return new ClientIpCheckActionFilter(Configuration["IPSafeList"]);
});

WebApplication app = builder.Build();

app.UseSerilogRequestLogging();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.AddBlogArrayApplication(app.Environment.IsDevelopment());

app.Run();
