//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.App.Models;

public class UserVM : CreateUserVM
{
    [Key]
    public long Id { get; set; }

    //[Required]
    //public string Name { get; set; } = default!;

    //[Required]
    //public string Username { get; set; } = default!;
}

public class CreateUserVM : UserEmailVM
{
    //[Required]
    //public string Name { get; set; } = default!;

    //[Required]
    //[DataType(DataType.EmailAddress)]
    //public string Email { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}
