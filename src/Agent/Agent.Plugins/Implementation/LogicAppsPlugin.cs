// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Agent.Plugins.Services.Interfaces;
using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using static Agent.Graph.Crawler.ARM.LogicAppCrawler;

namespace Agent.Plugins.Implementation;

public class LogicAppsPlugin : ILogicAppsPlugin
{
    private readonly ArmHelper _armHelper;
    private readonly IGraphService _graphService;
    private readonly ILogger<LogicAppsPlugin> _logger;

    public LogicAppsPlugin(
        ArmHelper armHelper,
        IGraphService graphService,
        ILogger<LogicAppsPlugin> logger)
    {
        this._armHelper = armHelper;
        this._graphService = graphService;
        this._logger = logger;
    }

    public async Task<LogicAppDescriptor?> GetLogicAppInfoAsync(string logicAppResourceId)
    {
        _logger.LogInternalInformation($"[get_logic_app_info] Invoked with resourceId: {logicAppResourceId}");

        try
        {
            string logicAppResourceIdKey = logicAppResourceId.ToLower().Replace("/", "_");
            string query = $@"g.V()
                    .has('id', '{logicAppResourceIdKey}')
                    .has('isDeleted', false)
                    .hasLabel('microsoft.web/sites')
                    .has('kind', containing('workflowapp'))
                    .project('id', 'name', 'type', 'properties')
                    .by(id())
                    .by(coalesce(values('resourceName'), constant('')))
                    .by(label())
                    .by(valueMap())";

            var result = await _graphService.QueryAsync(query);

            if (result == null || !result.Any())
            {
                return null;
            }

            var logicApp = result.First();
            var properties = logicApp["properties"];

            string name = logicApp["name"]?.ToString() ?? "";
            string kind = GetFirstPropertyValue(properties, "kind") ?? Constants.LogicAppKind;
            string resourceKind = GetFirstPropertyValue(properties, "resourceKind") ?? Constants.LogicAppKind;
            string location = GetFirstPropertyValue(properties, "location");
            string sku = GetFirstPropertyValue(properties, "sku") ?? "Unknown";
            string state = GetFirstPropertyValue(properties, "state") ?? "Unknown";
            string resourceGroup = GetFirstPropertyValue(properties, "resourceGroupName");

            string vnetIdValue = GetFirstPropertyValue(properties, "vnetId");
            string? vnetId = string.IsNullOrEmpty(vnetIdValue) ? null : vnetIdValue;

            string stackVersionValue = GetFirstPropertyValue(properties, "stackVersion");
            string? stackVersion = string.IsNullOrEmpty(stackVersionValue) ? null : stackVersionValue;

            string planTypeValue = GetFirstPropertyValue(properties, "planType");
            string? planType = string.IsNullOrEmpty(planTypeValue) ? null : planTypeValue;

            string minTlsVersionValue = GetFirstPropertyValue(properties, "minTlsVersion");
            string? minTlsVersion = string.IsNullOrEmpty(minTlsVersionValue) ? null : minTlsVersionValue;

            bool? webSocketEnabled = TryParseBool(GetFirstPropertyValue(properties, "webSocketEnabled"));
            int? numberOfWorkers = TryParseInt(GetFirstPropertyValue(properties, "numberOfWorkers"));
            bool? autoHealEnabled = TryParseBool(GetFirstPropertyValue(properties, "autoHealEnabled"));
            bool? alwaysOn = TryParseBool(GetFirstPropertyValue(properties, "alwaysOn"));
            bool? healthCheckEnabled = TryParseBool(GetFirstPropertyValue(properties, "healthCheckEnabled"));

            return new LogicAppDescriptor(
                ResourceId: logicAppResourceId,
                Name: name,
                Kind: kind,
                ResourceKind: resourceKind,
                Location: location,
                Sku: sku,
                State: state,
                ResourceGroup: resourceGroup,
                VnetId: vnetId,
                StackVersion: stackVersion,
                PlanType: planType,
                MinTlsVersion: minTlsVersion,
                WebSocketEnabled: webSocketEnabled,
                NumberOfWorkers: numberOfWorkers,
                AutoHealEnabled: autoHealEnabled,
                AlwaysOn: alwaysOn,
                HealthCheckEnabled: healthCheckEnabled);
        }
        catch (Exception)
        {
            _logger.LogInternalError($"Error in GetLogicAppInfoAsync with resourceId {logicAppResourceId}");
            return null;
        }
    }

