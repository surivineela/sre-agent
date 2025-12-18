// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Exceptions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.Models;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Agent.Plugins.Implementation;

public class GenevaActionWorkflowResponse
{
    public HttpStatusCode StatusCode { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;
}

public class GenevaActionsPlugin : IGenevaActionsPlugin
{
    private readonly KustoClient _kustoClient;
    private readonly ILogger<GenevaActionsPlugin> _logger;
    private readonly GenevaActionsSettings _genevaActionsSettings;
    private readonly AgentSpaceProxySettings _agentSpaceProxySettings;
    private readonly IICMAPIClient _icmAPIClient;
    private readonly IAuthenticationService _authenticationService;
    private readonly IHostEnvironment _hostEnvironment;

    private readonly bool _icmWorkflowReadOnly;
    private const string _genevaActionSecretName = "GenevaActionConfigs";

    private readonly Lazy<Task<List<GenevaActionConfig>>> _lazyGenevaActions;
    private readonly IKeyVaultService _keyVaultService;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
    private readonly TokenCredentialHttpClientHandler _tokenCredentialHttpClientHandler;

    private readonly string _agentResourceId;

    public Guid? ThreadId { get; set; }

    private bool IsICMClientAvailable()
    {
        return _icmAPIClient is not NullableICMAPIClient;
    }

    /// <summary>
    /// Posts a discussion entry to ICM with proper error handling and availability checks.
    /// </summary>
    /// <param name="incidentId">The incident ID to post to</param>
    /// <param name="message">The message to post</param>
    /// <param name="htmlRendering">Whether to render HTML (default: true)</param>
    /// <returns>True if posting succeeded or was skipped, false if an error occurred</returns>
    private async Task<bool> TryPostDiscussionEntryAsync(string incidentId, string message, bool htmlRendering = true)
    {
        if (!IsICMClientAvailable())
        {
            _logger.LogInternalWarning($"[GenevaActionsPlugin] ICM client is not available. Skipping posting discussion entry for incident {incidentId}");
            return true; // Return true because skipping is not an error
        }

        try
        {
            await _icmAPIClient.PostDiscussionEntryAsync(incidentId, message, htmlRendering);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[GenevaActionsPlugin] Failed to post discussion entry to ICM for incident {incidentId}");
            return false;
        }
    }

    public GenevaActionsPlugin(
        IHostEnvironment hostEnvironment,
        KustoClient kustoPlugin,
        ILogger<GenevaActionsPlugin> logger,
        GenevaActionsSettings genevaActionsSettings,
        ICMWorkflowSettings iCMWorkflowSettings,
        AgentSpaceProxySettings agentSpaceProxySettings,
        IICMAPIClient iCMAPIClient,
        IKeyVaultService keyVaultService,
        IAuthenticationService authenticationService,
        IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _kustoClient = kustoPlugin;
        _genevaActionsSettings = genevaActionsSettings;
        _icmWorkflowReadOnly = iCMWorkflowSettings.ReadOnly;
        _lazyGenevaActions = new Lazy<Task<List<GenevaActionConfig>>>(() => InitializeGenevaActionsConfig());
        _icmAPIClient = iCMAPIClient;
        _keyVaultService = keyVaultService;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
        _authenticationService = authenticationService;
        _agentSpaceProxySettings = agentSpaceProxySettings;
        _agentResourceId = AgentNameHelper.GetAgentResourceId(isProd: _hostEnvironment.IsProduction());

        _tokenCredentialHttpClientHandler = new TokenCredentialHttpClientHandler(_authenticationService.GetAgentSpaceProxyCredential(), _agentSpaceProxySettings.Scope);
    }

