// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;
public class FunctionAppsPlugin : IFunctionAppsPlugin
{
    private readonly IGraphDatabaseClient _databaseClient;
    private readonly ILogger<FunctionAppsPlugin> _logger;

    public FunctionAppsPlugin(
        IGraphDatabaseClient graphDatabaseClient,
        ILogger<FunctionAppsPlugin> logger)
    {
        _databaseClient = graphDatabaseClient;
        _logger = logger;
    }

    public async Task<FunctionAppDescriptor> GetFunctionAppInfoAsync(string resourceId)
    {
        _logger.LogInternalInformation($"[get_function_app_info] Invoked with resourceId: {resourceId}");

        try
        {
            string functionAppResourceId = resourceId.ToLower().Replace("/", "_");
            string query = $@"
                g.V().has('id', '{functionAppResourceId}').has('isDeleted', false)
                .hasLabel('{Constants.AppServiceType.ToLower()}')
                .has('kind', containing('{Constants.FunctionAppKind}'))
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";

            var result = await _databaseClient.Query(query);

            if (result == null || !result.Any())
            {
                _logger.LogInternalWarning($"Function App with ID '{resourceId}' not found in graph database.");
                return null;
            }

            var functionApp = result.First();
            var properties = functionApp["properties"];

            string name = functionApp["name"]?.ToString() ?? "";
            string kind = GetFirstPropertyValue(properties, "kind") ?? Constants.FunctionAppKind;
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

            return new FunctionAppDescriptor(
                ResourceId: resourceId,
                Name: name,
                Kind: kind,
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
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in GetFunctionAppInfoAsync with resourceId {resourceId}");
            return null;
        }
    }

    public async Task<IReadOnlyList<FunctionAppDescriptor>> ListFunctionAppsAsync(Guid subscriptionId)
    {
        _logger.LogInternalInformation($"[list_function_app_instances] Invoked with subscription {subscriptionId}");

        var functionApps = new List<FunctionAppDescriptor>();

        try
        {
            string query = $@"
                g.V().has('isDeleted', false)
                .has('subscriptionId', '{subscriptionId}')
                .hasLabel('{Constants.AppServiceType.ToLower()}')
                .has('kind', containing('{Constants.FunctionAppKind}'))
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";

            var result = await _databaseClient.Query(query);

            if (result == null || !result.Any())
            {
                _logger.LogInternalInformation($"No function apps found for subscription {subscriptionId} in graph database.");
                return functionApps;
            }

            foreach (var functionApp in result)
            {
                var properties = functionApp["properties"];

                string id = functionApp["id"].ToString();
                string resourceId = id.Replace("_", "/");

                string name = functionApp["name"]?.ToString() ?? "";
                string kind = GetFirstPropertyValue(properties, "kind") ?? Constants.FunctionAppKind;
                string location = GetFirstPropertyValue(properties, "location");
                string sku = GetFirstPropertyValue(properties, "sku") ?? "Unknown";
                string state = GetFirstPropertyValue(properties, "state") ?? "Unknown";
                string resourceGroup = GetFirstPropertyValue(properties, "resourceGroupName");

                // list function only contains the basic info.
                var functionAppDescriptor = new FunctionAppDescriptor(
                    ResourceId: resourceId,
                    Name: name,
                    Kind: kind,
                    Location: location,
                    Sku: sku,
                    State: state,
                    ResourceGroup: resourceGroup,
                    VnetId: null,
                    StackVersion: null,
                    PlanType: null,
                    MinTlsVersion: null,
                    WebSocketEnabled: null,
                    NumberOfWorkers: null,
                    AutoHealEnabled: null,
                    AlwaysOn: null,
                    HealthCheckEnabled: null);

                functionApps.Add(functionAppDescriptor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error in ListFunctionAppsAsync with subscription {subscriptionId}");
            return new List<FunctionAppDescriptor>();
        }

        return functionApps;
    }

    private string GetFirstPropertyValue(dynamic properties, string propertyName)
    {
        if (properties == null || !((IDictionary<string, object>)properties).ContainsKey(propertyName))
        {
            return string.Empty;
        }

        var values = properties[propertyName];
        if (values is IEnumerable<object> enumerable && enumerable.Any())
        {
            return enumerable.First().ToString();
        }

        return string.Empty;
    }

    private bool? TryParseBool(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        return null;
    }

    private int? TryParseInt(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (int.TryParse(value, out int result))
        {
            return result;
        }

        return null;
    }
}
