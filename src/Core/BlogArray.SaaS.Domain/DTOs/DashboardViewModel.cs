//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Domain.DTOs;

public class DashboardViewModel
{
    public int Applications { get; set; }
    public int PrevApplications { get; set; }

    public int Users { get; set; }
    public int PrevUsers { get; set; }

}
