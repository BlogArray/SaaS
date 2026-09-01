//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BlogArray.SaaS.Application.Filters;

/// <summary>
/// Restricts actions to requests originating from the configured IP allow-list
/// (semicolon-separated). Entries may be exact addresses ("192.168.1.5") or CIDR networks
/// ("10.0.0.0/24", "2001:db8::/48"). The allow-list is parsed and validated once at
/// construction so a missing or malformed configuration fails fast at startup instead of
/// producing 500 responses per call or silently disabling the protection.
/// </summary>
public class ClientIpCheckActionFilter(string? safelist) : ActionFilterAttribute
{
    private readonly (List<IPAddress> addresses, List<IPNetwork> networks) allowed = ParseSafelist(safelist);

    public ClientIpCheckActionFilter() : this(null)
    {
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        IPAddress? remoteIp = context?.HttpContext?.Connection?.RemoteIpAddress;

        if (remoteIp is null || !IsAllowed(remoteIp))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        base.OnActionExecuting(context);
    }

    private bool IsAllowed(IPAddress remoteIp)
    {
        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        foreach (IPAddress address in allowed.addresses)
        {
            IPAddress candidate = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

            if (candidate.Equals(remoteIp))
            {
                return true;
            }
        }

        foreach (IPNetwork network in allowed.networks)
        {
            if (network.Contains(remoteIp))
            {
                return true;
            }
        }

        return false;
    }

    private static (List<IPAddress> addresses, List<IPNetwork> networks) ParseSafelist(string? safelist)
    {
        if (string.IsNullOrWhiteSpace(safelist))
        {
            throw new InvalidOperationException(
                "IPSafeList is not configured. Add a semicolon-separated allow-list of IP addresses and/or CIDR networks (e.g. \"127.0.0.1;10.0.0.0/24\") to the application configuration to use IP-restricted endpoints.");
        }

        List<IPAddress> addresses = [];
        List<IPNetwork> networks = [];

        foreach (string entry in safelist.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (entry.Contains('/'))
            {
                if (!IPNetwork.TryParse(entry, out IPNetwork network))
                {
                    throw new InvalidOperationException($"IPSafeList contains an invalid CIDR network: '{entry}'.");
                }

                networks.Add(network);
            }
            else if (IPAddress.TryParse(entry, out IPAddress? address))
            {
                addresses.Add(address);
            }
            else
            {
                throw new InvalidOperationException($"IPSafeList contains an invalid IP address: '{entry}'.");
            }
        }

        if (addresses.Count == 0 && networks.Count == 0)
        {
            throw new InvalidOperationException("IPSafeList does not contain any valid entries.");
        }

        return (addresses, networks);
    }
}
