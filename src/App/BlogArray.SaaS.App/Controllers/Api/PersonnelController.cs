using BlogArray.SaaS.App.Models;
using BlogArray.SaaS.Mvc.ActionFilters;
using BlogArray.SaaS.TenantStore.App;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.App.Controllers.Api;

[Route("api/[controller]")]
[ServiceFilter(typeof(ClientIpCheckActionFilter))]
[ApiController]
public class PersonnelController(SaasAppDbContext context) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(UserEmailVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (await context.AppPersonnels.AnyAsync(s => s.Email == userVM.Email || s.Email == userVM.Email))
        {
            return BadRequest($"Email already exists with email {userVM.Email}.");
        }

        await context.AppPersonnels.AddAsync(new AppPersonnel
        {
            Email = userVM.Email,
            IsActive = true
        });

        await context.SaveChangesAsync();
        //Todo invitation
        return Ok($"Personnel with email {userVM.Email} created successfuly.");
    }

    [HttpPost]
    public async Task<IActionResult> Active(UserEmailVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await context.AppPersonnels.SingleOrDefaultAsync(u => u.Email == userVM.Email);

        if (user is not null)
        {
            user.IsActive = true;
            await context.SaveChangesAsync();

            return Ok($"Personnel with email {userVM.Email} activated successfuly.");
        }

        return BadRequest($"Personnel with email {userVM.Email} not found.");
    }

    [HttpPost]
    public async Task<IActionResult> Disable(UserEmailVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await context.AppPersonnels.SingleOrDefaultAsync(u => u.Email == userVM.Email);

        if (user is not null)
        {
            user.IsActive = false;
            await context.SaveChangesAsync();

            return Ok($"Personnel with email {userVM.Email} activated successfuly.");
        }

        return BadRequest($"Personnel with email {userVM.Email} not found.");
    }

}
