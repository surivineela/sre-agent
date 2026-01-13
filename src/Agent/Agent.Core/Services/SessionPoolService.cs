// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Common.ApiModels;
using Agent.Common.ApiModels.Session;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

public class SessionPoolService : ISessionPoolService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SessionPoolSettings _sessionPoolSettings;
    private readonly FederationSettings _federationSettings;
    private readonly IManagedIdentityConfigService _managedIdentityConfigService;
    private readonly string? _defaultIdentityResourceId;
    private readonly ILogger<SessionPoolService> _logger;
    private readonly string _defaultApiVersion = "2025-02-02-preview";

    public SessionPoolService(
        ILogger<SessionPoolService> logger,
        IHttpClientFactory httpClientFactory,
        IManagedIdentityConfigService managedIdentityConfigService,
        AzureSettings azureSettings)
    {
        _httpClientFactory = httpClientFactory;
        _sessionPoolSettings = azureSettings.SessionPool;
        _federationSettings = azureSettings.Federation;
        _managedIdentityConfigService = managedIdentityConfigService;
        _defaultIdentityResourceId = string.IsNullOrEmpty(azureSettings.Action.Identity) ? azureSettings.Crawler.Identity : azureSettings.Action.Identity;
        _logger = logger;
    }

    // Client Id must be the first part of the identifer as this is a contract between YARP for authorization
    public string BuildSessionIdentifier(string? agentName = null, string? threadId = null, bool randomSuffix = true)
    {
        var clientId = _federationSettings.ClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            clientId = "local";
        }
        List<string> parts = [clientId];
        if (!string.IsNullOrEmpty(agentName))
        {
            parts.Add(agentName);
        }
        if (!string.IsNullOrEmpty(threadId))
        {
            threadId = threadId.Trim().Trim('{', '}');
            parts.Add(threadId);
        }
        if (randomSuffix)
        {
            parts.Add(Guid.NewGuid().ToString("N").Substring(0, 8));
        }
        return string.Join("--", parts);
    }

    public async Task<(int, string, string)> ExecuteCliAsync(string command, string identifier, Dictionary<string, string>? tokens, string? identityResourceId = null)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            throw new InvalidOperationException("Identifier must be provided to execute CLI commands.");
        }

        await BootstrapSessionAsync(identifier, tokens, identityResourceId);

        _logger.LogInternalInformation($"Executing CLI command: {command} with identifier {identifier}.");

        var req = new AzCliExecutionRequest
        {
            ShellScripts = command,
            AccessTokens = new Dictionary<string, string>(), // Empty tokens - This is a required legacy property that will be removed in the future
            TimeoutInSeconds = (int)Constants.AzCliDefaultTimeout.TotalSeconds
        };

        var resp = await ExecuteShellCommandAsync<AzCliExecutionRequest, ShellExecuteResponse>(req, identifier);
        return (resp.ExitCode ?? -1, resp.Result?.Stdout ?? string.Empty, resp.Result?.Stderr ?? string.Empty);
    }

    /// <summary>
    /// Bootstraps the session with managed identity and tokens.
    /// This is called for every session request to ensure the session has the latest identity and tokens.
    /// </summary>
    private async Task BootstrapSessionAsync(string identifier, Dictionary<string, string>? tokens, string? identityResourceId)
    {
        var tokenScopes = tokens != null ? string.Join(", ", tokens.Keys) : "none";
        _logger.LogInternalInformation($"Bootstrapping session {identifier} with tokens scopes=[{tokenScopes}], identityResourceId={identityResourceId ?? "null"}");

        var bootstrapRequest = await BuildBootstrapRequestAsync(tokens, identityResourceId);
        await SendBootstrapRequestAsync(identifier, bootstrapRequest);

        _logger.LogInternalInformation($"Session {identifier} bootstrapped successfully.");
    }

    /// <summary>
    /// Builds the bootstrap request with managed identity info loaded from config and tokens.
    /// </summary>
    /// <param name="tokens">Optional tokens to include in the bootstrap request.</param>
    /// <param name="identityResourceId">The ARM resource ID of the managed identity to use, or null for system-assigned.</param>
    private async Task<BootstrapRequest> BuildBootstrapRequestAsync(Dictionary<string, string>? tokens, string? identityResourceId)
    {
        var request = new BootstrapRequest
        {
            Tokens = tokens ?? new Dictionary<string, string>()
        };

        // Load managed identity info using the provided identity resource ID
        var managedIdentityInfo = await _managedIdentityConfigService.GetManagedIdentityInfoAsync(identityResourceId);
        if (managedIdentityInfo != null)
        {
            request.ManagedIdentity = managedIdentityInfo;
        }

        return request;
    }

    /// <summary>
    /// Sends the bootstrap request to the session pool.
    /// </summary>
    private async Task SendBootstrapRequestAsync(string identifier, BootstrapRequest request)
    {
        var baseEndpoint = _sessionPoolSettings.PoolManagementEndpoint;
        var url = $"{baseEndpoint.TrimEnd('/')}/bootstrap?identifier={identifier}&api-version={_defaultApiVersion}";

        _logger.LogInternalInformation($"Sending bootstrap request to {url}");

        try
        {
            var client = _httpClientFactory.CreateClient(Constants.HttpClientForSessionPool);
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await client.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInternalError($"Bootstrap request failed: {(int)response.StatusCode} {response.ReasonPhrase}: {content}");
                throw new InvalidOperationException($"Bootstrap request failed: {response.StatusCode}");
            }

            _logger.LogInternalInformation($"Bootstrap response: {content}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error sending bootstrap request to session pool.");
            throw;
        }
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

        await BootstrapSessionAsync(identifier, tokens: null, identityResourceId: _defaultIdentityResourceId);

        var sessionRequest = new SessionRequest
        {
            Commands = ["/bin/bash", "-c", command],
            TimeoutInSeconds = Math.Clamp(timeoutSeconds, 5, 900)
        };

        var sessionResponse = await SendRequestAsync<SessionRequest, SessionResponse>(HttpMethod.Post, "/shellExecute", identifier, sessionRequest);
        _logger.LogInternalInformation($"Code Interpreter execution complete. Identifier={identifier} ExitCode={sessionResponse.ExitCode} ExecMs={sessionResponse.Result?.ExecutionTimeInMilliseconds}");
        return sessionResponse;
    }

    public async Task<CodeExecutionResponse> ExecutePythonInlineAsync(string code, string identifier, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code required", nameof(code));

        await BootstrapSessionAsync(identifier, tokens: null, identityResourceId: _defaultIdentityResourceId);

        var payload = new CodeExecuteRequest
        {
            Code = code,
            TimeoutInSeconds = Math.Clamp(timeoutSeconds, 5, 900),
            ExecutionType = "synchronous",
            StandardMsgLength = 24576,
            EnableEgress = true,
        };

        _logger.LogInternalInformation($"POST code/execute with identifier={identifier}, bodySize={code.Length}");

        return await SendRequestAsync<CodeExecuteRequest, CodeExecutionResponse>(HttpMethod.Post, "/execute", identifier, payload);
    }

    public async Task<byte[]> DownloadSessionFileAsync(string identifier, string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentException("filename required", nameof(filename));
        if (filename.Contains("..")) throw new InvalidOperationException("path traversal not allowed");

        var baseEndpoint = _sessionPoolSettings.PoolManagementEndpoint;

        if (!filename.StartsWith("mnt/data/"))
        {
            filename.Replace("mnt/data/", "");
        }

        var url = $"{baseEndpoint.TrimEnd('/')}/files/content/{UrlEncoder.Default.Encode(filename)}?identifier={identifier}&api-version=2024-02-02-preview";
        _logger.LogInternalInformation($"Downloading session file '{filename}' from session pool endpoint {url}");
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

    public async Task<string> ListSessionFilesAsync(string identifier)
    {
        var baseEndpoint = _sessionPoolSettings.PoolManagementEndpoint;

        var url = $"{baseEndpoint.TrimEnd('/')}/files?identifier={identifier}&api-version=2024-02-02-preview";
        _logger.LogInternalInformation($"Listing session files from session pool endpoint {url}");
        try
        {
            var client = _httpClientFactory.CreateClient(Constants.HttpClientForSessionPool);
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"List files failed {(int)response.StatusCode} {response.ReasonPhrase}: {err}");
                response.EnsureSuccessStatusCode();
            }
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed listing session files");
            throw;
        }
    }

    public async Task UploadSessionFileAsync(string identifier, string filename, byte[] fileContent, string? destinationPath = null)
    {
        if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentException("filename required", nameof(filename));
        if (fileContent == null || fileContent.Length == 0) throw new ArgumentException("fileContent required", nameof(fileContent));
        if (filename.Contains("..")) throw new InvalidOperationException("path traversal not allowed");

        var baseEndpoint = _sessionPoolSettings.PoolManagementEndpoint;

        var urlBuilder = new StringBuilder($"{baseEndpoint.TrimEnd('/')}/files?identifier={identifier}&api-version={_defaultApiVersion}");
        if (!string.IsNullOrWhiteSpace(destinationPath))
        {
            urlBuilder.Append($"&path={Uri.EscapeDataString(destinationPath)}");
        }
        var url = urlBuilder.ToString();

        _logger.LogInternalInformation($"Uploading session file '{filename}' (identifier={identifier}, size={fileContent.Length} bytes)");
        try
        {
            var client = _httpClientFactory.CreateClient(Constants.HttpClientForSessionPool);
            using var content = new MultipartFormDataContent();
            using var fileStreamContent = new ByteArrayContent(fileContent);
            fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileStreamContent, "file", filename);

            using var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Upload failed {(int)response.StatusCode} {response.ReasonPhrase}: {err}");
                response.EnsureSuccessStatusCode();
            }

            _logger.LogInternalInformation($"File uploaded successfully: {filename} ({fileContent.Length} bytes)");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed uploading session file");
            throw;
        }
    }

    private static readonly JsonSerializerOptions s_requestSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions s_responseSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task<TResp> SendRequestAsync<TReq, TResp>(HttpMethod method, string path, string identifier, TReq sessionRequest)
    {
        // Choose endpoint
        var baseEndpoint = _sessionPoolSettings.PoolManagementEndpoint;

        var url = $"{baseEndpoint.TrimEnd('/')}{path}?identifier={identifier}&api-version={_defaultApiVersion}";

        _logger.LogInternalInformation($"Sending {method} request to session pool endpoint {url}");

        try
        {
            var client = _httpClientFactory.CreateClient(Constants.HttpClientForSessionPool);
            var request = new HttpRequestMessage(method, url);

            if (sessionRequest != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(sessionRequest, sessionRequest.GetType(), s_requestSerializerOptions), Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Session pool endpoint returned error: {(int)response.StatusCode} {response.ReasonPhrase}, Content: {errorContent}");
                response.EnsureSuccessStatusCode();
            }

            var content = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<TResp>(content, s_responseSerializerOptions)!;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("An error occurred while sending request to session pool endpoint", ex);
            throw;
        }
    }
}
