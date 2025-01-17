//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.App.Interfaces;
using BlogArray.SaaS.App.Models;
using Microsoft.EntityFrameworkCore;
using Refit;


namespace BlogArray.SaaS.App.Controllers;

[Microsoft.AspNetCore.Authorization.Authorize]
public class PersonnelsController(SaasAppDbContext context,
     IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
     IMembershipClient membershipClient, ILogger<PersonnelsController> logger) : BaseController
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

        try
        {
            await membershipClient.Invite(new UserTenantVM
            {
                Email = userVM.Email,
                Tenant = multiTenantContextAccessor.MultiTenantContext.TenantInfo.Identifier
            });

            await context.AppPersonnels.AddAsync(new AppPersonnel
            {
                Email = userVM.Email,
                IsActive = userVM.IsActive
            });
            await context.SaveChangesAsync();
            AddSuccessMessage($"Personnel {userVM.Email} is successfully added.");
        }
        catch (ApiException apiException)
        {
            AddErrorMessage($"Unable to invite personnel with email {userVM.Email}.");
            logger.LogError("The API returned an exception with status code {StatusCode} with content {Content}", apiException.StatusCode, apiException.Content);
        }

        return RedirectToAction("Index");
    }

    public async Task<IActionResult> DeActivate(long id)
    {
        AppPersonnel? user = await context.AppPersonnels.SingleOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            try
            {
                await membershipClient.RemoveUserFromTenant(new UserTenantVM
                {
                    Email = user.Email,
                    Tenant = multiTenantContextAccessor.MultiTenantContext.TenantInfo.Identifier
                });
                user.IsActive = false;
                await context.SaveChangesAsync();

                AddSuccessMessage($"Personnel {user.Email} is deactivated successfully.");
            }
            catch (ApiException apiException)
            {
                AddErrorMessage($"Unable to deactivate personnel with email {user.Email}.");
                logger.LogError("The API returned an exception with status code {StatusCode} with content {Content}", apiException.StatusCode, apiException.Content);
            }
        }
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Activate(long id)
    {
        AppPersonnel? user = await context.AppPersonnels.SingleOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            try
            {
                await membershipClient.AddUserToTenant(new UserTenantVM
                {
                    Email = user.Email,
                    Tenant = multiTenantContextAccessor.MultiTenantContext.TenantInfo.Identifier
                });
                user.IsActive = true;
                await context.SaveChangesAsync();
                AddSuccessMessage($"Personnel {user.Email} is activated successfully.");
            }
            catch (ApiException apiException)
            {
                AddErrorMessage($"Unable to activate personnel with email {user.Email}.");
                logger.LogError("The API returned an exception with status code {StatusCode} with content {Content}", apiException.StatusCode, apiException.Content);
            }
        }
        return RedirectToAction("Index");
    }
}
