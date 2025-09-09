// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Web.Authorization;

/// <summary>
/// Authentication handler that converts the header 'x-allowed-actions' into individual claims of type 'allowed_action'.
/// No real identity validation is performed; this is a lightweight augmenting auth scheme that should be chained with
/// whatever upstream authentication (e.g. APIM / Gateway) already validated the caller. If the header is absent, an empty
/// identity is still produced allowing authorization handlers to decide outcome.
/// </summary>
public sealed class AllowedActionsAuthenticationHandler : AuthenticationHandler<AllowedActionsAuthenticationSchemeOptions>
{
    private readonly ILogger<AllowedActionsAuthenticationHandler> _logger;
    private readonly IOptionsMonitor<AllowedActionsAuthenticationSchemeOptions> _options;

    public AllowedActionsAuthenticationHandler(ILogger<AllowedActionsAuthenticationHandler> logger,
        IOptionsMonitor<AllowedActionsAuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder) : base(options, loggerFactory, encoder)
    {
        _logger = logger;
        _options = options;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>();

        if (Request.Headers.TryGetValue(_options.CurrentValue.ActionHeaderName, out var headerValues))
        {
            foreach (var value in headerValues)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            claims.Add(new Claim(AllowedActionsAuthenticationConstants.AllowedActionClaimType, token));
                        }
                    }
                }
            }
        }

        var identity = new ClaimsIdentity(claims, AllowedActionsAuthenticationConstants.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AllowedActionsAuthenticationConstants.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
