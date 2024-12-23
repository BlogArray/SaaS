using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.TenantStore.App;

public class AppPersonnel
{
    [Key]
    public long Id { get; set; }

    //[Required]
    //public string Name { get; set; } = default!;

    [Required]
    public string Email { get; set; } = default!;

    //[Required]
    //public string Username { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}
