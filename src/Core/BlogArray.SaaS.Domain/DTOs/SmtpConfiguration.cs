//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Domain.DTOs;

public class SmtpConfiguration
{
    public string FromEmail { get; set; } = default!;

    public string FromName { get; set; } = default!;

    public string Username { get; set; } = default!;

    public string Password { get; set; } = default!;

    public string Host { get; set; } = default!;

    public int Port { get; set; } = default!;

    public bool EnableSsl { get; set; } = default!;
}
