// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Factory for creating session-based MCP client transports.
/// Encapsulates the logic for building session pool connections with proper authentication and configuration.
/// </summary>
public class SessionTransportFactory : ISessionTransportFactory
{
    private readonly IAuthenticationService _authService;
    private readonly ISessionPoolService _sessionPoolService;
    private readonly SessionPoolSettings _sessionPoolSettings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<SessionTransportFactory> _logger;
    private static readonly Regex _unsafeToolNameChars = new("[^a-zA-Z0-9_\\.\\-]", RegexOptions.Compiled);

    public SessionTransportFactory(
        IAuthenticationService authService,
        ISessionPoolService sessionPoolService,
        SessionPoolSettings sessionPoolSettings,
        IHostEnvironment hostEnvironment,
        ILogger<SessionTransportFactory> logger)
    {
        _authService = authService;
        _sessionPoolService = sessionPoolService;
        _sessionPoolSettings = sessionPoolSettings;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IClientTransport CreateSessionTransport(string name, string command, string[] arguments, Dictionary<string, string>? envVars = null, string? identity = null)
    {
        var agentName = AgentNameHelper.GetAgentName(!_hostEnvironment.IsDevelopment());
        var sanitizedName = _unsafeToolNameChars.Replace(name, string.Empty);

        var serverUrl = _sessionPoolSettings.PoolManagementEndpoint.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/') + "/mcp/run";
        var sessionId = _sessionPoolService.BuildSessionIdentifier(agentName, null, false);

        // Get session pool credential for WebSocket authentication
        var sessionPoolCredential = _authService.GetSessionPoolCredential();

        // Define token scopes for Azure authentication (same as Azure CLI)
        var tokenScopes = new List<string>
        {
            Constants.ArmOboTokenScope,
            Constants.ArmApiTokenScope,
            Constants.AzureDevopsTokenScope,
            Constants.Ev2ProdTokenScope,
        };

        // Get data connector credential for acquiring action tokens (same as data connectors)
        TokenCredential? dataConnectorCredential = null;
        if (_hostEnvironment.IsDevelopment())
        {
            dataConnectorCredential = (TokenCredential?)_authService.GetDataConnectorCredential(new ConnectorAuthSettings
            {
                AuthenticationType = ConnectorAuthType.User
            });
        }
        else if (!string.IsNullOrEmpty(identity))
        {
            dataConnectorCredential = (TokenCredential?)_authService.GetDataConnectorCredential(new ConnectorAuthSettings
            {
                AuthenticationType = ConnectorAuthType.UAMI,
                ManagedIdentityResourceId = identity
            });
            tokenScopes.Add(Constants.FederatedIdentityTokenScope);
        }

        _logger.LogInternalDebug(
            "Creating session transport for '{Name}' with command '{Command}' via session pool. Identity: {Identity}, Token scopes: {Scopes}",
            sanitizedName,
            command,
            identity ?? "(default)",
            string.Join(", ", tokenScopes));

        return new SessionWebsocketClientTransport(new SessionWebsocketClientOptions
        {
            ServerUrl = serverUrl,
            SessionId = sessionId,
            Credential = sessionPoolCredential,
            Name = sanitizedName,
            Command = command,
            Arguments = arguments,
            EnvVars = envVars,
            TokenScopes = tokenScopes.ToArray(),
            DataConnectorCredential = dataConnectorCredential,
        }, _logger);
    }
}
