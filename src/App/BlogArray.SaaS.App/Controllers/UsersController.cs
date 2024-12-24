using BlogArray.SaaS.App.Interfaces;
using BlogArray.SaaS.App.Models;
using BlogArray.SaaS.TenantStore.App;
using Microsoft.EntityFrameworkCore;
using Refit;


namespace BlogArray.SaaS.App.Controllers;

[Microsoft.AspNetCore.Authorization.Authorize]
public class UsersController(SaasAppDbContext context,
     IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
     IMembershipClient membershipClient, ILogger<UsersController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        List<UserVM> users = await context.AppPersonnels.Select(s => new UserVM
        {
            Id = s.Id,
            Email = s.Email,
            IsActive = s.IsActive
        }).ToListAsync();
        return View(users);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return View(userVM);
        }

        if (await context.AppPersonnels.AnyAsync(s => s.Email == userVM.Email))
        {
            ModelState.AddModelError("Email", "Email already exists.");
            return View(userVM);
        }

        await context.AppPersonnels.AddAsync(new AppPersonnel
        {
            Email = userVM.Email,
            IsActive = userVM.IsActive
        });
        await context.SaveChangesAsync();
        return RedirectToAction("Index");
    }


    public async Task<IActionResult> DeActivate(long id)
    {
        var user = await context.AppPersonnels.SingleOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            user.IsActive = false;
            await context.SaveChangesAsync();
            try
            {
                await membershipClient.RemoveUserFromTenant(new UserTenantVM
                {
                    Email = user.Email,
                    Tenant = multiTenantContextAccessor.MultiTenantContext.TenantInfo.Identifier
                });
            }
            catch (ApiException apiException)
            {
                logger.LogError("The API returned an exception with status code {0} with content {1}", apiException.StatusCode, apiException.Content);
            }
        }
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Activate(long id)
    {
        var user = await context.AppPersonnels.SingleOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            user.IsActive = true;
            await context.SaveChangesAsync();

            try
            {
                await membershipClient.AddUserToTenant(new UserTenantVM
                {
                    Email = user.Email,
                    Tenant = multiTenantContextAccessor.MultiTenantContext.TenantInfo.Identifier
                });
            }
            catch (ApiException apiException)
            {
                logger.LogError("The API returned an exception with status code {0} with content {1}", apiException.StatusCode, apiException.Content);
            }
        }
        return RedirectToAction("Index");
    }
}
