using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;

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