    public async Task<string> ListRuns(string resourceId, string workflowName)
    {
        try
        {
            var result = await _armHelper.ListWorkflowRuns(resourceId, workflowName);
            return JsonSerializer.Serialize(result);
        }
        catch (Exception)
        {
            return "";
        }
    }

    public async Task<string> ListRunActions(string resourceId, string workflowName, string runName)
    {
        try
        {
            var result = await _armHelper.ListRunActions(resourceId, workflowName, runName);
            return JsonSerializer.Serialize(result);
        }
        catch (Exception)
        {
            return "";
        }
    }

    public async Task<string> ListTriggers(string resourceId, string workflowName)
    {
        var triggers = new List<OperationDescriptor>();
        var response = await _armHelper.GetWorkflowAsync($"{resourceId}/workflows/{workflowName}");
        using var responseElement = JsonDocument.Parse(response);

        responseElement.RootElement.TryGetProperty("properties", out var propertiesElement);
        propertiesElement.TryGetProperty("files", out var filesElement);

        filesElement.TryGetProperty("workflow.json", out var workflowElement);
        workflowElement.TryGetProperty("definition", out var definitionElement);
        definitionElement.TryGetProperty("triggers", out var triggersElement);

        filesElement.TryGetProperty("connections.json", out var connectionsElement);
        var connections = TryParseConnections(connectionsElement);

        foreach (var trigger in triggersElement.EnumerateObject())
        {
            var triggerDefinition = trigger.Value;
            if (triggerDefinition.ValueKind != JsonValueKind.Object)
                continue;

            TryGetOperation(triggers, connections, trigger.Name, triggerDefinition);
        }

        return JsonSerializer.Serialize(triggers);
    }

    private static void TryGetOperation(List<OperationDescriptor> operations, LogicAppConnections? connections, string operationName, JsonElement operationDefinition)
    {
        var operationType = operationDefinition.TryGetString("type")?.ToLower();
        string? connectorType = null;
        string? referenceName = null;

        switch (operationType)
        {
            case "apiconnection":
                var referecenNameElement = operationDefinition.TryGet("inputs", "host", "connection", "referenceName");
                referenceName = referecenNameElement.ValueKind == JsonValueKind.String ? referecenNameElement.GetString() : null;
                ManagedApiConnection? connection = null;
                connections?.ManagedApiConnections?.TryGetValue(referenceName ?? "", out connection);
                var connectorId = connection?.Api?.Id ?? string.Empty;
                var connectorName = connectorId.Split('/').LastOrDefault();
                connectorType = connectorName != null ? $"managedApi/{connectorName}" : null;
                break;
            case "serviceprovider":
                referenceName = operationDefinition.TryGet("inputs", "serviceProviderConfiguration", "connectionName").ToString();
                connectorType = operationDefinition.TryGet("inputs", "serviceProviderConfiguration", "serviceProviderId").ToString();
                break;
        }

        if (connectorType != null)
        {
            operations.Add(new OperationDescriptor(operationName, operationType, connectorType, referenceName));
        }
    }

    public async Task<string> ListActions(string resourceId, string workflowName)
    {
        var actions = new List<OperationDescriptor>();
        var response = await _armHelper.GetWorkflowAsync($"{resourceId}/workflows/{workflowName}");
        using var responseElement = JsonDocument.Parse(response);

        responseElement.RootElement.TryGetProperty("properties", out var propertiesElement);
        propertiesElement.TryGetProperty("files", out var filesElement);

        filesElement.TryGetProperty("workflow.json", out var workflowElement);
        workflowElement.TryGetProperty("definition", out var definitionElement);
        definitionElement.TryGetProperty("actions", out var actionsElement);

        filesElement.TryGetProperty("connections.json", out var connectionsElement);
        var connections = TryParseConnections(connectionsElement);

        foreach (var (name, action) in LogicAppCrawler.TraverseAllActions(actionsElement))
        {
            TryGetOperation(actions, connections, name, action);
        }

        return JsonSerializer.Serialize(actions);
    }

