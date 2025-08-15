// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

public class SessionRequest
{
    public List<string> Commands { get; set; } = [];

    public int TimeoutInSeconds { get; set; } = 30;
}

public class SessionPoolService : ISessionPoolService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SessionPoolSettings _sessionPoolSettings;
    private readonly ILogger<SessionPoolService> _logger;
    private readonly string _defaultApiVersion = "2025-02-02-preview";

    public SessionPoolService(
        ILogger<SessionPoolService> logger,
        IHttpClientFactory httpClientFactory,
        AzureSettings azureSettings)
    {
        _httpClientFactory = httpClientFactory;
        _sessionPoolSettings = azureSettings.SessionPool;
        _logger = logger;
    }

    public async Task<SessionResponse> ExecuteCliAsync(string command, string accessToken, string identifier)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("Access token must be provided to execute CLI commands.");
        }

        if (string.IsNullOrEmpty(identifier)) {
            throw new InvalidOperationException("Identifier must be provided to execute CLI commands.");
        }

        _logger.LogInternalInformation($"Executing CLI command: {command}");

        command = command.StartsWith("az ", StringComparison.OrdinalIgnoreCase) ? command : $"az {command}";
        var finalCommand = $"export AZURE_CLI_ACCESS_TOKEN={accessToken} && {command}".Trim();

        return await ExecuteShellCommandAsync(finalCommand, identifier);
    }

    public async Task<SessionResponse> ExecuteShellCommandAsync(string command, string identifier)
    {
        _logger.LogInternalInformation($"Executing shell command for identifier {identifier}");
        var sessionRequest = new SessionRequest
        {
            Commands = ["/bin/bash", "-c", command],
            TimeoutInSeconds = 30
        };

        return await SendRequestAsync<SessionResponse>(HttpMethod.Post, "/shellExecute", identifier, sessionRequest);
    }

    private async Task<T> SendRequestAsync<T>(HttpMethod method, string path, string identifier, SessionRequest? sessionRequest = null)
    {
        var url = $"{_sessionPoolSettings.PoolManagementEndpoint.TrimEnd('/')}{path}?identifier={identifier}&api-version={_defaultApiVersion}";

        _logger.LogInternalInformation($"Sending {method} request to session pool endpoint {url}");

        try
        {
            var client = _httpClientFactory.CreateClient(Constants.HttpClientForSessionPool);
            var request = new HttpRequestMessage(method, url);

            if (sessionRequest != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(sessionRequest), Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Session pool endpoint returned error: {(int)response.StatusCode} {response.ReasonPhrase}, Content: {errorContent}");
                response.EnsureSuccessStatusCode();
            }

            var content = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("An error occurred while sending request to session pool endpoint", ex);
            throw;
        }
    }
}