    private async Task<List<GenevaActionConfig>> InitializeGenevaActionsConfig()
    {
        var allGenevaActions = new List<GenevaActionConfig>();
        _logger.LogInternalInformation("[GenevaActionsPlugin] Initializing Geneva Actions Config");

        if (string.IsNullOrWhiteSpace(_agentSpaceProxySettings.Endpoint))
        {
            _logger.LogInternalWarning("[GenevaActionsPlugin] Agent Space Proxy endpoint is not configured. Geneva Actions Config will not be loaded.");
            return allGenevaActions;
        }

        try
        {
            if (!_keyVaultService.IsEnabled)
            {
                _logger.LogInternalWarning("[GenevaActionsPlugin] Key Vault Service is not enabled. Geneva Actions Config will not be loaded.");
                return allGenevaActions;
            }

            var json = await _keyVaultService.ReadSecretAsync(_genevaActionSecretName);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogInternalWarning($"[GenevaActionsPlugin] Geneva Actions Config not found in Key Vault with name {_genevaActionSecretName}. Returning empty list.");
                return allGenevaActions;
            }

            var genevaActionsConfigs = JsonConvert.DeserializeObject<List<GenevaActionConfig>>(json);

            if (genevaActionsConfigs == null || genevaActionsConfigs.Count == 0)
            {
                _logger.LogInternalWarning("[GenevaActionsPlugin] No Geneva Actions Config found in CosmosDB. Returning empty list.");
                return allGenevaActions;
            }

            allGenevaActions = genevaActionsConfigs;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[GenevaActionsPlugin] Error reading alert details from CosmosDB: {ex.Message}");
        }

        return allGenevaActions;
    }

    private async Task<List<GenevaActionConfig>> GetGenevaActions()
    {
        return await _lazyGenevaActions.Value;
    }

