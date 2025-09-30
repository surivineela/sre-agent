using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Agent.Plugins.Services.Interfaces;
using Azure.Storage.Blobs;
using IdentityModel.Client;
using Kusto.Cloud.Platform.Utils;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Graph.Models;
using Newtonsoft.Json.Linq;
using static Agent.Graph.Crawler.ARM.LogicAppCrawler;

namespace Agent.Plugins.Implementation
{
    public class LogicAppsPlugin : ILogicAppsPlugin
    {
        private readonly ArmHelper _armHelper;
        private readonly IGraphService _graphService;

        public LogicAppsPlugin(
            ArmHelper armHelper,
            IGraphService _graphService)
        {
            this._armHelper = armHelper;
            this._graphService = _graphService;
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
            var response = await this._armHelper.GetWorkflowAsync($"{resourceId}/workflows/{workflowName}");
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
            var response = await this._armHelper.GetWorkflowAsync($"{resourceId}/workflows/{workflowName}");
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
                string appSettingsJson = await _armHelper.GetAppSettings(resourceId);

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
                bool updateResult = await _armHelper.UpdateAppSettingsAsync(resourceId, appSettings);

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
                string logicAppResourceIdKey = logicAppResourceId.ToLower().Replace("/", "_");
                string query = $@"g.V()
                    .has('id', containing('{logicAppResourceIdKey}'))
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

        public async Task<IReadOnlyList<ManagedConnector>> GetManagedConnectorsByWorkflow(string subscriptionId, string resourceGroupName, string logicAppName, string workflowName)
        {
            var connectors = new Dictionary<string, ManagedConnector>();

            try
            {
                var id = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/sites/{logicAppName}/workflows/{workflowName}";
                //_logger.LogInternalInformation("Querying subscriptions from graph database");
                string query = $@"g.V()
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

            return Task.FromResult(lookup.GetOrDefault(managedConnectorId, null));
        }

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
}
