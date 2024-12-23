using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.App.Models;

public class UserVM
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

public class CreateUserVM
{
    //[Required]
    //public string Name { get; set; } = default!;

    [Required]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}
