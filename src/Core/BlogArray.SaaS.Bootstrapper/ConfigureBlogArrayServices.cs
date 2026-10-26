//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Claims;
using AspNetCore.Unobtrusive.Ajax;
using BlogArray.SaaS.Application.Filters;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Constants;
using BlogArray.SaaS.Domain.DTOs;
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Data;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using BlogArray.SaaS.OpenId.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BlogArray.SaaS.Bootstrapper;

public static class ConfigureBlogArrayServices
{
    /// <summary>
    /// How long a confirmed tenant membership is trusted before the next database check.
    /// Unassigning a user also clears the cached entry, so this is only the fallback delay.
    /// </summary>
    private static readonly TimeSpan TenantMembershipCacheDuration = TimeSpan.FromSeconds(60);

    /// <param name="membershipClientId">
    /// For apps that are OIDC clients of the identity server (tenant apps, TenantSuite): resolves
    /// the client id of the current request. When it returns a value, every cookie validation also
    /// checks that the user still holds a valid authorization for that client, so a user removed
    /// from a tenant loses that tenant's session - their identity server (SSO) session and other
    /// tenants are unaffected. Null for the identity server itself.
    /// </param>
    public static CookieAuthenticationOptions AddBlogArrayCookieAuthenticationOptions(this CookieAuthenticationOptions options, Func<HttpContext, string?>? membershipClientId = null)
    {
        options.ExpireTimeSpan = UserSession.InactivityLifetime;
        options.SlidingExpiration = true;

        TimeSpan sessionLifetime = options.ExpireTimeSpan;

        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        // Strict breaks the OIDC redirect-back from the identity server (top-level
        // cross-site navigation). Lax is correct for an auth cookie in an SSO flow.
        options.Cookie.SameSite = SameSiteMode.Lax;

        // Forbidden requests (signed in, but missing a required role) go to the shared
        // ErrorController.AccessDenied page that every app registers. Without this, the cookie
        // handler falls back to /Account/AccessDenied, which no app implements (404).
        options.AccessDeniedPath = "/error/accessdenied";

        options.Events.OnSigningIn = async context =>
        {
            // Per-device session tracking: every app-cookie sign-in is recorded and tagged
            // with a session id claim, enabling the "where you're signed in" list and
            // per-session revocation.
            //
            // Session id semantics (Google-style single session per device):
            //  - When the incoming principal already carries a "session_id" (the Identity
            //    server propagates it in the id_token during SSO), the sign-in ATTACHES to
            //    the existing session row: all suite apps share one session row per device,
            //    so revoking it signs the user out of the whole suite. The id is never
            //    rotated here.
            //  - Otherwise (a fresh local login at the identity server) a new session id is
            //    minted; an existing active row for the same user+user-agent is reused (its
            //    id is rotated) so repeated logins on the same device don't duplicate rows.
            string? userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            OpenIdDbContext dbContext = context.HttpContext.RequestServices.GetRequiredService<OpenIdDbContext>();

            string userAgent = Truncate(context.HttpContext.Request.Headers.UserAgent.ToString(), 512);
            string ipAddress = Truncate(context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", 64);
            UserAgentParser.UserAgentInfo uaInfo = UserAgentParser.Parse(userAgent);
            string deviceName = Truncate(uaInfo.ToString(), 256);
            string? incomingSessionId = context.Principal?.FindFirst("session_id")?.Value;

            UserSession? session = null;
            bool newDevice = false;
            List<UserSession> expiredSessions = [];

            if (!string.IsNullOrEmpty(incomingSessionId))
            {
                session = await dbContext.UserSessions
                    .SingleOrDefaultAsync(tracked => tracked.SessionId == incomingSessionId);

                // Ownership check: never trust an inbound session_id claim without
                // verifying it actually belongs to this user. Don't assume the IdP
                // token is infallible — this is defense in depth, not redundancy.
                if (session is not null && session.UserId != userId)
                {
                    session = null;
                    incomingSessionId = null;
                }
            }

            string sessionId;

            if (session is not null)
            {
                // SSO attach to an existing, verified session row. Id is not rotated.
                sessionId = incomingSessionId!;
            }
            else
            {
                // Fresh local login. The user agent does not identify a device: two computers
                // with the same browser version send the same string, and reusing (and so
                // re-keying) a live row signed the other computer out. Only a row no cookie can
                // still be using - unseen for longer than the sliding cookie lifetime - is
                // recycled for the same user and user agent; otherwise a new row is created.
                DateTime staleBefore = DateTime.UtcNow - sessionLifetime;

                session = await dbContext.UserSessions
                    .Where(tracked => !tracked.Revoked
                        && tracked.ExpiredOn == null
                        && tracked.UserId == userId
                        && tracked.UserAgent == userAgent
                        && tracked.LastSeenOn < staleBefore)
                    .OrderByDescending(tracked => tracked.LastSeenOn)
                    .FirstOrDefaultAsync();

                // A new-sign-in alert, on the identity server only (where users actually sign
                // in; the other apps attach to its session). Checked before this sign-in is saved.
                if (membershipClientId is null)
                {
                    newDevice = await IsUnrecognisedSignInAsync(dbContext, userId, uaInfo, ipAddress);
                }

                if (session is null)
                {
                    session = new UserSession
                    {
                        UserId = userId,
                        CreatedOn = DateTime.UtcNow
                    };

                    dbContext.UserSessions.Add(session);
                }

                sessionId = Guid.NewGuid().ToString();

                // On the identity server, a fresh sign-in also closes the user's other sessions
                // that went unused for longer than the session lifetime: no cookie can still use
                // them, so they are expired (and recorded) instead of lingering as "active".
                if (membershipClientId is null)
                {
                    UserSession current = session;

                    expiredSessions = await dbContext.UserSessions
                        .Where(tracked => !tracked.Revoked
                            && tracked.ExpiredOn == null
                            && tracked.UserId == userId
                            && tracked.LastSeenOn < staleBefore
                            && tracked.Id != current.Id)
                        .OrderBy(tracked => tracked.LastSeenOn)
                        .Take(50)
                        .ToListAsync();

                    foreach (UserSession expired in expiredSessions)
                    {
                        // Expired, not revoked: nobody ended it, it went unused.
                        expired.ExpiredOn = DateTime.UtcNow;
                    }
                }
            }

            session.SessionId = sessionId;
            session.UserId = userId; // re-assert on every write, not just on create
            session.DeviceName = deviceName;
            session.UserAgent = userAgent;
            session.IpAddress = ipAddress;
            session.LastSeenOn = DateTime.UtcNow;

            // (UserSession has no concurrency token, so there is no conflict to retry: the
            // previous DbUpdateConcurrencyException fallback could never run, and would have
            // re-saved the same failing change. Live rows are no longer recycled, so concurrent
            // logins on one device each get their own row.)
            await dbContext.SaveChangesAsync();

            // Recorded after the save, so the audit write never flushes a half-built session row.
            if (expiredSessions.Count > 0)
            {
                IAuditEventLogger auditLogger = context.HttpContext.RequestServices.GetRequiredService<IAuditEventLogger>();

                foreach (UserSession expired in expiredSessions)
                {
                    await auditLogger.LogAsync(new AuditEventRecord(userId, AuditTrigger.System, AuditEventTypes.SessionExpired,
                        TargetUserId: userId,
                        Reason: $"session on '{expired.DeviceName}' expired: unused since {expired.LastSeenOn:yyyy-MM-dd} UTC"));
                }
            }

            if (newDevice && await dbContext.Users.FindAsync(userId) is ApplicationUser signedInUser)
            {
                context.HttpContext.RequestServices.GetRequiredService<ISecurityNotificationService>().NewSignIn(signedInUser);
            }

            foreach (ClaimsIdentity identity in context.Principal!.Identities)
            {
                Claim? existing = identity.FindFirst("session_id");

                if (existing is not null)
                {
                    identity.RemoveClaim(existing);
                }

                identity.AddClaim(new Claim("session_id", sessionId));
            }
        };

        // Chain to the handler already configured instead of replacing it: on the identity
        // server's cookie that is Identity's SecurityStampValidator, which this assignment used
        // to discard - so security stamp changes (password changes, admin revocations,
        // "forget all trusted browsers") never ended other identity server sessions and the
        // principal's roles/claims were never refreshed.
        Func<CookieValidatePrincipalContext, Task> previousValidatePrincipal = options.Events.OnValidatePrincipal;

        options.Events.OnValidatePrincipal = async context =>
        {
            await previousValidatePrincipal(context);

            if (context.Principal is null)
            {
                return;
            }

            await ValidateSessionAsync(context);

            if (context.Principal is null || membershipClientId is null)
            {
                return;
            }

            string? clientId = membershipClientId(context.HttpContext);

            if (!string.IsNullOrEmpty(clientId))
            {
                await ValidateTenantMembershipAsync(context, clientId);
            }
        };

        return options;
    }

    /// <summary>
    /// Enforces per-session revocation and keeps LastSeenOn fresh (throttled to once per minute
    /// per session). Sessions without a tracking claim (issued before this feature) are allowed -
    /// fail-open for legacy cookies.
    /// </summary>
    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        string? sessionId = context.Principal?.FindFirst("session_id")?.Value;

        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        string? userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        OpenIdDbContext dbContext = context.HttpContext.RequestServices.GetRequiredService<OpenIdDbContext>();

        UserSession? session = await dbContext.UserSessions.SingleOrDefaultAsync(tracked => tracked.SessionId == sessionId);

        if (session is null)
        {
            // Claim present but no matching row — treat as invalid rather than
            // fail-open. A dangling claim with no backing row is more likely
            // corruption/tampering than a legitimate legacy case.
            context.RejectPrincipal();
            return;
        }

        if (session.Revoked || session.ExpiredOn != null || session.UserId != userId)
        {
            context.RejectPrincipal();
            return;
        }

        if (DateTime.UtcNow - session.LastSeenOn > TimeSpan.FromMinutes(1))
        {
            session.LastSeenOn = DateTime.UtcNow;
            session.IpAddress = Truncate(context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty, 64);
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Rejects the app's local session when the user no longer holds a valid authorization for
    /// this app's client (they were removed from the tenant). Tenant membership is otherwise only
    /// checked at /connect/authorize, so without this a removed user keeps the app until its
    /// cookie expires. The rejected user is challenged, re-authorizes through the identity server
    /// (no new sign-in: their SSO session is intact) and gets "access denied" for this tenant only.
    /// Confirmed memberships are cached briefly in the distributed cache (not the cookie, so the
    /// cookie is not reissued and its lifetime is not extended).
    /// </summary>
    private static async Task ValidateTenantMembershipAsync(CookieValidatePrincipalContext context, string clientId)
    {
        string? userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        IDistributedCache cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
        string cacheKey = CacheKeyHelper.GetTenantMembershipKey(clientId, userId);

        if (await cache.GetAsync(cacheKey) is not null)
        {
            return;
        }

        OpenIdDbContext dbContext = context.HttpContext.RequestServices.GetRequiredService<OpenIdDbContext>();

        bool isMember = await dbContext.Authorizations.AnyAsync(a =>
            a.Subject == userId
            && a.Application!.ClientId == clientId
            && a.Status == OpenIddict.Abstractions.OpenIddictConstants.Statuses.Valid);

        if (!isMember)
        {
            context.RejectPrincipal();
            return;
        }

        await cache.SetAsync(cacheKey, [1], new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TenantMembershipCacheDuration
        });
    }

    /// <summary>
    /// How far back a sign-in's device and IP address are compared when deciding whether to send
    /// the "new sign-in" alert.
    /// </summary>
    private static readonly TimeSpan NewSignInLookback = TimeSpan.FromDays(30);

    /// <summary>
    /// True when this sign-in should alert the owner: its browser/OS pair, or its IP address, was
    /// not seen on the account in the last <see cref="NewSignInLookback"/> - including an account
    /// with no recent history at all (its first sign-in). Versions are ignored, or every browser
    /// update would look like a new device. History comes from both the successful sign-in events
    /// and the recent session rows (whose IP the other apps keep current), so clearing one alone
    /// does not hide or invent a device.
    /// </summary>
    private static async Task<bool> IsUnrecognisedSignInAsync(OpenIdDbContext dbContext, string userId, UserAgentParser.UserAgentInfo uaInfo, string ipAddress)
    {
        DateTime since = DateTime.UtcNow - NewSignInLookback;
        string success = SignInResultType.Success.ToString();

        var signIns = await dbContext.SignInEvents
            .Where(signIn => signIn.UserId == userId && signIn.Result == success && signIn.CreatedOn >= since)
            .Select(signIn => new { signIn.UserAgent, signIn.IpAddress })
            .Distinct()
            .ToListAsync();

        var sessions = await dbContext.UserSessions
            .Where(tracked => tracked.UserId == userId && tracked.LastSeenOn >= since)
            .Select(tracked => new { UserAgent = (string?)tracked.UserAgent, IpAddress = (string?)tracked.IpAddress })
            .Distinct()
            .ToListAsync();

        var recent = signIns.Concat(sessions).ToList();

        bool knownDevice = recent.Any(seen => SameDeviceFamily(UserAgentParser.Parse(seen.UserAgent ?? string.Empty), uaInfo));
        bool knownAddress = !string.IsNullOrEmpty(ipAddress)
            && recent.Any(seen => string.Equals(seen.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase));

        return !knownDevice || !knownAddress;
    }

    /// <summary>
    /// Same browser and operating system, whatever their versions.
    /// </summary>
    private static bool SameDeviceFamily(UserAgentParser.UserAgentInfo a, UserAgentParser.UserAgentInfo b)
    {
        return string.Equals(a.Browser, b.Browser, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.OS, b.OS, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] SplitList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public static IHostApplicationBuilder AddBlogArrayServices(this IHostApplicationBuilder builder)
    {
        bool isDevelopment = builder.Environment.IsDevelopment();

        builder.Services.AddUnobtrusiveAjax();

        builder.Services.AddHttpContextAccessor();

        builder.Services.ConfigureOptions<ConfigureSecurityStampOptions>();

        // Razor runtime compilation is a development convenience only and is disabled in
        // production to reduce the attack surface.
        IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(BlogArray.SaaS.Web.Controllers.BaseController).Assembly);

        if (isDevelopment)
        {
            mvcBuilder.AddRazorRuntimeCompilation();
        }

        IMvcBuilder razorPagesBuilder = builder.Services.AddRazorPages()
            .AddApplicationPart(typeof(BlogArray.SaaS.Web.Controllers.BaseController).Assembly);

        if (isDevelopment)
        {
            razorPagesBuilder.AddRazorRuntimeCompilation();
        }

        // Automatically validate antiforgery tokens for all unsafe HTTP methods (POST, PUT,
        // PATCH, DELETE). Actions that legitimately cannot carry a token are already marked
        // with [IgnoreAntiforgeryToken].
        mvcBuilder.AddMvcOptions(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

        builder.Services.AddRouting(options => options.LowercaseUrls = true);

        BlogArrayConstants.DefaultLogoUrl = builder.Configuration.GetValue<string>("Defaults:DefaultLogoUrl");
        BlogArrayConstants.DefaultFaviconUrl = builder.Configuration.GetValue<string>("Defaults:DefaultFaviconUrl");

        // Behind a reverse proxy / load balancer the connection's address and scheme are the
        // proxy's: the client IP (rate limits, IP allow-lists, session and security logs) and
        // the scheme (SAML ACS URL, redirects) come from X-Forwarded-For / X-Forwarded-Proto,
        // trusted only from the proxies listed in ForwardedHeaders:KnownProxies (IP addresses)
        // and ForwardedHeaders:KnownNetworks (CIDR), semicolon-separated. With neither set only
        // loopback is trusted, so the headers cannot be spoofed by clients.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (string proxy in SplitList(builder.Configuration["ForwardedHeaders:KnownProxies"]))
            {
                options.KnownProxies.Add(System.Net.IPAddress.TryParse(proxy, out System.Net.IPAddress? address)
                    ? address
                    : throw new InvalidOperationException($"ForwardedHeaders:KnownProxies contains an invalid IP address: '{proxy}'."));
            }

            foreach (string network in SplitList(builder.Configuration["ForwardedHeaders:KnownNetworks"]))
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.TryParse(network, out System.Net.IPNetwork parsed)
                    ? parsed
                    : throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks contains an invalid CIDR network: '{network}'."));
            }
        });

