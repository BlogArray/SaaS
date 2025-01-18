//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

global using System.ComponentModel.DataAnnotations;
global using System.Globalization;
global using System.Security.Claims;
global using BlogArray.SaaS.Application.Services;
global using BlogArray.SaaS.Domain.Constants;
global using BlogArray.SaaS.Domain.DTOs;
global using BlogArray.SaaS.Domain.Entities;
global using BlogArray.SaaS.Mvc.Extensions;
global using BlogArray.SaaS.OpenId;
global using BlogArray.SaaS.Resources.Controllers;
global using Microsoft.AspNetCore.Authentication;
global using Microsoft.AspNetCore.Identity;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Mvc.RazorPages;
