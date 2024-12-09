using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using BlogArray.SaaS.App.Models;
using BlogArray.SaaS.TenantStore.App;

namespace BlogArray.SaaS.App.Controllers;

[Authorize]
public class UsersController(SaasAppDbContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        var users = await context.AppUsers.Select(s => new UserVM
        {
            Id = s.Id,
            Email = s.Email,
            Username = s.Username
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

        if (await context.AppUsers.AnyAsync(s => s.Username == userVM.Email || s.Email == userVM.Email))
        {
            ModelState.AddModelError("Email", "Email already exists.");
            return View(userVM);
        }

        await context.AppUsers.AddAsync(new SaasAppUser
        {
            Email = userVM.Email,
            Username = userVM.Email
        });
        await context.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}
