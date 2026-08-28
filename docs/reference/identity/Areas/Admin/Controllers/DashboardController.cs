using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Identity.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Superuser")]
public class DashboardController(OpenIdDbContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        DateTime now = DateTime.Now;

        DateTime currentPeriodStart = now.AddDays(-30);
        //DateTime previousPeriodStart = now.AddDays(-60);
        //DateTime previousPeriodEnd = now.AddDays(-31);

        int applications = await context.Applications.CountAsync();
        int appsPrev = await context.Applications.Where(s => s.CreatedOn >= currentPeriodStart).CountAsync();
        //var scopes = await context.Scopes.CountAsync();
        int users = await context.Users.CountAsync();
        int usersPrev = await context.Users.Where(s => s.CreatedOn >= currentPeriodStart).CountAsync();

        DashboardViewModel dashboard = new()
        {
            Applications = applications,
            Users = users,
            PrevApplications = appsPrev,
            PrevUsers = usersPrev
        };

        return View(dashboard);
    }
}
