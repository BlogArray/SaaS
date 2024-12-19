using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.Mvc.ViewModels;

public class RoleViewModel
{
    public string? Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// Role description
    /// </summary>
    [StringLength(512)]
    public string? Description { get; set; } = default!;

    public int UsersAssigned { get; set; }

    public bool SystemDefined { get; set; } = false;
}

public class RoleMiniViewModel
{
    public string? Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// Role description
    /// </summary>
    [StringLength(512)]
    public string? Description { get; set; } = default!;
}
