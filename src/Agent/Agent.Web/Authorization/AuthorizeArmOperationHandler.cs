// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.FeatureManagement;
using Agent.Logging;

namespace Agent.Web.Authorization;

public class AuthorizeArmOperationHandler : AuthorizationHandler<ArmOperationRequirement>
{
    private readonly ILogger<AuthorizeArmOperationHandler> _logger;

    public AuthorizeArmOperationHandler(ILogger<AuthorizeArmOperationHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ArmOperationRequirement requirement)
    {
        switch (context.Resource)
        {
            case HttpContext httpContext:
                {
                    if (await AuthorizeUserAction(httpContext.RequestServices, httpContext.User, requirement.Action))
                    {
                        context.Succeed(requirement);
                    }
                    else
                    {
                        _logger.LogInternalWarning($"Authorization of http request failed for action '{requirement.Action}'");
                    }
                    break;

                }
            case HubInvocationContext hubContext:
                {
                    if (await AuthorizeUserAction(hubContext.ServiceProvider, hubContext.Context.User, requirement.Action))
                    {
                        context.Succeed(requirement);
                    }
                    else
                    {
                        _logger.LogInternalWarning($"Authorization of hub invocation failed for action '{requirement.Action}'");
                    }
                    break;
                }
            default:
                break;
        }
    }

    public async Task<bool> AuthorizeUserAction(IServiceProvider sp, ClaimsPrincipal? user, string action)
    {
        if (user == null)
        {
            return false;
        }

        var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
        if (hostEnvironment.IsDevelopment())
        {
            return true;
        }

        var featureManager = sp.GetService(typeof(IFeatureManager)) as IFeatureManager;
        if (featureManager == null)
        {
            return true;
        }

        bool enabled;
        try
        {
            enabled = await featureManager.IsEnabledAsync(AllowedActionsAuthenticationConstants.FeatureFlagName);
        }
        catch
        {
            enabled = true;
        }

        if (!enabled)
        {
            return true;
        }

        var allowed = user.FindAll(AllowedActionsAuthenticationConstants.AllowedActionClaimType).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return allowed.Contains(action);
    }
}