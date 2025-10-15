// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Azure.Core;
using Azure.Identity;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;

namespace Agent.Plugins.Implementation;

public class CannotConnectToVmPlugin : ICannotConnectToVmPlugin
{
    private static readonly Uri ManagementRoot = new("https://management.azure.com/");
    private const string ApiVersion = "2024-09-01-preview";
    private const string BearerTokenHeaderName = "Bearer";
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FixedPollInterval = TimeSpan.FromSeconds(2); // fixed interval
    // Cache loaded markdown to avoid repeated disk IO
    private static readonly ConcurrentDictionary<string, string> _kbMarkdownCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] KnowledgeBaseSubPaths =
    [
        Path.Combine("Agent.Runtime", "KnowledgeBase", "VmConnectivity"),          // source tree relative
        Path.Combine("KnowledgeBase", "VmConnectivity")                          // fallback pattern
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<CannotConnectToVmPlugin> _logger;
    private readonly Core.Interfaces.IAuthenticationService _authService;
    private static long _instanceSequence = 0;

    public CannotConnectToVmPlugin(IHttpClientFactory httpClientFactory,
        ILogger<CannotConnectToVmPlugin> logger,
        Core.Interfaces.IAuthenticationService authService)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(CannotConnectToVmPlugin));
        _logger = logger ?? NullLogger<CannotConnectToVmPlugin>.Instance;
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public Guid? ThreadId { get; set; }

    public Task<string> AnalyzeVmScreenshotAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Starting screenshot analysis ThreadId={ThreadId} ResourceId={ResourceId}", ThreadId, resourceId);
        return ExecuteAnalysisAsync(
            resourceId,
            pluginType: "ComputeScreenshotAnalyzerPlugin",
            functionName: "AnalyzeScreenshot",
            prompt: "Help me analyze the Screenshot of this Azure VM",
            cancellationToken);
    }

    public Task<string> AnalyzeVmSerialLogAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Starting serial log analysis ThreadId={ThreadId} ResourceId={ResourceId}", ThreadId, resourceId);
        return ExecuteAnalysisAsync(
            resourceId,
            pluginType: "ComputeSerialLogAnalyzerPlugin",
            functionName: "AnalyzeSerialLog",
            prompt: "Help me analyze the serial log of this Azure VM",
            cancellationToken);
    }
        

    public async Task<string> DiagnoseVmConnectivityIssuesAsync(string resourceId, string osType, string? tsgFileName, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Starting Cannot Connect to Vm scenario, ThreadId={ThreadId} ResourceId={ResourceId} osType={osType}", ThreadId, resourceId, osType);


        if (ThreadId is null)
            throw new InvalidOperationException("ThreadId must be set before invoking cannot connect to vm scenario.");
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("resourceId is required.", nameof(resourceId));

        _logger.LogInternalInformation("Starting connectivity to cannot connect to vm scenario ThreadId={ThreadId} ResourceId={ResourceId}", ThreadId, resourceId);

        if (!string.IsNullOrWhiteSpace(tsgFileName))
        {
            var guidance = TryResolveErrorGuidance(tsgFileName);
            if (guidance is not null)
            {
                return guidance;
            }
        }

        var screenshot = await AnalyzeVmScreenshotAsync(resourceId, cancellationToken).ConfigureAwait(false);
        if (osType.Trim().Equals("Linux", StringComparison.OrdinalIgnoreCase))
        {
            var serial = await AnalyzeVmSerialLogAsync(resourceId, cancellationToken).ConfigureAwait(false);
            return $"{screenshot}\n {serial}";
        }
        return screenshot;
    }

    #region Core Flow

    private async Task<string> ExecuteAnalysisAsync(
        string resourceId,
        string pluginType,
        string functionName,
        string prompt,
        CancellationToken externalToken)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("resourceId cannot be null or empty.", nameof(resourceId));

        if (ThreadId is null)
            throw new InvalidOperationException("ThreadId must be set before invoking Cannot Connect to Vm tools.");

        var normalizedId = resourceId.TrimStart('/');

        using var timeoutCts = new CancellationTokenSource(OperationTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, externalToken);
        var token = linkedCts.Token;

        // Generate a unique plugin instance name per invocation (not the ThreadId)
        // Format: <threadShort>-<seq>-<guid6>
        var pluginInstanceId = GeneratePluginInstanceId(ThreadId.Value);

        var endpoint = BuildPluginUri(normalizedId, pluginInstanceId);
        var payload = BuildRequestPayload(prompt, pluginType, functionName, ThreadId.Value);

        await StartOperationAsync(endpoint, payload, token).ConfigureAwait(false);

        var body = await PollForTerminalAsync(endpoint, token).ConfigureAwait(false);
        return ExtractFinalResult(body);
    }

    #endregion

    #region HTTP + Polling

    private async Task StartOperationAsync(Uri endpoint, string jsonBody, CancellationToken token)
    {
        int attempt = 0;
        const int maxAttempts = 3;

        while (true)
        {
            token.ThrowIfCancellationRequested();
            attempt++;
            _logger.LogInternalInformation("PUT attempt {Attempt} endpoint={Endpoint} payloadBytes={Size}", attempt, endpoint, jsonBody.Length);

            using var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            var cred = await _authService.GetArmOperationCredential();
            var accessToken = await cred.GetTokenAsync(new TokenRequestContext([Constants.DefaultOboTokenScope]), default);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

            // Avoid duplicate if retry loop regenerates request
            if (!request.Headers.Contains("x-ms-request-id"))
            {
                request.Headers.TryAddWithoutValidation("x-ms-request-id", ThreadId.ToString());
            }

            if (!request.Headers.Contains("x-ms-client-agent"))
            {
                request.Headers.TryAddWithoutValidation("x-ms-client-agent", "SREAgent");
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInternalInformation("PUT success endpoint={Endpoint} attempt={Attempt}", endpoint, attempt);
                return;
            }

            var body = await SafeReadAsync(response).ConfigureAwait(false);

            // Retry on conflict due to duplicate resource name.
            if (response.StatusCode == HttpStatusCode.Conflict &&
                body.Contains("Not unique", StringComparison.OrdinalIgnoreCase) &&
                attempt < maxAttempts)
            {
                // Generate new plugin id & rebuild endpoint
                _logger.LogInternalWarning("Conflict duplicate plugin id attempt={Attempt} regenerating id", attempt);

                var newId = GeneratePluginInstanceId(ThreadId ?? Guid.NewGuid());
                endpoint = RebuildWithNewPluginId(endpoint, newId);
                continue;
            }

            _logger.LogInternalError("PUT failure status={Status} reason={Reason} attempt={Attempt} bodyLen={BodyLen}", (int)response.StatusCode, response.ReasonPhrase, attempt, body?.Length ?? 0);
            throw new HttpRequestException(
                $"PUT {endpoint} failed {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}",
                null,
                response.StatusCode);
        }
    }

    private async Task<string> PollForTerminalAsync(Uri endpoint, CancellationToken token)
    {
        var start = DateTime.UtcNow;
        int attempt = 0;

        while (true)
        {
            token.ThrowIfCancellationRequested();
            attempt++;

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            var cred = await _authService.GetArmOperationCredential();
            var accessToken = await cred.GetTokenAsync(new TokenRequestContext([Constants.DefaultOboTokenScope]), default);

            request.Headers.Authorization = new AuthenticationHeaderValue(BearerTokenHeaderName, accessToken.Token);

            // Avoid duplicate if retry loop regenerates request
            if (!request.Headers.Contains("x-ms-request-id"))
            {
                request.Headers.TryAddWithoutValidation("x-ms-request-id", ThreadId.ToString());
            }

            if (!request.Headers.Contains("x-ms-client-agent"))
            {
                request.Headers.TryAddWithoutValidation("x-ms-client-agent", "SREAgent");
            }

            HttpResponseMessage? response = null;
            string? body = null;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, token)
                    .ConfigureAwait(false);
                body = await SafeReadAsync(response).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (TryGetProvisioningState(body, out var state) && IsTerminal(state))
                    {
                        return body;
                    }
                }
                else if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Accepted)
                {
                    // still initializing
                }
                else
                {
                    throw new HttpRequestException(
                        $"GET {endpoint} failed {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}",
                        null,
                        response.StatusCode);
                }
            }
            finally
            {
                response?.Dispose();
            }

            if (DateTime.UtcNow - start > OperationTimeout)
            {
                throw new TimeoutException(
                    $"Polling timed out after {OperationTimeout.TotalSeconds:F0}s ({attempt} attempts).");
            }

            await Task.Delay(FixedPollInterval, token).ConfigureAwait(false);
        }
    }

    #endregion

    #region Helpers

    private static string GeneratePluginInstanceId(Guid threadId)
    {
        var seq = Interlocked.Increment(ref _instanceSequence);
        var shortThread = threadId.ToString("N")[..8];
        var shortGuid = Guid.NewGuid().ToString("N")[..6];
        return $"{shortThread}-{seq}-{shortGuid}";
    }

    private static Uri BuildPluginUri(string normalizedResourceId, string pluginInstanceId)
    {
        var sb = new StringBuilder()
            .Append(normalizedResourceId.TrimEnd('/'))
            .Append("/providers/Microsoft.Help/plugins/")
            .Append(pluginInstanceId)
            .Append("?api-version=")
            .Append(ApiVersion);
        return new Uri(ManagementRoot, sb.ToString());
    }

    private static Uri RebuildWithNewPluginId(Uri oldEndpoint, string newPluginId)
    {
        // Old format: .../providers/Microsoft.Help/plugins/<oldId>?api-version=...
        // Replace the segment after /plugins/
        var original = oldEndpoint.ToString();
        var idx = original.IndexOf("/providers/Microsoft.Help/plugins/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return oldEndpoint;

        var prefix = original[..(idx + "/providers/Microsoft.Help/plugins/".Length)];
        var after = original[(idx + "/providers/Microsoft.Help/plugins/".Length)..];
        var queryIdx = after.IndexOf("?api-version=", StringComparison.OrdinalIgnoreCase);
        var suffix = queryIdx >= 0 ? after[queryIdx..] : string.Empty;
        return new Uri(prefix + newPluginId + suffix);
    }

    private static string BuildRequestPayload(string prompt, string pluginType, string functionName, Guid threadId)
    {
        var json = new
        {
            Properties = new
            {
                prompt = prompt,
                PluginType = pluginType,
                FunctionName = functionName,
                Context = string.Empty
            }
        };
        return JsonSerializer.Serialize(json, SerializerOptions);
    }

    private static bool TryGetProvisioningState(string body, out string state)
    {
        state = string.Empty;
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            var node = JsonNode.Parse(body);
            var ps = node?["properties"]?["provisioningState"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(ps))
            {
                state = ps;
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool IsTerminal(string provisioningState) =>
        provisioningState.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) ||
        provisioningState.Equals("Failed", StringComparison.OrdinalIgnoreCase);

    private static string ExtractFinalResult(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return body;

        try
        {
            var node = JsonNode.Parse(body);
            var completionText = node?["properties"]?["completionText"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(completionText))
                return body;

            try
            {
                using var doc = JsonDocument.Parse(completionText);
                bool? ruleMatched = null;
                if (doc.RootElement.TryGetProperty("RuleMatched", out var rm))
                {
                    if (rm.ValueKind == JsonValueKind.True) ruleMatched = true;
                    else if (rm.ValueKind == JsonValueKind.False) ruleMatched = false;
                }

                if (ruleMatched.HasValue)
                {
                    if (ruleMatched.Value == false)
                    {
                        // Rule explicitly not matched -> generic message.
                        return "No issues were found with the VM. HANDOFF: meta_agent (Plugin did not detect a connectivity cause; continue broader investigation).";
                    }

                    // RuleMatched == true -> return the meaningful content.
                    if (doc.RootElement.TryGetProperty("Llm_response", out var lrMatched) &&
                        lrMatched.ValueKind == JsonValueKind.String)
                    {
                        return "Result from Serial Log Analyzer Plugin: " + (lrMatched.GetString() ?? string.Empty);
                    } 
                    else if (doc.RootElement.TryGetProperty("Answer", out var ans) &&
                        ans.ValueKind == JsonValueKind.String)
                    {
                        return "Result from Screen Shot Analysis Plugin: " + (ans.GetString() ?? string.Empty);
                    }

                    // If no Llm_response or Answer, return raw completionText as-is.
                    return completionText;
                }

                // Backward compatibility (no RuleMatched field present)
                if (doc.RootElement.TryGetProperty("Llm_response", out var lr) &&
                    lr.ValueKind == JsonValueKind.String)
                {
                    return "Result from CannotConnectToVmPlugin: " + lr.GetString() ?? completionText;
                }
            }
            catch
            {
                // ignore inner parse failures
            }

            return completionText;
        }
        catch
        {
            return body;
        }
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response)
    {
        try { return await response.Content.ReadAsStringAsync().ConfigureAwait(false); }
        catch { return string.Empty; }
    }

    #endregion


    private string? TryResolveErrorGuidance(string tsgFileName)
    {

        var content = TryLoadKnowledgeBaseMarkdown(tsgFileName);
        if (content is not null)
        {
            return content; // Return full markdown content
        }
        return $"Known issue: {tsgFileName}";
    }

    private string? TryLoadKnowledgeBaseMarkdown(string docId)
    {
        if (string.IsNullOrWhiteSpace(docId))
            return null;

        // Allow only safe filename chars
        foreach (var ch in docId)
        {
            if (!(char.IsLetterOrDigit(ch) || ch is '-' or '_'))
            { 
                _logger.LogInternalWarning("Rejected unsafe docId {DocId}", docId);
                return null;
            }
        }

        // Cached?
        if (_kbMarkdownCache.TryGetValue(docId, out var cached))
            return cached;

        var fileName = docId + ".md";
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);

        for (int depth = 0; depth < 6 && dir != null; depth++, dir = dir.Parent)
        {
            foreach (var sub in KnowledgeBaseSubPaths)
            {
                var candidate = Path.Combine(dir.FullName, sub, fileName);
                try
                {
                    if (File.Exists(candidate))
                    {
                        var text = File.ReadAllText(candidate);
                        _kbMarkdownCache[docId] = text;
                        _logger.LogInternalInformation("Loaded KB markdown {DocId} from {Path}", docId, candidate);
                        return text;
                    }
                }
                catch (IOException ioEx)
                {
                    _logger.LogInternalWarning(ioEx, "IO error reading KB markdown {DocId} from {Path}", docId, candidate);
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    _logger.LogInternalWarning(uaEx, "Access denied reading KB markdown {DocId} from {Path}", docId, candidate);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Unexpected error reading KB markdown {DocId} from {Path}", docId, candidate);
                }
            }
        }

        // 2. Last resort: shallow search (avoid expensive full-recursive)
        try
        {
            dir = new DirectoryInfo(baseDir);
            var top = dir;
            for (int depth = 0; depth < 3 && top?.Parent != null; depth++)
                top = top.Parent;

            if (top is not null && top.Exists)
            {
                foreach (var sub in KnowledgeBaseSubPaths)
                {
                    var folder = new DirectoryInfo(Path.Combine(top.FullName, sub));
                    if (folder.Exists)
                    {
                        var candidate = Path.Combine(folder.FullName, fileName);
                        try
                        {
                            if (File.Exists(candidate))
                            {
                                var text = File.ReadAllText(candidate);
                                _kbMarkdownCache[docId] = text;
                                _logger.LogInternalInformation("Loaded KB markdown (fallback) {DocId} from {Path}", docId, candidate);
                                return text;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalWarning(ex, "Fallback load failed for KB markdown {DocId} at {Path}", docId, candidate);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // High-level failure in fallback probing
            _logger.LogInternalWarning(ex, "Fallback probing failed for KB markdown {DocId}", docId);
        }
        return null;
    }
}