    private async Task<GenevaActionWorkflowResponse> ExecuteGenevaActionWorkflow(string extensionName, string actionName, Dictionary<string, string> inputParameters)
    {
        HttpResponseMessage? response = null;
        string? content = null;

        try
        {
            var payload = JsonConvert.SerializeObject(inputParameters);
            var path = $"/genevaActions/extensions/{extensionName}/operations/{actionName}";
            // get scope of Constants.AgentSpaceOboTokenScope
            var oboTokenCred = await _authenticationService.GetGenevaActionOboCredential();
            var oboToken = (await oboTokenCred.GetTokenAsync(new TokenRequestContext(new[] { Constants.AgentSpaceOboTokenScope }), CancellationToken.None)).Token;

            response = await CallAgentSpaceProxy(path, payload, oboToken);
            content = await response.Content.ReadAsStringAsync();

            _logger.LogInternalInformation($"[GenevaActionsPlugin] [execute_geneva_action_workflow] - extensionName: {extensionName}, actionName: {actionName}, statusCode: {response.StatusCode}");

            if (response.StatusCode == HttpStatusCode.Unauthorized && string.IsNullOrWhiteSpace(oboToken))
            {
                var ex = new ToolExecutionUnauthorizedException($"Access denied for Geneva Action '{actionName}'. The proxy returned 401 Unauthorized. Response: {content}. Try getting OBO token.");
                ex.CustomDescription = $"Run Geneva Action: {extensionName}/{actionName} with parameters {JsonConvert.SerializeObject(inputParameters)}";
                throw ex;
            }

            return new GenevaActionWorkflowResponse
            {
                StatusCode = response.StatusCode,
                Content = content
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[GenevaActionsPlugin] Failed to execute geneva action: extensionName {extensionName}, actionName {actionName} with parameters: {JsonConvert.SerializeObject(inputParameters)}. StatusCode: {response?.StatusCode}, Response: {content ?? "<null>"}, Exception: {ex.Message}");
            throw;
        }
    }

    public async Task<string> ListInputParametersForGenevaAction(string actionName)
    {
        var logMessage = $"[list_input_parameters_for_geneva_action] Invoked with actionName {actionName}.";
        _logger.LogInternalInformation(logMessage);
        var genevaAction = (await GetGenevaActions()).Where(x => x.ActionName == actionName).FirstOrDefault();
        if (genevaAction == null)
        {
            _logger.LogInternalWarning($"[GenevaActionsPlugin] No Geneva Action found for actionName: {actionName}");
            var availableActions = string.Join(", ", (await GetGenevaActions()).Select(x => x.ActionName));
            _logger.LogInternalInformation($"[GenevaActionsPlugin] Available Geneva Actions: {availableActions}");
            return $"No Geneva Action found for actionName: {actionName}";
        }
        return $"For actionName: {actionName}. Required parameters are: {string.Join(", ", genevaAction.WorkflowInputParameters)}";
    }

    private HttpClient GetHttpClient()
    {
        if (string.IsNullOrWhiteSpace(_agentSpaceProxySettings.Endpoint))
        {
            throw new ArgumentException("Agent Space Proxy endpoint is not configured.");
        }

        var client = new HttpClient(_tokenCredentialHttpClientHandler);
        client.BaseAddress = new Uri(_agentSpaceProxySettings.Endpoint!);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("User-Agent", "SRE Agent");
        client.Timeout = TimeSpan.FromSeconds(120);

        return client;
    }

    private async Task<HttpResponseMessage> CallAgentSpaceProxy(string path, string payload, string? oboToken = null)
    {
        _logger.LogInternalInformation($"[GenevaActionsPlugin] [CallAgentSpaceProxy] - endpoint: {_agentSpaceProxySettings.Endpoint}, path: {path}, agentResourceId: {_agentResourceId}, payload: {payload}");

        var httpClient = GetHttpClient();

        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-ms-sreagent-resource-id", _agentResourceId);

        if (!string.IsNullOrEmpty(oboToken))
        {
            request.Headers.Add("X-OBO-Token", oboToken);
        }

        var response = await httpClient.SendAsync(request);
        return response;
    }

    public async Task<string> ExecuteGenevaAction(string incidentId, string extensionName, string actionName, string inputParameters)
    {
        _logger.LogInternalInformation("[GenevaActionsPlugin] Proceeding with executing Geneva Action");

        Dictionary<string, string> parsedInputParameters;

        try
        {
            parsedInputParameters = ParseInputParameter(inputParameters);
        }
        catch (Exception ex)
        {
            var errorMessage = $"[execute_geneva_action] Error parsing input parameters: {ex.Message}";
            _logger.LogInternalWarning(errorMessage);
            return errorMessage;
        }

        var response = await ExecuteGenevaActionWorkflow(extensionName, actionName, parsedInputParameters);

        // Log Geneva Action execution result to ICM
        await TryPostDiscussionEntryAsync(incidentId, $"Geneva Action '{actionName}' executed with status code: {response.StatusCode}, response: {response.Content}");

        if (response.IsSuccessStatusCode)
        {
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                ThreadId!.Value,
                new ChatMessage(ChatRole.Assistant, $"Geneva Actions response: {response.Content}"));
            return $"Geneva Action '{actionName}' executed successfully. Response: {response.Content}.";
        }
        else
        {
            return $"Geneva Action '{actionName}' execution failed with status code: {response.StatusCode}, response: {response.Content}";
        }
    }