    public async Task<UpdateAppSettingResult> UpdateAppSetting(string resourceId, string key, string value)
    {

        var result = new UpdateAppSettingResult
        {
            ResourceId = resourceId
        };

        try
        {
            // Get current app settings
            var appSettingsJson = await _armHelper.GetAppSettings(resourceId);

            if (string.IsNullOrWhiteSpace(appSettingsJson))
            {
                result.IsSuccessful = false;
                result.ErrorMessage = "Failed to retrieve app settings";
                return result;
            }

            // Parse app settings to prepare for update
            var appSettings = new Dictionary<string, string>();
            var appSettingsObj = JObject.Parse(appSettingsJson);
            var properties = appSettingsObj["properties"] as JObject;

            if (properties == null)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = "Failed to parse app settings";
                return result;
            }

            appSettings[key] = value;

            // Update app settings
            var updateResult = await _armHelper.UpdateAppSettingsAsync(resourceId, appSettings);

            if (!updateResult)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = "Failed to update app settings";
                return result;
            }

            result.IsSuccessful = true;
            result.Details += $"Successfully updated '{key}' to '{value}'.";

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = $"An error occurred while updating '{key}': {ex.Message}";
            return result;
        }
    }

    public async Task<IReadOnlyList<Workflow>> ListWorkflowsAsync(string logicAppResourceId)
    {

        var workflows = new List<Workflow>();
        try
        {
            var logicAppResourceIdKey = logicAppResourceId.ToLower().Replace("/", "_");
            var query = $@"g.V()
                    .has('id', '{logicAppResourceIdKey}')
                    .has('isDeleted', false)
                    .hasLabel('microsoft.web/sites')
                    .has('kind', containing('workflowapp'))
                    .outE()
                    .inV()
                        .hasLabel('microsoft.web/sites/workflows')
                        .has('isDeleted', false)
                        .project('id', 'name', 'type', 'properties')
                            .by(id())
                            .by(coalesce(values('resourceName'), constant('')))
                            .by(label())
                            .by(valueMap())";

            var result = await _graphService.QueryAsync(query);

            if (result == null || !result.Any())
            {
                return workflows;
            }

            foreach (var workflow in result)
            {
                var properties = workflow["properties"];
                var id = workflow["id"].ToString();
                var resourceId = id.Replace("_", "/");
                var name = workflow["name"]?.ToString();

                var workflowDescriptor = new Workflow(
                    Id: resourceId,
                    Name: name
                );

                workflows.Add(workflowDescriptor);
            }
        }
        catch (Exception)
        {
            return Array.Empty<Workflow>();
        }

        return workflows;
    }

    public async Task<IReadOnlyList<Workflow>> ListHttpRequestTriggerWorkflowsAsync(string logicAppResourceId)
    {
        var allWorkflows = await ListWorkflowsAsync(logicAppResourceId);
        var httpRequestTriggerWorkflows = new List<Workflow>();
        foreach (var workflow in allWorkflows)
        {
            var response = await _armHelper.GetWorkflowAsync(workflow.Id);
            using var responseElement = JsonDocument.Parse(response);

            responseElement.RootElement.TryGetProperty("properties", out var propertiesElement);
            propertiesElement.TryGetProperty("files", out var filesElement);
            filesElement.TryGetProperty("workflow.json", out var workflowElement);
            workflowElement.TryGetProperty("definition", out var definitionElement);
            definitionElement.TryGetProperty("triggers", out var triggersElement);

            foreach (var trigger in triggersElement.EnumerateObject())
            {
                var triggerDefinition = trigger.Value;
                if (triggerDefinition.ValueKind != JsonValueKind.Object)
                    continue;

                var triggerType = triggerDefinition.TryGetString("type")?.ToLower();
                if (triggerType == "request")
                {
                    httpRequestTriggerWorkflows.Add(workflow);
                    break;
                }
            }
        }
        return httpRequestTriggerWorkflows;
    }

    public async Task<IReadOnlyList<ManagedConnector>> GetManagedConnectorsByWorkflow(string subscriptionId, string resourceGroupName, string logicAppName, string workflowName)
    {
        var connectors = new Dictionary<string, ManagedConnector>();

        try
        {
            var id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{logicAppName}/workflows/{workflowName}";
            //_logger.LogInternalInformation("Querying subscriptions from graph database");
            var query = $@"g.V()
                .has('id', '{id.ToLower().Replace("/", "_")}')
                .has('isDeleted', false)
                .outE('USES')
                .inV()
                    .has('isDeleted', false)
                    .hasLabel('microsoft.web/connections')
                    .project('id', 'name', 'type', 'properties')
                        .by(id())
                        .by(coalesce(values('resourceName'), constant('')))
                        .by(label())
                        .by(valueMap())";

            var result = await _graphService.QueryAsync(query);
            if (result == null || !result.Any())
            {
                return Array.Empty<ManagedConnector>();
            }

            foreach (var connector in result)
            {
                var properties = connector["properties"];
                var connectionNode = new ConnectionNode(properties);
                var connectorName = connectionNode.ConnectorName;
                if (connectorName != null)
                {
                    connectors.TryAdd(connectorName, new ManagedConnector($"managedApis/{connectorName}", connectorName));
                }
            }
        }
        catch (Exception)
        {
            return Array.Empty<ManagedConnector>();
        }

        return connectors.Values.ToArray();
    }

    public Task<ServiceProviderConnector?> LookupServiceProviderConnectorEquivalent(string managedConnectorId)
    {
        var lookup = new Dictionary<string, ServiceProviderConnector?>()
        {
            {
                "managedApis/sftpwithssh",
                new ServiceProviderConnector("serviceProviders/sftp", "sftp")
            }
        };

        return Task.FromResult(
            lookup.TryGetValue(managedConnectorId, out var connector) ? connector : null);
    }

    public async Task<IReadOnlyList<string>> GetMissingDiagnosticSettingsAsync(string logicAppResourceId)
    {
        var diagnosticSettingsJson = await _armHelper.GetDiagnosticSettingsByResourceIdAsync(logicAppResourceId);

        if (string.IsNullOrEmpty(diagnosticSettingsJson))
            return new List<string> { RequiredMetricsCategory }.Concat(RequiredLogCategories).ToList();

        bool metricsEnabled = false;
        var presentLogCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var envelope = JsonSerializer.Deserialize<DiagnosticSettingsEnvelope>(diagnosticSettingsJson, SerializerOptions);

            if (envelope?.Value != null)
            {
                foreach (var setting in envelope.Value)
                {
                    var properties = setting?.Properties;
                    if (properties == null)
                        continue;

                    if (properties.Metrics != null &&
                        properties.Metrics.Any(m => m.Enabled &&
                                                    string.Equals(m.Category, RequiredMetricsCategory, StringComparison.OrdinalIgnoreCase)))
                    {
                        metricsEnabled = true;
                    }

                    if (properties.Logs != null)
                    {
                        foreach (var log in properties.Logs.Where(l => l.Enabled && !string.IsNullOrEmpty(l.Category)))
                        {
                            presentLogCategories.Add(log.Category!);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in GetMissingDiagnosticSettingsAsync with resourceId {logicAppResourceId}");
            // If deserialization fails, assume all required categories are missing
            return new List<string> { RequiredMetricsCategory }.Concat(RequiredLogCategories).ToList();
        }
        var missingCategories = new List<string>();

        if (!metricsEnabled)
            missingCategories.Add(RequiredMetricsCategory);

        missingCategories.AddRange(RequiredLogCategories.Except(presentLogCategories));

        return missingCategories;
    }

    public async Task<bool> IsEasyAuthEnabledAsync(string resourceId)
    {
        try
        {
            var response = await _armHelper.GetAuthSettingsV2Async(resourceId);
            if (string.IsNullOrEmpty(response))
                return false;

            using var responseElement = JsonDocument.Parse(response);

            if (responseElement.RootElement.TryGetProperty("properties", out var propertiesElement) &&
                propertiesElement.TryGetProperty("platform", out var platformElement) &&
                platformElement.TryGetProperty("enabled", out var enabled))
            {
                return enabled.ValueKind == JsonValueKind.True;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in IsEasyAuthEnabledAsync with resourceId {resourceId}");
            return false;
        }
    }

    public async Task<bool> IsApplicationInsightsConfiguredAsync(string resourceId)
    {
        try
        {
            var response = await _armHelper.GetAppSettings(resourceId);
            if (string.IsNullOrEmpty(response))
                return false;

            using var responseElement = JsonDocument.Parse(response);
            if (!responseElement.RootElement.TryGetProperty("properties", out var propertiesElement))
                return false;

            string? connectionString = null;
            string? instrumentationKey = null;

            if(propertiesElement.TryGetProperty("APPLICATIONINSIGHTS_CONNECTION_STRING", out var connectionStringElement) &&
               connectionStringElement.ValueKind == JsonValueKind.String)
            {
                connectionString = connectionStringElement.GetString();
            }

            if (propertiesElement.TryGetProperty("APPINSIGHTS_INSTRUMENTATIONKEY", out var instrumentationKeyElement) &&
                instrumentationKeyElement.ValueKind == JsonValueKind.String)
            {
                instrumentationKey = instrumentationKeyElement.GetString();
            }

            // If both are missing, app insights is not configured
            if (string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(instrumentationKey))
            {
                return false;
            }

            bool isValidConnectionString = false;
            if (!string.IsNullOrEmpty(connectionString))
            {
                // Connection String must contain: InstrumentationKey=<valid-guid>
                var match = System.Text.RegularExpressions.Regex.Match(
                    connectionString,
                    @"InstrumentationKey=([0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})"
                );
                isValidConnectionString = match.Success;
            }

            bool isValidInstrumentationKey = false;
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                // Instrumentation Key must be a valid GUID
                var match = System.Text.RegularExpressions.Regex.Match(
                    instrumentationKey,
                    @"^([0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})$"
                );
                isValidInstrumentationKey = match.Success;
            }

            return isValidConnectionString || isValidInstrumentationKey;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in IsApplicationInsightsConfigured with resourceId {resourceId}");
            return false;
        }
    }

    public async Task<bool> IsExtensionBundleVersionPinnedAsync(string resourceId)
    {
        try
        {
            var response = await _armHelper.GetAppSettings(resourceId);
            if (string.IsNullOrEmpty(response))
                return false;

            using var responseElement = JsonDocument.Parse(response);
            if (!responseElement.RootElement.TryGetProperty("properties", out var propertiesElement))
                return false;

            if (!propertiesElement.TryGetProperty("AzureFunctionsJobHost__extensionBundle__version", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.String)
            {
                // Setting doesn't exist - not pinned (default behavior will be used)
                return false;
            }

            var versionValue = versionElement.GetString();
            if (string.IsNullOrWhiteSpace(versionValue))
            {
                return false;
            }

            // The recommended setting is "[1.*, 2.0.0)", If it's not this exact value, it's considered pinned
            const string recommendedVersion = "[1.*, 2.0.0)";
            return !string.Equals(versionValue, recommendedVersion, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in IsExtensionBundleVersionPinnedAsync with resourceId {resourceId}");
            return false;
        }
    }

    private const string RequiredMetricsCategory = "AllMetrics";

    private static readonly string[] RequiredLogCategories =
    [
        "WorkflowRuntime",
        "FunctionAppLogs",
        "AppServiceAuthenticationLogs"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static LogicAppConnections? TryParseConnections(JsonElement connectionsElement)
    {
        try
        {
            return JsonSerializer.Deserialize<LogicAppConnections>(connectionsElement, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? default;
        }
        catch (Exception)
        {
            return default;
        }
    }

    private string GetFirstPropertyValue(dynamic properties, string propertyName)
    {
        if (properties == null)
            return string.Empty;

        var dict = properties as IDictionary<string, object>;
        if (dict == null || !dict.ContainsKey(propertyName))
            return string.Empty;

        var values = dict[propertyName];
        if (values is IEnumerable<object> enumerable)
        {
            var firstValue = enumerable.Cast<object>().FirstOrDefault();
            return firstValue?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private bool? TryParseBool(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (bool.TryParse(value, out bool result))
            return result;

        return null;
    }

    private int? TryParseInt(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (int.TryParse(value, out int result))
            return result;

        return null;
    }
}

public record OperationDescriptor(
    string Name,
    string? Type,
    string? ConnectorType,
    string? ConnectionReferenceName
);

public class UpdateAppSettingResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the update was successful
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the resource ID that was updated
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message if the update failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional details about the update
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time when the update was performed
    /// </summary>
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
