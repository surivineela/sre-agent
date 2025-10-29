// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using System.Text.Json;
using Agent.Core.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;
public class IcmApiAgentSpaceTokenAuthService : IIcmTokenAuthService
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ICMAgentSpaceAuthOptions _authOptions;
    private readonly ILogger<IcmApiAgentSpaceTokenAuthService> _logger;

    public IcmApiAgentSpaceTokenAuthService(IAuthenticationService authenticationService, ICMAgentSpaceAuthOptions authOptions, ILogger<IcmApiAgentSpaceTokenAuthService> logger)
    {
        _authenticationService = authenticationService;
        _authOptions = authOptions;
        _logger = logger;
    }

    public static bool IsAgentSpaceProxyConfigured(ICMAgentSpaceAuthOptions authOptions)
    {
        return authOptions is not null
            && !string.IsNullOrWhiteSpace(authOptions.AgentSpaceProxyEndpoint)
            && !string.IsNullOrWhiteSpace(authOptions.Scope)
            && !string.IsNullOrWhiteSpace(authOptions.ResourceId);
    }

    public async ValueTask<AccessToken> GetAccessTokenAsync()
    {
        try
        {
            _logger.LogInternalInformation("[ICMAPIAgentSpaceTokenAuthService] Attempting to acquire token via AgentSpaceProxy.");
            var token = await _authenticationService.GetTokenFromAgentSpaceProxy(_authOptions.Scope, _authOptions.ResourceId);
            _logger.LogInternalInformation("[ICMAPIAgentSpaceTokenAuthService] Successfully acquired token via AgentSpaceProxy.");
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[ICMAPIAgentSpaceTokenAuthService] ERROR: Failed to get token from AgentSpaceProxy: {ex}, ICMAgentSpaceAuthOptions: {JsonSerializer.Serialize(_authOptions)}");
            throw new InvalidOperationException($"Failed to get token from Agent Space Proxy: {ex.Message}", ex);
        }
    }
}

public class ICMAgentSpaceAuthOptions
{
    public required string AgentSpaceProxyEndpoint { get; set; }
    public required string Scope { get; set; }
    public required string ResourceId { get; set; }
}
