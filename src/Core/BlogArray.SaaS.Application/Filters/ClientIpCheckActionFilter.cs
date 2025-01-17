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

public class ClientIpCheckActionFilter(string safelist) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        IPAddress remoteIp = context?.HttpContext?.Connection?.RemoteIpAddress;
        string[] ipList = safelist.Split(';');
        bool badIp = true;

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        foreach (string address in ipList)
        {
            IPAddress iPAddress = IPAddress.Parse(address);

            if (iPAddress.Equals(remoteIp))
            {
                badIp = false;
                break;
            }
        }

        if (badIp)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        base.OnActionExecuting(context);
    }
}
