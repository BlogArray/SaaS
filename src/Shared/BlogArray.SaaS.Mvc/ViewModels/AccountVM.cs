using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.Mvc.ViewModels;

public class ChangePasswordVM
{
    [Required(ErrorMessage = "Current Password is required to prevent unauthorized changes")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password", Prompt = "Enter current password")]
    public string CurrentPassword { get; set; } = default!;

    [Required(ErrorMessage = "Please enter new password")]
    [DataType(DataType.Password)]
    [Display(Name = "New password", Prompt = "Enter new password")]
    public string NewPassword { get; set; } = default!;

    [Required(ErrorMessage = "Please enter confirm password")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password", Prompt = "Confirm new password")]
    public string ConfirmPassword { get; set; } = default!;
}

public class ChangeUsernameVM
{
    public string Username { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Enter new username")]
    [Display(Name = "Username", Prompt = "Enter new username")]
    public string NewUsername { get; set; }
}

public class ChangeEmailVM
{
    public string Email { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Enter new email address")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    [Display(Name = "Email", Prompt = "Enter new email address")]
    public string NewEmail { get; set; }
}

public class AppUserBaseVM
{
    //public string Id { get; set; }

    [Required]
    [StringLength(128)]
    [Display(Name = "First name", Prompt = "Enter a first name")]
    public string FirstName { get; set; } = default!;

    [StringLength(128)]
    [Display(Name = "Last name", Prompt = "Enter a last name")]
    public string? LastName { get; set; } = default!;

    [MaxLength(128)]
    [Display(Name = "Display name", Prompt = "Enter a display name")]
    public string DisplayName { get; set; }

    public string? ProfileImage { get; set; }

    [Display(Name = "Gender")]
    public string? Gender { get; set; } = default!;

    [Display(Name = "Time zone")]
    public string? TimeZone { get; set; } = default!;

    [Display(Name = "Language")]
    public string? LocaleCode { get; set; } = default!;

}

public class UserEmailVM
{
    [Required]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = default!;
}

public class UserInviteEmailVM : UserEmailVM
{
    [Required]
    public string Tenant { get; set; } = default!;
}