        builder.Services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
            options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
            options.Secure = CookieSecurePolicy.Always;
        });

        builder.Services.AddSingleton<IEmailTemplate, EmailTemplate>();

        // Shared DataProtection key ring: every app in the suite (Identity, TenantSuite, App)
        // uses the same application name and persisted key ring so payloads protected by one
        // app (e.g. tenant API keys) can be unprotected by another. DataProtection:Mode picks
        // the persistence location:
        //   Local         - the master database (DataProtectionKeys table): the ring is backed
        //                   up with the DB and survives machine loss.
        //   AzureKeyVault - Azure App Service / multi-instance: persists to the blob at
        //                   DataProtection:BlobUri (SAS URI or managed identity) and optionally
        //                   encrypts the ring at rest with DataProtection:KeyVaultKeyId.
        IDataProtectionBuilder dataProtection = builder.Services.AddDataProtection();

        string? mode = builder.Configuration["DataProtection:Mode"];
        string? blobUri = builder.Configuration["DataProtection:BlobUri"];
        string? keyVaultKeyId = builder.Configuration["DataProtection:KeyVaultKeyId"];

        if (string.Equals(mode, "AzureKeyVault", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(blobUri))
            {
                throw new InvalidOperationException(
                    "DataProtection:Mode is AzureKeyVault but DataProtection:BlobUri is not set. Set it to a blob URI (with SAS token, or plain for managed identity).");
            }

            Uri blobKeyUri = new(blobUri);

            // A URI carrying a SAS token authenticates itself; a plain blob URI is resolved
            // with managed identity (DefaultAzureCredential).
            if (!string.IsNullOrEmpty(blobKeyUri.Query))
            {
                dataProtection.PersistKeysToAzureBlobStorage(blobKeyUri);
            }
            else
            {
                dataProtection.PersistKeysToAzureBlobStorage(blobKeyUri, new Azure.Identity.DefaultAzureCredential());
            }

            if (!string.IsNullOrEmpty(keyVaultKeyId))
            {
                dataProtection.ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyId), new Azure.Identity.DefaultAzureCredential());
            }
        }
        else
        {
            // Local mode: the ring lives in the master database, shared by all three apps and
            // backed up with the regular database backups. The DataProtectionKeys table is
            // created by the AddDataProtectionKeys migration (EnsureCreated covers new DBs).
            dataProtection.PersistKeysToDbContext<OpenIdDbContext>();
        }

        // Key lifetime: how long a generated key protects new payloads before DP rolls to a
        // fresh one. Expiration never affects decryption - expired keys are retained in the
        // ring indefinitely, so already-encrypted payloads keep unprotecting. Default is 90
        // days; DataProtection:KeyLifetimeDays overrides it when set.
        int? keyLifetimeDays = builder.Configuration.GetValue<int?>("DataProtection:KeyLifetimeDays");

        if (keyLifetimeDays is > 0)
        {
            dataProtection.SetDefaultKeyLifetime(TimeSpan.FromDays(keyLifetimeDays.Value));
        }

        dataProtection.SetApplicationName("BlogArray.SaaS");

        builder.Services.AddSingleton<IDataProtector>(services =>
            services.GetRequiredService<IDataProtectionProvider>().CreateProtector("BlogArray.TenantApiKeys"));
        builder.Services.AddSingleton<ISmtpEmailSender, EmailHelper>();
        builder.Services.AddSingleton<IEmailHelper>(services => services.GetRequiredService<ISmtpEmailSender>());
        builder.Services.AddSingleton<IAzureStorageService, AzureStorageService>();
        builder.Services.AddSingleton<ICacheService, CacheService>();

        builder.Services.AddScoped<ITenantPersonnelService, TenantPersonnelService>();
        builder.Services.AddScoped<IDbConnectionFactory, SqlDbConnectionFactory>();
        builder.Services.AddScoped<ITenantManagementService, TenantManagementService>();
        builder.Services.AddScoped<IUserManagementService, UserManagementService>();
        builder.Services.AddScoped<IUserAccessRevocationService, UserAccessRevocationService>();
        builder.Services.AddScoped<IStepUpService, StepUpService>();
        builder.Services.AddScoped<ISecurityNotificationService, SecurityNotificationService>();
        builder.Services.AddScoped<ITenantAccessNotifier, TenantAccessNotifier>();
        builder.Services.AddScoped<IAdminAlertService, AdminAlertService>();
        builder.Services.AddScoped<ApiKeyAuthorizationFilter>();

        // CORS is restricted to the origins listed in the optional "Cors:AllowedOrigins"
        // configuration key (semicolon-separated). When it is not configured, no cross-origin
        // request is allowed, which is the correct default for same-site MVC applications.
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowedOrigins", policy =>
            {
                string? configuredOrigins = builder.Configuration.GetValue<string>("Cors:AllowedOrigins");

                if (string.IsNullOrWhiteSpace(configuredOrigins))
                {
                    // No origins allowed.
                    return;
                }

                foreach (string origin in configuredOrigins.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    policy = policy.WithOrigins(origin);
                }

                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });

        // Rate limiting for authentication- and mail-related endpoints. Policies are applied
        // via [EnableRateLimiting] attributes; the middleware itself is added in
        // AddBlogArrayApplication.
        // Credential checks and email sends are limited per IP; page loads are not (see
        // RateLimitPolicies). RateLimits:* overrides the per-minute limits.
        int authPerMinute = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 20);
        int emailPerMinute = builder.Configuration.GetValue("RateLimits:EmailPerMinute", 5);
        int apiPerMinute = builder.Configuration.GetValue("RateLimits:ApiPerMinute", 60);

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth", httpContext => RateLimitPolicies.PostsPerIp(httpContext, authPerMinute));
            options.AddPolicy("email", httpContext => RateLimitPolicies.PostsPerIp(httpContext, emailPerMinute));
            options.AddPolicy("api", httpContext => RateLimitPolicies.PerIp(httpContext, apiPerMinute));
        });

        builder.Services.Configure<SmtpConfiguration>(builder.Configuration.GetSection("SMTP"));

        return builder;
    }

    /// <summary>
    /// Sends email from a background queue instead of during the request (see
    /// <see cref="BackgroundEmailHelper"/>). For apps whose callers don't need to know at once
    /// whether a send succeeded: the identity server. TenantSuite keeps synchronous sending so
    /// its dialogs can report which recipients failed.
    /// </summary>
    public static IHostApplicationBuilder AddBackgroundEmailDelivery(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<EmailQueue>();
        builder.Services.AddSingleton<IEmailHelper, BackgroundEmailHelper>();
        builder.Services.AddHostedService<EmailDispatcherService>();

        return builder;
    }

    public static IHostApplicationBuilder AddBlogArrayCacheServices(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<CacheConfiguration>(builder.Configuration.GetSection("Cache"));

        string? cacheType = builder.Configuration.GetValue("Cache:Type", "SqlServer");
        string? connectionString = builder.Configuration.GetValue<string>("Cache:ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("No cache connection string was provided.");
        }

        if (cacheType == "Redis")
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString;
            });
        }
        else if (cacheType == "SqlServer")
        {
            builder.Services.AddDistributedSqlServerCache(options =>
            {
                options.ConnectionString = connectionString;
                options.SchemaName = "dbo";
                options.TableName = "BlogArray";
            });
        }
        else
        {
            throw new InvalidOperationException("Invalid cache type specified.");
        }

        return builder;
    }
}
