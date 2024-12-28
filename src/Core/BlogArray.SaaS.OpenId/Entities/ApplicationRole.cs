using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.OpenId.Entities;

public class ApplicationRole : IdentityRole
{
    /// <summary>
    /// Role description
    /// </summary>
    [StringLength(512)]
    public string? Description { get; set; } = default!;

    public bool SystemDefined { get; set; } = false;
}