    // Old implementation
    public async Task<string> ExecuteGenevaAction(string incidentId, string actionName, Dictionary<string, string> inputParameters)
    {
        var logMessage = $"[execute_geneva_action] Invoked with actionName {actionName} and parameters: {JsonConvert.SerializeObject(inputParameters)}";
        _logger.LogInternalInformation(logMessage);
        await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            ThreadId!.Value,
            new ChatMessage(ChatRole.Assistant, $@"Invoking geneva action **{actionName}** with parameters:
    {string.Join(Environment.NewLine, inputParameters.Select(kvp => $"    {kvp.Key}: {kvp.Value}"))}")
        );

        var genevaAction = (await GetGenevaActions()).Where(x => x.ActionName.ToLower() == actionName.ToLower()).FirstOrDefault();
        if (genevaAction == null)
        {
            return $"No Geneva Action found for actionName: {actionName}";
        }
        var paramsNotFound = genevaAction.WorkflowInputParameters.Where(x => !inputParameters.ContainsKey(x)).Any();
        if (paramsNotFound)
        {
            return $"Missing input parameters for actionName: {actionName}. Required parameters are: {string.Join(", ", genevaAction.WorkflowInputParameters)}";
        }

        if (_icmWorkflowReadOnly && genevaAction.IsWriteAction)
        {
            return "Success. ICM Workflow Client is in ReadOnly mode.";
        }

        // Check if ICM client is available before calling GetIncidentAsync
        if (IsICMClientAvailable())
        {
            try
            {
                var incident = await _icmAPIClient.GetIncidentAsync(incidentId);
                if (incident == null)
                {
                    return $"Incident with ID {incidentId} not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"[GenevaActionsPlugin] Failed to get incident {incidentId} from ICM");
                return $"Failed to retrieve incident {incidentId} from ICM: {ex.Message}";
            }
        }
        else
        {
            _logger.LogInternalWarning($"[GenevaActionsPlugin] ICM client is not available. Skipping incident validation for incident {incidentId}");
        }

        var subscriptionId = inputParameters.ContainsKey("subscriptionId") ? inputParameters["subscriptionId"] : (inputParameters.ContainsKey("subscription") ? inputParameters["subscription"] : null);

        _logger.LogInternalInformation("[GenevaActionsPlugin] Proceeding with executing Geneva Action");
        var response = await ExecuteGenevaActionWorkflow(genevaAction.WorkflowName, genevaAction.ActionName, inputParameters);

        // Log Geneva Action execution result to ICM if allowed by IcmLogLevel
        await LogToICMIfAllowed(genevaAction, incidentId, $"Geneva Action '{actionName}' executed with status code: {response.StatusCode}, response: {response.Content}");

        if (response.IsSuccessStatusCode)
        {
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                ThreadId!.Value,
                new ChatMessage(ChatRole.Assistant, $"Geneva Actions response: {response.Content}"));
            return $"Geneva Action '{actionName}' executed successfully. Response: {response.Content}.";
        }
        else
        {
            return $"Geneva Action '{actionName}' execution failed with status code: {response.StatusCode}, response: {response.Content}";
        }
    }

    public static Dictionary<string, string> ParseInputParameter(string inputParameter)
    {
        var tags = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(inputParameter))
        {
            return tags;
        }

        // Pattern: key=value; key="value with ; and \"quotes\""; key2=unquoted
        var pattern = @"(?<key>[^=;]*)=(?:(?:""(?<qval>(?:[^""\\]|\\.)*)"")|(?<val>[^;]*))(?:;|$)";
        foreach (Match match in Regex.Matches(inputParameter, pattern))
        {
            var key = match.Groups["key"].Value.Trim();
            if (key.Contains('\\') || key.Contains('"'))
            {
                throw new InvalidOperationException("Cannot use quotation marks or backslash in a variable name.");
            }
            string value;
            if (match.Groups["qval"].Success)
            {
                // Unescape \" and \\ in quoted values
                value = Regex.Unescape(match.Groups["qval"].Value);
            }
            else
            {
                value = match.Groups["val"].Value.Trim();
            }
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("Empty variable name was found.");
            }
            else
            {
                tags[key] = value;
            }
        }
        return tags;
    }

    private class AgentFactoryConfigCosmos<T>
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string? Id { get; set; }
        public T? Content { get; set; }

        [JsonPropertyName("_ts")]
        [JsonProperty("_ts")]
        public int Timestamp { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTimeOffset Datetime => DateTimeOffset.FromUnixTimeSeconds(Timestamp);
    }

    private bool ShouldLogToICM(GenevaActionConfig genevaAction, LogLevel messageLogLevel)
    {
        // If IcmLogLevel is null or None, don't log to ICM
        if (genevaAction.IcmLogLevel == null || genevaAction.IcmLogLevel == LogLevel.None)
        {
            return false;
        }

        // Log if the message log level is greater than or equal to the configured ICM log level
        return messageLogLevel >= genevaAction.IcmLogLevel;
    }

    private async Task LogToICMIfAllowed(GenevaActionConfig genevaAction, string incidentId, string message, bool overrideLogLevel = false)
    {
        if (ShouldLogToICM(genevaAction, LogLevel.Information) || overrideLogLevel)
        {
            await TryPostDiscussionEntryAsync(incidentId, message);
        }
    }
}

