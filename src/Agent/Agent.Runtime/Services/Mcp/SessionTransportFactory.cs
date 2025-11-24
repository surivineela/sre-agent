// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Runtime.Interfaces;
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
    public IClientTransport CreateSessionTransport(string name, string command, string[] arguments)
    {
        var agentName = AgentNameHelper.GetAgentName(!_hostEnvironment.IsDevelopment());
        var threadId = Core.ToolStatic.AsyncLocalThreadId.Value.ToString();
        var sanitizedName = _unsafeToolNameChars.Replace(name, string.Empty);

        var serverUrl = _sessionPoolSettings.PoolManagementEndpoint.Replace("https://", "wss://") + "/mcp/run";
        var sessionId = _sessionPoolService.BuildSessionIdentifier(agentName, threadId, true);
        var credential = _authService.GetSessionPoolCredential();

        _logger.LogInternalDebug(
            "Creating session transport for '{Name}' with command '{Command}' via session pool",
            sanitizedName,
            command);

        return new SessionWebsocketClientTransport(new SessionWebsocketClientOptions
        {
            ServerUrl = serverUrl,
            SessionId = sessionId,
            Credential = credential,
            Name = sanitizedName,
            Command = command,
            Arguments = arguments,
        }, _logger);
    }
}
