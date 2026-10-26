using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.Application.Services;

/// <summary>
/// Security alerts about administrative changes, sent to administrators rather than to the
/// affected user: platform-level changes to every Superuser (a second pair of eyes), and
/// tenant-level changes to a tenant's admin addresses. Never throws: the change itself has
/// already been made, so a mail failure is only logged.
/// </summary>
public interface IAdminAlertService
{
    /// <summary>
    /// Alerts every active Superuser except <paramref name="excludeUserId"/> (normally the
    /// administrator who made the change, who already knows).
    /// </summary>
    Task NotifySuperusersAsync(string subject, string summary, IReadOnlyList<string> details, string? excludeUserId);

    /// <summary>
    /// Alerts the given addresses (e.g. a tenant's admin emails).
    /// </summary>
    void NotifyAddresses(IEnumerable<string> addresses, string subject, string summary, IReadOnlyList<string> details);
}

public class AdminAlertService(UserManager<ApplicationUser> userManager,
    IEmailTemplate emailTemplate,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AdminAlertService> logger) : IAdminAlertService
{
    private const string SuperuserRole = "Superuser";

    public async Task NotifySuperusersAsync(string subject, string summary, IReadOnlyList<string> details, string? excludeUserId)
    {
        try
        {
            IEnumerable<string> addresses = (await userManager.GetUsersInRoleAsync(SuperuserRole))
                .Where(user => user.IsActive && user.Id != excludeUserId && !string.IsNullOrEmpty(user.Email))
                .Select(user => user.Email!);

            NotifyAddresses(addresses, subject, summary, details);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send the '{Subject}' alert to the Superusers.", subject);
        }
    }

    public void NotifyAddresses(IEnumerable<string> addresses, string subject, string summary, IReadOnlyList<string> details)
    {
        string actor = Actor();

        foreach (string address in addresses.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                emailTemplate.AdminSecurityAlert(address, subject, summary, details, actor);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not send the '{Subject}' alert to {Address}.", subject, address);
            }
        }
    }

    /// <summary>
    /// The administrator making the change, as shown in the alert.
    /// </summary>
    private string Actor()
    {
        System.Security.Claims.ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;

        return user?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? user?.Identity?.Name
            ?? "an administrator";
    }
}
