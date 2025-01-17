//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Domain.DTOs;

public class CacheConfiguration
{
    public string? Type { get; set; }

    public int AbsoluteExpirationInHours { get; set; }

    public int SlidingExpirationInMinutes { get; set; }

    public string? ConnectionString { get; set; }
}
