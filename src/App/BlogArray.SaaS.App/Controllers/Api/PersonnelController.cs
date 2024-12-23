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

        var user = await context.AppPersonnels.SingleOrDefaultAsync(u => u.Email == userVM.Email);

        if (user is null)
        {
            await context.AppPersonnels.AddAsync(new AppPersonnel
            {
                Email = userVM.Email,
                IsActive = true
            });

            await context.SaveChangesAsync();
        }
        else
        {
            user.IsActive = true;
        }

        await context.SaveChangesAsync();

        //Todo invitation
        return Ok($"Personnel with email {userVM.Email} created successfuly.");
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

            return Ok($"Personnel with email {userVM.Email} disabled successfuly.");
        }

        return BadRequest($"Personnel with email {userVM.Email} not found.");
    }

}
