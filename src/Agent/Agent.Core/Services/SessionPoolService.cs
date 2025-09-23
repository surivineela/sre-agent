// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Session;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

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

    /// <summary>
    /// Builds a stable session identifier for the code interpreter pool using the agent name and thread id.
    /// This ensures all python/report generation code for the same (agent, thread) reuses the same underlying session when supported by the pool.
    /// </summary>
    public static string BuildSessionIdentifier(string agentName, string threadId)
    {
        if (string.IsNullOrWhiteSpace(agentName)) throw new ArgumentException("agentName required", nameof(agentName));
        if (string.IsNullOrWhiteSpace(threadId)) throw new ArgumentException("threadId required", nameof(threadId));
        // Normalize thread id (strip braces if Guid string)
        threadId = threadId.Trim().Trim('{', '}');
        return $"{agentName}-{threadId}";
    }

    public async Task<(int, string, string)> ExecuteCliLegacyAsync(string command, string accessToken, string identifier)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("Access token must be provided to execute CLI commands.");
        }

        if (string.IsNullOrEmpty(identifier))
        {
            throw new InvalidOperationException("Identifier must be provided to execute CLI commands.");
        }

        _logger.LogInternalInformation($"Executing CLI command: {command} with identifier {identifier}");

        command = command.StartsWith("az ", StringComparison.OrdinalIgnoreCase) ? command : $"az {command}";
        var finalCommand = $"export AZURE_CLI_ACCESS_TOKEN={accessToken} && {command}".Trim();

        var req = new SessionRequest
        {
            Commands = ["/bin/bash", "-c", finalCommand],
            TimeoutInSeconds = 900 // 15 minutes
        };
        var sessionResponse = await ExecuteShellCommandAsync<SessionRequest, SessionResponse>(req, identifier);

        return (sessionResponse.ExitCode ?? -1, sessionResponse.Result?.Stdout ?? string.Empty, sessionResponse.Result?.Stderr ?? string.Empty);
    }

    public async Task<(int, string, string)> ExecuteCliAsync(string command, string identifier, Dictionary<string, string>? tokens)
    {
        if (tokens == null || tokens.Count == 0)
        {
            throw new InvalidOperationException("Access token must be provided to execute CLI commands.");
        }

        if (string.IsNullOrEmpty(identifier))
        {
            throw new InvalidOperationException("Identifier must be provided to execute CLI commands.");
        }

        _logger.LogInternalInformation($"Executing CLI command: {command} with identifier {identifier}. Token scopes: {string.Join(", ", tokens.Keys)}");

        var req = new AzCliExecutionRequest
        {
            ShellScripts = command,
            AccessTokens = tokens,
            TimeoutInSeconds = 900 // 15 minutes
        };

        var resp = await ExecuteShellCommandAsync<AzCliExecutionRequest, ShellExecuteResponse>(req, identifier);

        return (resp.ExitCode ?? -1, resp.Result?.Stdout ?? string.Empty, resp.Result?.Stderr ?? string.Empty);
    }

    public async Task<TResp> ExecuteShellCommandAsync<TReq, TResp>(TReq request, string identifier)
        where TReq : SessionRequest
        where TResp : SessionResponse
    {
        var path = request switch
        {
            AzCliExecutionRequest => "/shellexecute/azcli",
            KubectlExecutionRequest => "/shellexecute/kubectl",
            SessionRequest => "/shellExecute",
            _ => throw new InvalidOperationException("Unsupported shell execute request type.")
        };

        var sessionResponse = await SendRequestAsync<TReq, TResp>(HttpMethod.Post, path, identifier, request);
        _logger.LogInternalInformation($"Shell command executed successfully, identifier {identifier}, exit code {sessionResponse.ExitCode}, ExecutionTimeInMilliseconds {sessionResponse.Result?.ExecutionTimeInMilliseconds}");

        return sessionResponse;
    }

    public async Task<SessionResponse> ExecuteShellCommandInCodeInterpreterPoolAsync(string command, string identifier, int timeoutSeconds)
    {
        _logger.LogInternalInformation($"Executing bash command in Code Interpreter pool. Identifier={identifier} Timeout={timeoutSeconds}s");
        var sessionRequest = new SessionRequest
        {
            Commands = ["/bin/bash", "-c", command],
            TimeoutInSeconds = Math.Clamp(timeoutSeconds, 5, 900)
        };

        var sessionResponse = await SendRequestAsync<SessionRequest, SessionResponse>(HttpMethod.Post, "/shellExecute", identifier, sessionRequest, useCodeInterpreterPool: true);
        _logger.LogInternalInformation($"Code Interpreter execution complete. Identifier={identifier} ExitCode={sessionResponse.ExitCode} ExecMs={sessionResponse.Result?.ExecutionTimeInMilliseconds}");
        return sessionResponse;
    }

    public async Task<CodeExecutionResponse> ExecutePythonInlineAsync(string code, string identifier, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code required", nameof(code));

        // Build endpoint (force code interpreter pool)
        var baseEndpoint = !string.IsNullOrWhiteSpace(_sessionPoolSettings.CodeInterpreterPoolManagementEndpoint)
            ? _sessionPoolSettings.CodeInterpreterPoolManagementEndpoint
            : _sessionPoolSettings.PoolManagementEndpoint;

        var url = $"{baseEndpoint.TrimEnd('/')}/executions?identifier={identifier}&api-version={_defaultApiVersion}";

        var payload = new CodeExecuteRequestProperties
        {
            Code = code,
            CodeInputType = "inline",
            ExecutionType = "synchronous"
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        _logger.LogInternalInformation($"POST code/execute (identifier={identifier}) bodySize={code.Length}");
        try
        {
            var client = _httpClientFactory.CreateClient(Constants.HttpClientForSessionPool);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 900)));
            using var response = await client.SendAsync(request, cts.Token);
            var content = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInternalError($"code/execute error {(int)response.StatusCode} {response.ReasonPhrase}: {content}");
                response.EnsureSuccessStatusCode();
            }
            return CodeExecutionResponse.Parse(content);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed executing code/execute");
            throw;
        }
    }

    public async Task<byte[]> DownloadSessionFileAsync(string identifier, string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentException("filename required", nameof(filename));
        if (filename.Contains("..")) throw new InvalidOperationException("path traversal not allowed");

        var baseEndpoint = !string.IsNullOrWhiteSpace(_sessionPoolSettings.CodeInterpreterPoolManagementEndpoint)
            ? _sessionPoolSettings.CodeInterpreterPoolManagementEndpoint
            : _sessionPoolSettings.PoolManagementEndpoint;

        if (!filename.StartsWith("mnt/data/"))
        {
            filename.Replace("mnt/data/", "");
        }

        var url = $"{baseEndpoint.TrimEnd('/')}/python/downloadFile?fileName={filename}&identifier={identifier}&api-version=2024-02-02-preview";
        _logger.LogInternalInformation($"Downloading session file '{filename}' (identifier={identifier})");
        try
        {
            var client = _httpClientFactory.CreateClient(Constants.HttpClientForSessionPool);
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Download failed {(int)response.StatusCode} {response.ReasonPhrase}: {err}");
                response.EnsureSuccessStatusCode();
            }
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed downloading session file");
            throw;
        }
    }

    private async Task<TResp> SendRequestAsync<TReq, TResp>(HttpMethod method, string path, string identifier, TReq sessionRequest, bool useCodeInterpreterPool = false)
    {
        // Choose endpoint: prefer dedicated code interpreter pool if requested & configured
        var baseEndpoint = useCodeInterpreterPool && !string.IsNullOrWhiteSpace(_sessionPoolSettings.CodeInterpreterPoolManagementEndpoint)
            ? _sessionPoolSettings.CodeInterpreterPoolManagementEndpoint
            : _sessionPoolSettings.PoolManagementEndpoint;

        var url = $"{baseEndpoint.TrimEnd('/')}{path}?identifier={identifier}&api-version={_defaultApiVersion}";

        _logger.LogInternalInformation($"Sending {method} request to session pool endpoint {url}");

        try
        {
            var client = _httpClientFactory.CreateClient(Constants.HttpClientForSessionPool);
            var request = new HttpRequestMessage(method, url);

            if (sessionRequest != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(sessionRequest, sessionRequest.GetType()), Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Session pool endpoint returned error: {(int)response.StatusCode} {response.ReasonPhrase}, Content: {errorContent}");
                response.EnsureSuccessStatusCode();
            }

            var content = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<TResp>(content, new JsonSerializerOptions
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
