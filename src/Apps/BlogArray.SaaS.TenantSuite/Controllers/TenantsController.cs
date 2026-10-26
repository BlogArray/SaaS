//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Domain.Helpers;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using P.Pager;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class TenantsController(OpenIdDbContext context,
    OpenIddictApplicationManager<OpenIdApplication> manager,
    ITenantManagementService tenantManagementService,
    IAzureStorageService azureStorage,
    ICacheService cacheService,
    ITenantPersonnelService personnelService,
    IDataProtector protector,
    IConfiguration configuration,
    IEmailTemplate emailTemplate,
    IAuditEventLogger auditLogger,
    ITenantAccessNotifier tenantAccessNotifier,
    ITenantInviteService inviteService,
    IAdminAlertService adminAlerts,
    ILogger<TenantsController> logger) : BaseController
{
    private readonly int _apiKeyPrefixLength = configuration.GetValue("ApiKey:PrefixLength", 8);
    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        take = ClampPageSize(take);
        page = ClampPage(page);

        ViewBag.SearchTerm = term;
        ViewBag.Take = take;

        IQueryable<OpenIdApplication> applications = context.Applications;

        if (!string.IsNullOrEmpty(term))
        {
            applications = applications.Where(a => a.DisplayName.Contains(term) || a.ClientId.Contains(term));
        }

        IQueryable<ApplicationListViewModel> filteredApps = applications
            .OrderBy(a => a.DisplayName).Select(a => new ApplicationListViewModel
            {
                Id = a.Id,
                ClientId = a.ClientId,
                DisplayName = a.DisplayName,
                Icon = a.Theme.Favicon,
                Description = a.Description
            });

        return View(await filteredApps.ToPagerListAsync(page, take));
    }

    #region Create
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApplicationViewModel openIdApplication)
    {
        if (!ModelState.IsValid)
        {
            return View(openIdApplication);
        }

        // The client id is also the tenant's subdomain. Checked here rather than on the view
        // model so existing tenants with older ids can still be edited.
        openIdApplication.ClientId = openIdApplication.ClientId?.Trim() ?? string.Empty;

        if (!TenantInputValidator.IsValidClientId(openIdApplication.ClientId))
        {
            ModelState.AddModelError("ClientId", "Use lowercase letters, digits and hyphens (1-63 characters, not starting or ending with a hyphen): the client id becomes the tenant's subdomain.");
            return View(openIdApplication);
        }

        if (await manager.FindByClientIdAsync(openIdApplication.ClientId) is not null)
        {
            ModelState.AddModelError("ClientId", "A tenant with this client id already exists.");
            return View(openIdApplication);
        }

        if (!TryValidateRedirectUris(openIdApplication, out List<string> redirectUris, out List<string> postLogoutRedirectUris))
        {
            return View(openIdApplication);
        }

        if (!string.IsNullOrEmpty(openIdApplication.ConnectionString))
        {
            bool valid = await personnelService.TestConnectionAsync(openIdApplication.ConnectionString);

            if (!valid)
            {
                ModelState.AddModelError("ConnectionString", "Unable to connect to database, please check the SQL connection string.");
                return View(openIdApplication);
            }
        }

        if (!TrySerializeEmails(openIdApplication.AdminEmail, out string adminEmail, out string emailError))
        {
            ModelState.AddModelError("AdminEmail", emailError);
            return View(openIdApplication);
        }

        OpenIdApplication? entity = new();

        // Secrets are finalized server-side: any missing or too-short value is replaced with a
        // cryptographically random one so weak or predictable client-side generation can never
        // become a tenant credential.
        openIdApplication.ClientSecret = FinalizeSecret(openIdApplication.ClientSecret);
        openIdApplication.APIKey = FinalizeSecret(openIdApplication.APIKey);

        MapProperties(openIdApplication, entity);

        entity.AdminEmail = adminEmail;
        entity.RedirectUris = JsonSerializer.Serialize(redirectUris);
        entity.PostLogoutRedirectUris = postLogoutRedirectUris.Count > 0 ? JsonSerializer.Serialize(postLogoutRedirectUris) : null;

        // Secrets exist in plaintext only for this request (shown once in the browser and
        // emailed); storage keeps the API key hash and DataProtection-protected copies.
        SetApiKey(entity, openIdApplication.APIKey!);
        SetClientSecret(entity, openIdApplication.ClientSecret!);
        SetConnectionString(entity, openIdApplication.ConnectionString);

        try
        {
            await manager.CreateAsync(entity, openIdApplication.ClientSecret);
        }
        catch (OpenIddictExceptions.ValidationException ex)
        {
            // OpenIddict's own checks (e.g. a client id created concurrently): show them on the
            // form instead of failing the request.
            ModelState.AddModelError(string.Empty, DescribeValidationErrors(ex));
            return View(openIdApplication);
        }

        await InvalidateTenantCacheAsync(entity);

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.TenantCreated, ClientId: entity.ClientId, Reason: entity.DisplayName));

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.ApiKeyCreated, ClientId: entity.ClientId, Reason: entity.DisplayName));

        // Deliver the credentials by email, but never let a mail failure fail the creation:
        // the secrets are also shown once in the browser so the admin can hand them over.
        List<string> recipients = DeserializeEmailList(DeserializeEmails(entity.AdminEmail));

        List<string> failures = SendEach(recipients, email => emailTemplate.TenantWelcome(email, entity.DisplayName, entity.TenantUrl ?? string.Empty, openIdApplication.ClientSecret!, openIdApplication.APIKey!));

        // The one-time secrets are rendered by this response only: never carried to another
        // request in TempData (a browser cookie), and never cached.
        TenantCreatedViewModel created = new()
        {
            Id = entity.Id!,
            Name = entity.DisplayName ?? entity.ClientId!,
            ClientSecret = openIdApplication.ClientSecret!,
            ApiKey = openIdApplication.APIKey!,
            EmailError = failures.Count != 0
                ? $"The credentials could not be emailed to: {string.Join(", ", failures)}. Copy them now - they will not be shown again."
                : null
        };

        // Platform-level change: every other Superuser hears about it.
        await adminAlerts.NotifySuperusersAsync("new tenant created",
            $"A new tenant, {entity.DisplayName} ({entity.ClientId}), was created.",
            [$"Tenant URL: {entity.TenantUrl ?? "-"}", $"Admin emails: {string.Join(", ", recipients)}"],
            LoggedInUserID);

        Response.Headers.CacheControl = "no-store";

        return View("Created", created);
    }

    #endregion Create

    #region Detais/edit

    public async Task<IActionResult> Details(string id)
    {
        if (id == null)
        {
            return NotFound();
        }
        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication is null
            ? NotFound()
            : View(new ApplicationListViewModel
            {
                Id = id,
                ClientId = openIdApplication.ClientId,
                DisplayName = openIdApplication.DisplayName,
                Icon = openIdApplication.Theme.Favicon,
                Description = openIdApplication.Description,
            });
    }

    public async Task<IActionResult> Toolbar(string id)
    {
        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        return PartialView("_TenantToolbar", new TenantToolbar
        {
            Id = id,
            UsersCount = await context.Authorizations.CountAsync(a => a.Application.Id == id),
            PendingInvitationsCount = await context.TenantInvitations.CountAsync(i => i.ApplicationId == id && i.Status == TenantInvitationStatus.Pending),
            HasApiKey = !string.IsNullOrEmpty(openIdApplication.APIKeyHash),
            IsTenantSuite = IsTenantSuite(openIdApplication)
        });
    }

    public async Task<IActionResult> BasicInfo(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);
        return openIdApplication == null ? NotFound() : PartialView("_BasicInfo", ToEdit(openIdApplication));
    }

    public async Task<IActionResult> EditBasicInfo(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);
        return openIdApplication == null ? NotFound() : PartialView("_EditBasicInfo", ToEdit(openIdApplication));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBasicInfo(EditApplicationViewModel openIdApplication)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        if (!string.IsNullOrEmpty(openIdApplication.ConnectionString))
        {
            bool valid = await personnelService.TestConnectionAsync(openIdApplication.ConnectionString);

            if (!valid)
            {
                ModelState.AddModelError("ConnectionString", "Unable to connect to database, please check the SQL connection string.");
                return ModelStateError(ModelState);
            }
        }

        OpenIdApplication? entity = await manager.FindByIdAsync(openIdApplication.Id);

        if (entity is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        if (!TrySerializeEmails(openIdApplication.AdminEmail, out string adminEmail, out string emailError))
        {
            ModelState.AddModelError("AdminEmail", emailError);
            return ModelStateError(ModelState);
        }

        if (!TryValidateRedirectUris(openIdApplication, out List<string> redirectUris, out List<string> postLogoutRedirectUris))
        {
            return ModelStateError(ModelState);
        }

        // TenantSuite's sign-in callbacks are fixed by its deployment: changing them here would
        // break signing in to TenantSuite itself.
        if (IsTenantSuite(entity)
            && (!SameUris(redirectUris, entity.RedirectUris)
                || !SameUris(postLogoutRedirectUris, entity.PostLogoutRedirectUris)))
        {
            return JsonError("TenantSuite's own redirect URIs can't be changed here: a wrong value would lock every administrator out of TenantSuite.");
        }

        var infoBefore = new { entity.DisplayName, entity.Legalname, entity.Website, entity.Description, entity.TenantUrl };
        List<string> previousAdmins = DeserializeEmailList(DeserializeEmails(entity.AdminEmail));

        MapProperties(openIdApplication, entity);

        entity.AdminEmail = adminEmail;
        entity.RedirectUris = JsonSerializer.Serialize(redirectUris);
        entity.PostLogoutRedirectUris = postLogoutRedirectUris.Count > 0 ? JsonSerializer.Serialize(postLogoutRedirectUris) : null;

        try
        {
            await manager.UpdateAsync(entity);
        }
        catch (OpenIddictExceptions.ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, DescribeValidationErrors(ex));
            return ModelStateError(ModelState);
        }

        var infoAfter = new { entity.DisplayName, entity.Legalname, entity.Website, entity.Description, entity.TenantUrl };
        (string? infoOldValueJson, string? infoNewValueJson) = AuditDiff.Changed(infoBefore, infoAfter);

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.TenantSettingsChanged, ClientId: entity.ClientId, Reason: "basic information updated", OldValueJson: infoOldValueJson, NewValueJson: infoNewValueJson));

        await InvalidateTenantCacheAsync(entity);

        // Rotated credentials are emailed to the admin list: when it changes, the previous
        // admins are told, so the list can't be silently redirected to someone else.
        List<string> currentAdmins = DeserializeEmailList(DeserializeEmails(entity.AdminEmail));

        if (!new HashSet<string>(previousAdmins, StringComparer.OrdinalIgnoreCase).SetEquals(currentAdmins))
        {
            await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.TenantSettingsChanged, ClientId: entity.ClientId, Reason: "admin emails changed",
                OldValueJson: System.Text.Json.JsonSerializer.Serialize(new { AdminEmail = previousAdmins }),
                NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { AdminEmail = currentAdmins })));

            adminAlerts.NotifyAddresses(previousAdmins,
                $"admin emails changed for {entity.DisplayName}",
                $"The administrator email addresses of tenant {entity.DisplayName} ({entity.ClientId}) were changed. New client secrets and API keys are sent to these addresses.",
                [$"Previous: {string.Join(", ", previousAdmins)}", $"New: {string.Join(", ", currentAdmins)}"]);
        }

        return JsonSuccess("Tenant information updated successfuly");
    }

    public async Task<IActionResult> Theme(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication == null ? NotFound() : PartialView("_Theme", new ThemeViewModel
        {
            Id = openIdApplication.Id,
            Logo = openIdApplication.Theme.Logo,
            Favicon = openIdApplication.Theme.Favicon,
            NavbarColor = openIdApplication.Theme.NavbarColor,
            NavbarTextAndIconColor = openIdApplication.Theme.NavbarTextAndIconColor,
            PrimaryColor = openIdApplication.Theme.PrimaryColor
        });
    }

    public async Task<IActionResult> EditTheme(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication == null ? NotFound() : PartialView("_EditTheme", new ThemeViewModel
        {
            Id = openIdApplication.Id,
            Logo = openIdApplication.Theme.Logo,
            Favicon = openIdApplication.Theme.Favicon,
            NavbarColor = openIdApplication.Theme.NavbarColor,
            NavbarTextAndIconColor = openIdApplication.Theme.NavbarTextAndIconColor,
            PrimaryColor = openIdApplication.Theme.PrimaryColor
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTheme(ThemeViewModel themeViewModel)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? entity = await manager.FindByIdAsync(themeViewModel.Id);

        if (entity is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        // Theme colors are written into the tenant app's inline styles: only hex colors are
        // accepted (empty means "use the default").
        foreach ((string field, string? color) in new[]
                 {
                     (nameof(themeViewModel.NavbarColor), themeViewModel.NavbarColor),
                     (nameof(themeViewModel.NavbarTextAndIconColor), themeViewModel.NavbarTextAndIconColor),
                     (nameof(themeViewModel.PrimaryColor), themeViewModel.PrimaryColor)
                 })
        {
            if (!string.IsNullOrEmpty(color) && !TenantInputValidator.IsValidHexColor(color))
            {
                ModelState.AddModelError(field, "Colors must be hex values such as #1a73e8.");
            }
        }

        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        var themeBefore = new { entity.Theme.NavbarColor, entity.Theme.NavbarTextAndIconColor, entity.Theme.PrimaryColor };

        entity.Theme.NavbarColor = themeViewModel.NavbarColor;
        entity.Theme.NavbarTextAndIconColor = themeViewModel.NavbarTextAndIconColor;
        entity.Theme.PrimaryColor = themeViewModel.PrimaryColor;

        await manager.UpdateAsync(entity);

        await InvalidateTenantCacheAsync(entity);

        var themeAfter = new { entity.Theme.NavbarColor, entity.Theme.NavbarTextAndIconColor, entity.Theme.PrimaryColor };
        (string? themeOldValueJson, string? themeNewValueJson) = AuditDiff.Changed(themeBefore, themeAfter);

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.TenantSettingsChanged, ClientId: entity.ClientId, Reason: "theme updated", OldValueJson: themeOldValueJson, NewValueJson: themeNewValueJson));

        return JsonSuccess("Tenant theme information updated successfuly");
    }

    public async Task<IActionResult> Security(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication == null ? NotFound() : PartialView("_Security", new TenantSecurityViewModel
        {
            Id = openIdApplication.Id,
            IsSocialAuthEnabled = openIdApplication.Security.IsSocialAuthEnabled,
            IsMfaEnforced = openIdApplication.Security.IsMfaEnforced,
            IsSsoEnabled = openIdApplication.Security.IsSsoEnabled,
            SsoEntityId = openIdApplication.Security.SsoEntityId,
            SsoSignInUrl = openIdApplication.Security.SsoSignInUrl,
            SsoSignOutUrl = openIdApplication.Security.SsoSignOutUrl,
            SsoX509Certificate = openIdApplication.Security.SsoX509Certificate,
            IsSingleSignOutEnabled = openIdApplication.Security.IsSingleSignOutEnabled
        });
    }

    public async Task<IActionResult> EditSecurity(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication == null ? NotFound() : PartialView("_EditSecurity", new TenantSecurityViewModel
        {
            Id = openIdApplication.Id,
            IsSocialAuthEnabled = openIdApplication.Security.IsSocialAuthEnabled,
            IsMfaEnforced = openIdApplication.Security.IsMfaEnforced,
            IsSsoEnabled = openIdApplication.Security.IsSsoEnabled,
            SsoEntityId = openIdApplication.Security.SsoEntityId,
            SsoSignInUrl = openIdApplication.Security.SsoSignInUrl,
            SsoSignOutUrl = openIdApplication.Security.SsoSignOutUrl,
            SsoX509Certificate = openIdApplication.Security.SsoX509Certificate,
            IsSingleSignOutEnabled = openIdApplication.Security.IsSingleSignOutEnabled
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSecurity(TenantSecurityViewModel securityViewModel)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? entity = await manager.FindByIdAsync(securityViewModel.Id);

        if (entity is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        // An unreadable certificate would otherwise only surface as a failure at SAML sign-in.
        if (securityViewModel.IsSsoEnabled && !TenantInputValidator.IsValidX509Certificate(securityViewModel.SsoX509Certificate))
        {
            ModelState.AddModelError(nameof(securityViewModel.SsoX509Certificate), "The certificate isn't a valid Base-64 or PEM encoded X.509 certificate.");
            return ModelStateError(ModelState);
        }

        if (securityViewModel.IsSsoEnabled)
        {
            // When SAML SSO is enabled it takes precedence over the local social-login
            // (the UI disables those switches in this state, and disabled
            // checkboxes are not submitted at all).
            securityViewModel.IsSocialAuthEnabled = false;
        }

        var securityBefore = new { entity.Security.IsSocialAuthEnabled, entity.Security.IsMfaEnforced, entity.Security.IsSsoEnabled, entity.Security.SsoSignInUrl, entity.Security.SsoSignOutUrl, entity.Security.SsoX509Certificate, entity.Security.SsoEntityId, entity.Security.IsSingleSignOutEnabled };

        entity.Security.IsSocialAuthEnabled = securityViewModel.IsSocialAuthEnabled;
        entity.Security.IsMfaEnforced = securityViewModel.IsMfaEnforced;
        entity.Security.IsSsoEnabled = securityViewModel.IsSsoEnabled;
        entity.Security.SsoSignInUrl = securityViewModel.SsoSignInUrl;
        entity.Security.SsoSignOutUrl = securityViewModel.SsoSignOutUrl;
        entity.Security.SsoX509Certificate = securityViewModel.SsoX509Certificate;
        entity.Security.SsoEntityId = securityViewModel.SsoEntityId;
        entity.Security.IsSingleSignOutEnabled = securityViewModel.IsSingleSignOutEnabled;

        await manager.UpdateAsync(entity);

        var securityAfter = new { entity.Security.IsSocialAuthEnabled, entity.Security.IsMfaEnforced, entity.Security.IsSsoEnabled, entity.Security.SsoSignInUrl, entity.Security.SsoSignOutUrl, entity.Security.SsoX509Certificate, entity.Security.SsoEntityId, entity.Security.IsSingleSignOutEnabled };
        (string? securityOldValueJson, string? securityNewValueJson) = AuditDiff.Changed(securityBefore, securityAfter);

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.TenantSettingsChanged, ClientId: entity.ClientId, Reason: "security settings updated", OldValueJson: securityOldValueJson, NewValueJson: securityNewValueJson));

        await InvalidateTenantCacheAsync(entity);

        // How the tenant's users sign in changed (e.g. MFA turned off, SSO pointed at another
        // identity provider): the tenant's admins are told, so a malicious or mistaken change
        // can't go unnoticed. The certificate itself is not included, only that it changed.
        List<string> securityChanges = DescribeSecurityChanges(securityBefore.IsMfaEnforced, securityAfter.IsMfaEnforced, "Multi-factor authentication enforced")
            .Concat(DescribeSecurityChanges(securityBefore.IsSsoEnabled, securityAfter.IsSsoEnabled, "SAML single sign-on"))
            .Concat(DescribeSecurityChanges(securityBefore.IsSocialAuthEnabled, securityAfter.IsSocialAuthEnabled, "Social login"))
            .Concat(DescribeSecurityChanges(securityBefore.IsSingleSignOutEnabled, securityAfter.IsSingleSignOutEnabled, "Single sign-out"))
            .ToList();

        if (securityBefore.SsoSignInUrl != securityAfter.SsoSignInUrl)
        {
            securityChanges.Add($"SAML sign-in URL changed to {securityAfter.SsoSignInUrl ?? "(none)"}");
        }

        if (securityBefore.SsoSignOutUrl != securityAfter.SsoSignOutUrl)
        {
            securityChanges.Add($"SAML sign-out URL changed to {securityAfter.SsoSignOutUrl ?? "(none)"}");
        }

        if (securityBefore.SsoEntityId != securityAfter.SsoEntityId)
        {
            securityChanges.Add($"SAML entity ID changed to {securityAfter.SsoEntityId ?? "(none)"}");
        }

        if (securityBefore.SsoX509Certificate != securityAfter.SsoX509Certificate)
        {
            securityChanges.Add("SAML signing certificate replaced");
        }

        if (securityChanges.Count > 0)
        {
            adminAlerts.NotifyAddresses(DeserializeEmailList(DeserializeEmails(entity.AdminEmail)),
                $"sign-in settings changed for {entity.DisplayName}",
                $"The sign-in security settings of your tenant {entity.DisplayName} ({entity.ClientId}) were changed:",
                securityChanges);
        }

        return JsonSuccess("Tenant security information updated successfuly");
    }

    #endregion Detais/edit

    #region Actions

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(5242880)]
    public async Task<IActionResult> UpdateImage(string id, string type)
    {
        if (id == null)
        {
            return NotFound();
        }

        // Only the tenant's logo and favicon can be changed here; anything else used to
        // overwrite the favicon.
        if (type is not ("logo" or "favicon"))
        {
            return JsonError("Unknown image type.");
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        IFormFile? file = Request.Form.Files.FirstOrDefault();

        if (file == null)
        {
            return StatusCode(400, "No File is selected.");
        }

        //ToDo delete existing image
        ReturnResult<string> result = await azureStorage.Upload(file, type, true);

        if (!result.Status)
        {
            return JsonError("An error occurred while uploading the file.");
        }

        string? previousImage = type == "logo" ? openIdApplication.Theme.Logo : openIdApplication.Theme.Favicon;

        if (type == "logo")
        {
            openIdApplication.Theme.Logo = result.Result;
        }
        else
        {
            openIdApplication.Theme.Favicon = result.Result;
        }

        await manager.UpdateAsync(openIdApplication);

        await InvalidateTenantCacheAsync(openIdApplication);

        // Audited like every other tenant setting change.
        (string? imageOldValueJson, string? imageNewValueJson) = type == "logo"
            ? AuditDiff.Changed(new { Logo = previousImage }, new { Logo = result.Result })
            : AuditDiff.Changed(new { Favicon = previousImage }, new { Favicon = result.Result });

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.TenantSettingsChanged, ClientId: openIdApplication.ClientId, Reason: $"{type} updated", OldValueJson: imageOldValueJson, NewValueJson: imageNewValueJson));

        return result.Status ? JsonSuccess(result.Result) : JsonError("An error occurred while saving your information.");
    }

    [HttpGet]
    /// <summary>
    /// The rotation prompt (recipients for the new credential) for the API key or, with
    /// type "secret", the client secret.
    /// </summary>
    public async Task<IActionResult> RotateApiKeys(string id, string type = "apikey")
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        if (type is not ("secret" or "apikey"))
        {
            return JsonError("Unknown key type.");
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        // TenantSuite signs in with the client secret from its own configuration
        // (OIDC:ClientSecret): rotating it here would lock every administrator out of
        // TenantSuite until the configuration is updated.
        if (type == "secret" && IsTenantSuite(openIdApplication))
        {
            return JsonError("TenantSuite's own client secret is managed in its configuration (OIDC:ClientSecret) and can't be rotated here.");
        }

        return PartialView("_RotateApiKeysPrompt", new RotateApiKeysPromptViewModel
        {
            ApplicationId = openIdApplication.Id,
            Type = type,
            Name = openIdApplication.DisplayName,
            Emails = DeserializeEmails(openIdApplication.AdminEmail)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateKeys(string id, string type, string? emails)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type))
        {
            return NotFound();
        }

        // Unknown types would show a "new key" that was never saved.
        if (type is not ("secret" or "apikey"))
        {
            return JsonError("Unknown key type.");
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        // TenantSuite signs in with the client secret from its own configuration
        // (OIDC:ClientSecret): rotating it here would lock every administrator out of
        // TenantSuite until the configuration is updated.
        if (type == "secret" && IsTenantSuite(openIdApplication))
        {
            return JsonError("TenantSuite's own client secret is managed in its configuration (OIDC:ClientSecret) and can't be rotated here.");
        }

        RotateKeysViewModel rotateKeys = new()
        {
            ApplicationId = id,
            Name = openIdApplication.DisplayName,
            Key = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            Type = type
        };

        if (type == "secret")
        {
            // Hash for OpenIddict (one-way), protected copy for the tenant's OIDC handshake;
            // the plaintext is shown once in the response and never stored.
            SetClientSecret(openIdApplication, rotateKeys.Key);

            await manager.UpdateAsync(openIdApplication, rotateKeys.Key);

            await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.ClientSecretRotated, ClientId: openIdApplication.ClientId, Reason: openIdApplication.DisplayName));
        }
        else
        {
            // Hash-on-write: the plaintext key is shown once in the response and never stored.
            SetApiKey(openIdApplication, rotateKeys.Key);
            await manager.UpdateAsync(openIdApplication);

            await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.ApiKeyRotated, ClientId: openIdApplication.ClientId, Reason: $"{openIdApplication.DisplayName} ({openIdApplication.ClientId})"));
        }

        // Email the new credential to the addresses chosen in the rotation prompt (prefilled
        // with the tenant admins). An empty list skips the email, as the prompt says. A mail
        // failure never fails the rotation: the credential is shown once in the result dialog.
        List<string> recipients = DeserializeEmailList(emails);
        string rotatedBy = User.Identity?.Name ?? "an administrator";
        string credential = type == "secret" ? "client secret" : "API key";

        List<string> failures = SendEach(recipients, email =>
        {
            if (type == "secret")
            {
                emailTemplate.ClientSecretRotated(email, openIdApplication.DisplayName, rotateKeys.Key, rotatedBy);
            }
            else
            {
                emailTemplate.ApiKeyRotated(email, openIdApplication.DisplayName, rotateKeys.Key, rotatedBy);
            }
        });

        if (failures.Count != 0)
        {
            rotateKeys.EmailError = $"The new {credential} could not be emailed to: {string.Join(", ", failures)}. Copy it now - it will not be shown again.";
        }

        await InvalidateTenantCacheAsync(openIdApplication);

        await adminAlerts.NotifySuperusersAsync($"tenant {credential} rotated",
            $"The {credential} of tenant {openIdApplication.DisplayName} ({openIdApplication.ClientId}) was rotated. The previous one no longer works.",
            [], LoggedInUserID);

        return PartialView("_RotateKeys", rotateKeys);
    }

    /// <summary>
    /// Revokes the tenant's API key without issuing a new one (e.g. it leaked): every call with
    /// it is rejected from now on. "Rotate API key" issues a new key later.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeApiKey(string id)
    {
        OpenIdApplication? openIdApplication = string.IsNullOrEmpty(id) ? null : await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(openIdApplication.APIKeyHash))
        {
            return JsonError("This tenant has no API key to revoke.");
        }

        string? previousPrefix = openIdApplication.APIKeyPrefix;

        openIdApplication.APIKeyHash = null;
        openIdApplication.APIKeyProtected = null;
        openIdApplication.APIKeyPrefix = null;
        openIdApplication.UpdatedOn = DateTime.UtcNow;
        openIdApplication.UpdatedById = LoggedInUserID;

        await manager.UpdateAsync(openIdApplication);

        await InvalidateTenantCacheAsync(openIdApplication);

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.ApiKeyRevoked, ClientId: openIdApplication.ClientId,
            Reason: $"API key {(string.IsNullOrEmpty(previousPrefix) ? string.Empty : $"'{previousPrefix}...' ")}revoked for {openIdApplication.DisplayName}"));

        await adminAlerts.NotifySuperusersAsync("tenant API key revoked",
            $"The API key of tenant {openIdApplication.DisplayName} ({openIdApplication.ClientId}) was revoked. Calls with it are rejected until a new key is issued.",
            [], LoggedInUserID);

        return JsonSuccess("The API key has been revoked. Calls with it are now rejected; use Rotate API key to issue a new one.");
    }

    [HttpGet]
    public async Task<IActionResult> Invite(string id)
    {
        OpenIdApplication? openIdApplication = string.IsNullOrEmpty(id) ? null : await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        return PartialView("_InviteUsers", new InviteUsersViewModel
        {
            ApplicationId = openIdApplication.Id,
            Name = openIdApplication.DisplayName
        });
    }

    /// <summary>
    /// Invites people by email. New addresses get an account and access with the password setup
    /// link; existing accounts get an invitation they accept in the identity portal.
    /// </summary>
    [HttpPost, ActionName("Invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteConfirm(InviteUsersRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? openIdApplication = string.IsNullOrEmpty(request.ApplicationId) ? null : await context.Applications.FindAsync(request.ApplicationId);

        if (openIdApplication is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        List<string> emails = InviteEmailList.Parse(request.Emails);

        if (emails.Count == 0)
        {
            return JsonError("Enter at least one email address.");
        }

        if (emails.Count > InviteEmailList.MaxEmails)
        {
            return JsonError($"Invite at most {InviteEmailList.MaxEmails} people at a time.");
        }

        List<string> invalid = [.. emails.Where(email => !InviteEmailList.IsValid(email))];

        if (invalid.Count != 0)
        {
            return JsonError($"Not valid email address(es): {string.Join(", ", invalid)}. Nobody was invited.");
        }

        // The name is for a single new account; with several addresses each defaults to its email.
        string? displayName = emails.Count == 1 ? request.DisplayName : null;

        List<TenantInviteResult> results = [];

        foreach (string email in emails)
        {
            results.Add(await inviteService.InviteAsync(openIdApplication, email, displayName, request.Notify,
                LoggedInUserID ?? "system", AuditTrigger.Admin, LoggedInUserEmail ?? "an administrator"));
        }

        string summary = DescribeInviteResults(results, request.Notify);

        return results.Any(r => r.Outcome != TenantInviteOutcome.Failed) ? JsonSuccess(summary) : JsonError(summary);
    }

    [HttpGet]
    public async Task<IActionResult> PendingInvitations(string id)
    {
        OpenIdApplication? openIdApplication = string.IsNullOrEmpty(id) ? null : await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        List<PendingInvitationItem> invitations = await context.TenantInvitations
            .Where(i => i.ApplicationId == id && i.Status == TenantInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedOn)
            .Select(i => new PendingInvitationItem
            {
                Id = i.Id,
                Email = i.Email,
                DisplayName = context.Users.Where(u => u.Id == i.UserId).Select(u => u.DisplayName).FirstOrDefault(),
                InvitedBy = i.InvitedBy,
                CreatedOn = i.CreatedOn,
                ExpiresOn = i.ExpiresOn
            })
            .ToListAsync();

        return PartialView("_PendingInvitations", new PendingInvitationsViewModel
        {
            ApplicationId = openIdApplication.Id,
            Name = openIdApplication.DisplayName,
            Invitations = invitations
        });
    }

    /// <summary>
    /// Emails a pending invitation again and restarts its expiry.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendInvitation(string id)
    {
        TenantInvitation? invitation = string.IsNullOrEmpty(id) ? null
            : await context.TenantInvitations.SingleOrDefaultAsync(i => i.Id == id && i.Status == TenantInvitationStatus.Pending);

        OpenIdApplication? openIdApplication = invitation is null ? null : await context.Applications.FindAsync(invitation.ApplicationId);
        string? email = invitation is null ? null : await context.Users.Where(u => u.Id == invitation.UserId).Select(u => u.Email).SingleOrDefaultAsync();

        if (invitation is null || openIdApplication is null || string.IsNullOrEmpty(email))
        {
            return JsonError("This invitation is no longer pending. Refresh the list.");
        }

        TenantInviteResult result = await inviteService.InviteAsync(openIdApplication, email, null, notify: true,
            LoggedInUserID ?? "system", AuditTrigger.Admin, LoggedInUserEmail ?? "an administrator");

        return result.Outcome switch
        {
            TenantInviteOutcome.AlreadyMember => JsonSuccess($"{email} already has access to this tenant."),
            TenantInviteOutcome.Failed => JsonError($"The invitation could not be sent: {result.Error}."),
            _ when !result.EmailSent => JsonError($"The invitation to {email} could not be emailed. Please try again later."),
            TenantInviteOutcome.Granted => JsonSuccess($"{email} has access now and was sent the link to set up their account."),
            _ => JsonSuccess($"The invitation has been sent to {email} again. It expires in {TenantInvitation.Lifetime.Days} days.")
        };
    }

    /// <summary>
    /// Withdraws a pending invitation: its link stops working. No email (nothing was granted).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeInvitation(string id)
    {
        if (string.IsNullOrEmpty(id) || !await inviteService.RevokeAsync(id, LoggedInUserID ?? "system"))
        {
            return JsonError("This invitation is no longer pending. Refresh the list.");
        }

        return JsonSuccess("The invitation has been withdrawn. Its link no longer works.");
    }

    /// <summary>
    /// The invite drawer's result: who was invited, added, skipped or failed.
    /// </summary>
    private static string DescribeInviteResults(List<TenantInviteResult> results, bool notify)
    {
        List<string> parts = [];

        int invited = results.Count(r => r.Outcome == TenantInviteOutcome.Invited);
        int granted = results.Count(r => r.Outcome == TenantInviteOutcome.Granted);

        if (invited != 0)
        {
            parts.Add($"{invited} existing account(s) invited: they get access once they accept.");
        }

        if (granted != 0)
        {
            parts.Add($"{granted} new user(s) added: they set up their account from the emailed link.");
        }

        List<string> members = [.. results.Where(r => r.Outcome == TenantInviteOutcome.AlreadyMember).Select(r => r.Email)];

        if (members.Count != 0)
        {
            parts.Add($"Already members: {string.Join(", ", members)}.");
        }

        List<string> failed = [.. results.Where(r => r.Outcome == TenantInviteOutcome.Failed).Select(r => $"{r.Email} ({r.Error})")];

        if (failed.Count != 0)
        {
            parts.Add($"Failed: {string.Join(", ", failed)}.");
        }

        if (notify)
        {
            List<string> unsent = [.. results.Where(r => r.Outcome is TenantInviteOutcome.Invited or TenantInviteOutcome.Granted && !r.EmailSent).Select(r => r.Email)];

            if (unsent.Count != 0)
            {
                parts.Add($"The email could not be sent to: {string.Join(", ", unsent)}. Resend it from Pending invitations, or invite again later.");
            }
        }
        else
        {
            if (invited != 0)
            {
                parts.Add("No emails were sent: the invitations are listed in each user's account settings.");
            }

            if (granted != 0)
            {
                parts.Add("New users can't sign in until they get their setup link: send it with Users > Resend invite.");
            }
        }

        return string.Join(" ", parts);
    }

    [HttpGet]
    public async Task<IActionResult> Unassign(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        // Retrieve distinct users associated with the application. Access left behind by a
        // deleted account has no user row: it is listed as "Deleted user" (with its id) so it
        // can still be found and removed, instead of showing as a blank entry.
        List<BasicUserViewModel> users = await context.Authorizations
            .Where(a => a.Application.Id == id)
            .Select(s => new BasicUserViewModel
            {
                Id = s.Subject,
                DisplayName = s.SubjectUser != null ? s.SubjectUser.DisplayName : "Deleted user",
                Email = s.SubjectUser != null ? s.SubjectUser.Email : s.Subject,
                ProfileImage = s.SubjectUser != null ? s.SubjectUser.ProfileImage : null
            }).Distinct().ToListAsync();

        users = [.. users.OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.Email, StringComparer.OrdinalIgnoreCase)];

        UnAssignViewModel unAssignViewModel = new()
        {
            ApplicationId = openIdApplication.Id,
            Name = openIdApplication.DisplayName,
            Users = users
        };

        return PartialView("_UnassignUser", unAssignViewModel);
    }

    [HttpPost, ActionName("Unassign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignConfirm(UnAssignViewModelRequest unAssignViewModel, bool notify = true)
    {
        if (string.IsNullOrEmpty(unAssignViewModel.ApplicationId))
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(unAssignViewModel.ApplicationId);

        if (openIdApplication is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        if (unAssignViewModel.Users is null || unAssignViewModel.Users.Count is 0)
        {
            return JsonError("Please select at least one user to unassign.");
        }

        // Removing your own access to TenantSuite would lock you out of it immediately.
        if (IsTenantSuite(openIdApplication) && unAssignViewModel.Users.Contains(LoggedInUserID ?? string.Empty))
        {
            return JsonError("You cannot remove your own access to TenantSuite.");
        }

        List<string> unassigned = await tenantManagementService.UnassignUsersAsync(openIdApplication, unAssignViewModel.Users);

        foreach (string userId in unassigned)
        {
            await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.UserRemovedFromTenant, TargetUserId: userId, ClientId: openIdApplication.ClientId, Reason: "user unassigned by administrator"));

            if (notify)
            {
                await tenantAccessNotifier.NotifyRemovedAsync(userId, openIdApplication);
            }
        }

        string successMessage = $"{unassigned.Count} user(s) have been successfully unassigned from the tenant.";
        //TODO: Remove Admins from the list of unassign
        // if (adminInAssign)
        // {
        //     successMessage += " Note: Some users who manage the tenant could not be unassigned.";
        // }

        return JsonSuccess(successMessage);
    }

    public async Task<IActionResult> Search(string term)
    {
        IQueryable<OpenIdApplication> tenants = context.Applications;

        if (!string.IsNullOrEmpty(term))
        {
            tenants = tenants.Where(a => a.DisplayName.Contains(term) || a.ClientId.Contains(term) || a.Description.Contains(term));
        }

        // Ordered so the same term always yields the same five suggestions.
        List<BasicApplicationViewModel> basicUserViews = await tenants.OrderBy(a => a.DisplayName).ThenBy(a => a.ClientId).Select(u => new BasicApplicationViewModel
        {
            Id = u.Id,
            ClientId = u.ClientId,
            DisplayName = u.DisplayName,
            Icon = u.Theme.Favicon
        }).Take(5).ToListAsync();

        return Ok(basicUserViews);
    }

    #endregion Actions

    #region Private

    private bool OpenIdApplicationExists(string id)
    {
        return context.Applications.Any(e => e.Id == id);
    }

    private static EditApplicationViewModel ToEdit(OpenIdApplication entity)
    {
        return new EditApplicationViewModel
        {
            Id = entity.Id,
            ClientId = entity.ClientId,
            DisplayName = entity.DisplayName,
            Legalname = entity.Legalname,
            // Never send the stored connection string (it contains credentials) back to the
            // browser; an empty field means "keep the existing value" on submit.
            HasConnectionString = entity?.ConnectionStringProtected?.Length > 0,
            TenantUrl = entity.TenantUrl,
            Website = entity.Website,
            Description = entity.Description,
            //Permissions = entity.Permissions != null ? JsonSerializer.Deserialize<List<string>>(entity.Permissions) : [],
            RedirectUri = entity.RedirectUris != null ? string.Join(",", JsonSerializer.Deserialize<string[]>(entity.RedirectUris)) : "",
            PostLogoutRedirectUri = entity.PostLogoutRedirectUris != null ? string.Join(",", JsonSerializer.Deserialize<string[]>(entity.PostLogoutRedirectUris)) : "",
            AdminEmail = DeserializeEmails(entity.AdminEmail),
        };
    }

    /// <summary>
    /// Deserializes the stored JSON email array to a comma-joined string for the choices UI.
    /// </summary>
    private static string DeserializeEmails(string? serialized)
    {
        return serialized != null ? string.Join(",", JsonSerializer.Deserialize<string[]>(serialized) ?? []) : "";
    }

    /// <summary>
    /// Parses a comma-joined email list (from the choices UI) into distinct, trimmed entries.
    /// </summary>
    private static List<string> DeserializeEmailList(string? joined)
    {
        return (joined ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Sends the same notification to every recipient. A failure for one address is logged
    /// and reported to the caller but never thrown: credential delivery must not break the
    /// tenant operation, and the secrets are always shown once in the browser as fallback.
    /// </summary>
    private List<string> SendEach(List<string> recipients, Action<string> send)
    {
        List<string> failures = [];

        foreach (string recipient in recipients)
        {
            try
            {
                send(recipient);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send notification email to {Recipient}.", recipient);
                failures.Add(recipient);
            }
        }

        return failures;
    }

    /// <summary>
    /// Validates the comma-joined admin email list and serializes it for storage. Returns
    /// false with an error message when the list is empty or contains invalid addresses.
    /// </summary>
    private static bool TrySerializeEmails(string? joined, out string serialized, out string error)
    {
        var emails = (joined ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        EmailAddressAttribute validator = new();

        var invalid = emails.Where(email => !validator.IsValid(email)).ToList();

        if (emails.Count == 0)
        {
            serialized = "[]";
            error = "Add at least one admin email.";
            return false;
        }

        if (invalid.Count != 0)
        {
            serialized = "[]";
            error = $"Invalid email address(es): {string.Join(", ", invalid)}";
            return false;
        }

        serialized = JsonSerializer.Serialize(emails);
        error = string.Empty;
        return true;
    }

    private void MapProperties(CreateApplicationViewModel model, OpenIdApplication entity)
    {
        entity.Website = model.Website;
        entity.DisplayName = model.DisplayName;
        entity.Legalname = model.Legalname;
        entity.Description = model.Description;
        entity.ClientId = model.ClientId;
        entity.AdminEmail = JsonSerializer.Serialize((model.AdminEmail ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        entity.TenantUrl = model.TenantUrl;
        entity.Theme = new ThemeConfiguration
        {
            Logo = BlogArrayConstants.DefaultLogoUrl,
            Favicon = BlogArrayConstants.DefaultFaviconUrl
        };
        entity.Security = new TenantSecurityConfiguration
        {
            IsMfaEnforced = false,
            IsSingleSignOutEnabled = false,
            IsSocialAuthEnabled = false,
            IsSsoEnabled = false
        };
        entity.ClientType = ClientTypes.Confidential;
        entity.ConsentType = ConsentTypes.External;
        entity.Permissions = JsonSerializer.Serialize(OpenIdConstants.OpenIdPermissions());
        entity.Requirements = JsonSerializer.Serialize(OpenIdConstants.OpenIdRequirements());

        entity.RedirectUris = string.IsNullOrEmpty(model.RedirectUri) ? null : JsonSerializer.Serialize(model.RedirectUri.Split(","));
        entity.PostLogoutRedirectUris = string.IsNullOrEmpty(model.PostLogoutRedirectUri) ? null : JsonSerializer.Serialize(model.PostLogoutRedirectUri.Split(","));

        entity.CreatedById = LoggedInUserID;
        entity.CreatedOn = DateTime.UtcNow;
    }

    private void MapProperties(EditApplicationViewModel model, OpenIdApplication entity)
    {
        entity.Website = model.Website;
        entity.DisplayName = model.DisplayName;
        entity.Legalname = model.Legalname;
        entity.Description = model.Description;
        entity.TenantUrl = model.TenantUrl;
        // The connection string is never rendered back to the browser: an empty value means
        // "keep the existing connection string" rather than clearing it.
        SetConnectionString(entity, model.ConnectionString);
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;
        entity.AdminEmail = JsonSerializer.Serialize((model.AdminEmail ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        //entity.Permissions = JsonSerializer.Serialize(model.Permissions);
        entity.RedirectUris = string.IsNullOrEmpty(model.RedirectUri) ? null : JsonSerializer.Serialize(model.RedirectUri.Split(","));
        entity.PostLogoutRedirectUris = string.IsNullOrEmpty(model.PostLogoutRedirectUri) ? null : JsonSerializer.Serialize(model.PostLogoutRedirectUri.Split(","));
    }

    /// <summary>
    /// Stores the client secret without plaintext: a DataProtection-protected copy for the
    /// tenant's OIDC handshake (OpenIddict separately keeps its own one-way hash). The
    /// plaintext lives only in the current request.
    /// </summary>
    private void SetClientSecret(OpenIdApplication entity, string plainSecret)
    {
        entity.ClientSecretProtected = protector.Protect(plainSecret);
    }

    /// <summary>
    /// Stores the connection string protected at rest; plaintext is never persisted.
    /// </summary>
    private void SetConnectionString(OpenIdApplication entity, string? plainConnectionString)
    {
        if (string.IsNullOrWhiteSpace(plainConnectionString))
        {
            return;
        }

        entity.ConnectionStringProtected = protector.Protect(plainConnectionString);
    }

    /// <summary>
    /// Returns the supplied secret when it is strong enough, otherwise generates a new
    /// cryptographically random 32-character value.
    /// </summary>
    private static string FinalizeSecret(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length >= 32 ? value : Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }

    /// <summary>
    /// Stores the API key without plaintext: SHA-256 hash for validation, a
    /// DataProtection-protected copy for tenant apps to retrieve and send, and a short
    /// display prefix. The plaintext lives only in the current request.
    /// </summary>
    private void SetApiKey(OpenIdApplication entity, string plainKey)
    {
        entity.APIKeyHash = ApiKeyHasher.Hash(plainKey);
        entity.APIKeyProtected = protector.Protect(plainKey);
        entity.APIKeyPrefix = ApiKeyHasher.GetPrefix(plainKey, _apiKeyPrefixLength);
    }

    /// <summary>
    /// True for TenantSuite's own application (its OIDC client), whose access an administrator
    /// must not remove from themselves.
    /// </summary>
    private bool IsTenantSuite(OpenIdApplication application)
    {
        string? tenantSuiteClientId = configuration["OIDC:ClientId"];

        return !string.IsNullOrEmpty(tenantSuiteClientId)
            && string.Equals(application.ClientId, tenantSuiteClientId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the redirect and post-logout redirect URI fields (comma-separated, trimmed,
    /// absolute http(s) URLs; at least one redirect URI). Errors are added to ModelState.
    /// </summary>
    private bool TryValidateRedirectUris(ApplicationViewModel model, out List<string> redirectUris, out List<string> postLogoutRedirectUris)
    {
        bool valid = true;

        if (!TenantInputValidator.TryParseUris(model.RedirectUri, required: true, "Redirect URIs", out redirectUris, out string redirectError))
        {
            ModelState.AddModelError(nameof(model.RedirectUri), redirectError);
            valid = false;
        }

        if (!TenantInputValidator.TryParseUris(model.PostLogoutRedirectUri, required: false, "Post logout redirect URIs", out postLogoutRedirectUris, out string postLogoutError))
        {
            ModelState.AddModelError(nameof(model.PostLogoutRedirectUri), postLogoutError);
            valid = false;
        }

        // The tenant URL is trusted by the identity server (single sign-out target, allowed
        // SAML return origin), and the website is shown as a link: both must be real URLs.
        if (!string.IsNullOrWhiteSpace(model.TenantUrl) && !TenantInputValidator.IsValidAbsoluteHttpUrl(model.TenantUrl))
        {
            ModelState.AddModelError(nameof(model.TenantUrl), "Tenant URL must be an absolute http(s) URL, e.g. https://acme.blogarray.dev.");
            valid = false;
        }

        if (!string.IsNullOrWhiteSpace(model.Website) && !TenantInputValidator.IsValidAbsoluteHttpUrl(model.Website))
        {
            ModelState.AddModelError(nameof(model.Website), "Website must be an absolute http(s) URL, e.g. https://www.example.com.");
            valid = false;
        }

        return valid;
    }

    /// <summary>
    /// Whether the submitted URIs are the stored ones, ignoring order, surrounding whitespace
    /// and duplicates (so editing other fields never trips over how they were stored).
    /// </summary>
    private static bool SameUris(IEnumerable<string> submitted, string? storedJson)
    {
        static HashSet<string> Normalize(IEnumerable<string> uris) =>
            [.. uris.Select(uri => uri.Trim()).Where(uri => uri.Length > 0)];

        return Normalize(submitted).SetEquals(Normalize(DeserializeUriList(storedJson)));
    }

    /// <summary>
    /// "Setting: On -> Off" when a switch changed, nothing otherwise.
    /// </summary>
    private static IEnumerable<string> DescribeSecurityChanges(bool before, bool after, string setting)
    {
        if (before != after)
        {
            yield return $"{setting}: {(before ? "on" : "off")} -> {(after ? "on" : "off")}";
        }
    }

    private static List<string> DeserializeUriList(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// The messages of an OpenIddict validation failure, joined for display.
    /// </summary>
    private static string DescribeValidationErrors(OpenIddictExceptions.ValidationException exception)
    {
        string details = string.Join(" ", exception.Results.Select(result => result.ErrorMessage).Where(message => !string.IsNullOrEmpty(message)));

        return string.IsNullOrEmpty(details) ? "The tenant could not be saved: its settings are not valid." : details;
    }

    private void SetOptions()
    {
        ViewBag.Permissions = OpenIdConstants.OpenIdPermissions().ToSelectList();
    }

    #endregion Private

    #region Cache

    /// <summary>
    /// Drops the tenant's cached store entries after a change. The tenant apps' store
    /// (OpenIdApplicationTenantStore) reloads them from the database on the next request, so
    /// the cache never has to be written - or hold secrets - from here.
    /// </summary>
    private async Task InvalidateTenantCacheAsync(OpenIdApplication openIdApplication)
    {
        if (!string.IsNullOrEmpty(openIdApplication.Id))
        {
            await cacheService.RemoveAsync(CacheKeyHelper.GetTenantIdKey(openIdApplication.Id));
        }

        if (!string.IsNullOrEmpty(openIdApplication.ClientId))
        {
            await cacheService.RemoveAsync(CacheKeyHelper.GetTenantIdentifierKey(openIdApplication.ClientId));
        }
    }

    #endregion Cache

}
