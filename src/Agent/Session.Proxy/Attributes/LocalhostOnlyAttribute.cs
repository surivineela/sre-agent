using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Session.Proxy.Attributes;

public sealed class LocalhostOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var http = context.HttpContext;
        var ip = http.Connection.RemoteIpAddress;

        // If behind reverse proxy and you enable forwarded headers, ip may already be the original client
        if (ip is null || !IPAddress.IsLoopback(ip))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        base.OnActionExecuting(context);
    }
}
