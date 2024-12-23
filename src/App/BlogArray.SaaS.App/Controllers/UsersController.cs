using BlogArray.SaaS.App.Models;
using BlogArray.SaaS.TenantStore.App;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.App.Controllers;

[Authorize]
public class UsersController(SaasAppDbContext context) : Controller
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

        if (await context.AppPersonnels.AnyAsync(s => s.Email == userVM.Email || s.Email == userVM.Email))
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
        }
        return RedirectToAction("Index");
    }
}
