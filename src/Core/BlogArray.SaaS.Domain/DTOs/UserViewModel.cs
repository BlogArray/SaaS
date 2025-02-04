﻿//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;
using BlogArray.SaaS.Mvc.Attributes;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BlogArray.SaaS.Domain.DTOs;

public class BasicUserViewModel
{
    public string Id { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string Email { get; set; } = default!;

    public string? ProfileImage { get; set; } = default!;
}

public class UserViewModel
{
    public string Id { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string Email { get; set; } = default!;

    public string? ProfileImage { get; set; } = default!;

    public string? Gender { get; set; } = default!;

    public string[]? Roles { get; set; } = default!;

    public DateTimeOffset? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; }

    public bool IsActive { get; set; } = true;
}

public class CreateUserViewModel
{
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

    [Required]
    [StringLength(128)]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = default!;

    public bool SendLinkToMe { get; set; }
}

public class EditUserViewModel : CreateUserViewModel
{
    public string Id { get; set; } = default!;

    public string? ProfileImage { get; set; } = default!;

    [Display(Name = "Gender")]
    public string? Gender { get; set; } = default!;

    [Display(Name = "Time zone")]
    public string? TimeZone { get; set; } = default!;

    [Display(Name = "Language")]
    public string? LocaleCode { get; set; } = default!;
}

public class ResetPasswordViewModel
{
    public string Id { get; set; } = default!;

    public string? DisplayName { get; set; }

    public bool CreatePassword { get; set; }

    [RequiredIf("CreatePassword", true, ErrorMessage = "Enter a strong password")]
    [DataType(DataType.Password)]
    public string? Password { get; set; } = default!;
}

public class UserToolbar
{
    public string Id { get; set; } = default!;

    public bool IsActive { get; set; }

    public bool IsEmailPhoneConfirmed { get; set; }

    public bool LockoutEnabled { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public int TenantsCount { get; set; } = default!;
}

public class AssignTenantViewModel
{
    public string UserId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public List<BasicApplicationViewModel>? Tenants { get; set; } = default!;
}

public class AssignTenantRequestViewModel
{
    public string UserId { get; set; } = default!;

    public List<string>? Tenants { get; set; } = default!;
}

public class UnAssignTenantViewModel : AssignTenantViewModel
{
}

public class UnAssignTenantRequestViewModel : AssignTenantRequestViewModel
{
}

public class UserRolesViewModel
{
    public string UserId { get; set; } = default!;

    public List<string>? RolesSelected { get; set; } = default!;

    public List<SelectListItem>? Roles { get; set; } = default!;
}
