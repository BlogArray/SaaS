using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.Mvc.ViewModels;

public class ScopeViewModel
{
    public string? Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string Name { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string DisplayName { get; set; }

    public string? Description { get; set; }
}
