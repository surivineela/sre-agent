// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Agent.Web.Authorization;

/// <summary>
/// Attribute to declare a single required ARM-like operation for an API action.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class AuthorizeArmOperationAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "ArmOp:";
    public string Action { get; }

    public AuthorizeArmOperationAttribute(string action)
    {
        Action = action.Trim();
        Policy = $"{PolicyPrefix}{Action}";
    }
}

public sealed class ArmOperationRequirement : IAuthorizationRequirement
{
    public string Action { get; }
    public ArmOperationRequirement(string action)
    {
        Action = action;
    }
}

public sealed class ArmOperationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly AuthorizationOptions _options;

    // AuthorizationOptions does not use ConcurrentDictionary to store policies
    // We may see System.IndexOutOfRangeException in race conditions
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policies = new();

    public ArmOperationPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _options = options.Value;
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = _options.GetPolicy(policyName);
        if (policy != null)
        {
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        if (_policies.TryGetValue(policyName, out policy))
        {
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        var actionName = policyName.Substring(AuthorizeArmOperationAttribute.PolicyPrefix.Length);
        policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new ArmOperationRequirement(actionName))
            .Build();

        _policies.TryAdd(policyName, policy);

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return Task.FromResult(new AuthorizationPolicyBuilder().Build());
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return Task.FromResult<AuthorizationPolicy?>(null);
    }
}
