// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;
public class AppServicePlugin : IAppServicePlugin
{
    private readonly IGraphDatabaseClient _databaseClient;
    private readonly ILogger<AppServicePlugin> _logger;

    public AppServicePlugin(
        IGraphDatabaseClient graphDatabaseClient, 
        ILogger<AppServicePlugin> logger)
    {
        _databaseClient = graphDatabaseClient;
        _logger = logger;
    }

    public async Task<AppServiceDescriptor> GetAppServiceInfoAsync(string resourceId)
    {
        _logger.LogInformation($"[get_app_service_info] Invoked with resourceId: {resourceId}");

        try
        {
            string appServiceResourceId = resourceId.ToLower().Replace("/", "_");
            
            string query = $@"
                g.V().has('id', '{appServiceResourceId}')
                .hasLabel('{Constants.AppServiceType.ToLower()}')
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";

            var result = await _databaseClient.Query(query);

            if (result == null || !result.Any())
            {
                _logger.LogWarning($"App Service with ID '{resourceId}' not found in graph database.");
                return null;
            }

            var appService = result.First();
            var properties = appService["properties"];

            string name = appService["name"]?.ToString() ?? "";
            string kind = GetFirstPropertyValue(properties, "kind") ?? "app"; 
            string location = GetFirstPropertyValue(properties, "location");
            string sku = GetFirstPropertyValue(properties, "sku") ?? "Unknown";
            string state = GetFirstPropertyValue(properties, "state") ?? "Unknown";
            string resourceGroup = GetFirstPropertyValue(properties, "resourceGroupName");
            int numberOfWOrkers = int.TryParse(GetFirstPropertyValue(properties, "numberOfWorkers"), out int numWorkers) ? numWorkers : 1;
            bool autoHealEnabled = bool.TryParse(GetFirstPropertyValue(properties, "autoHealEnabled"), out bool autoHeal) ? autoHeal : false;
            bool alwaysOnEnabled = bool.TryParse(GetFirstPropertyValue(properties, "alwaysOnEnabled"), out bool alwaysOn) ? alwaysOn : false;
            bool healthCheckEnabled = bool.TryParse(GetFirstPropertyValue(properties, "healthCheckEnalbled"), out bool healthCheck) ? healthCheck : false;

            return new AppServiceDescriptor(
                ResourceId: resourceId,
                Name: name,
                Kind: kind,
                Location: location,
                Sku: sku,
                State: state,
                ResourceGroup: resourceGroup,
                NumberOfWorkers: numberOfWOrkers,
                AutoHealEnabled: autoHealEnabled,
                AlwaysOn: alwaysOnEnabled,
                HealthCheckEnabled: healthCheckEnabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in GetAppServiceInfoAsync with resourceId {resourceId}");
            return null;
        }
    }

    public async Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId)
    {
        _logger.LogInformation($"[list_app_service_instances] Invoked with subscription {subscriptionId}");

        var appServices = new List<AppServiceDescriptor>();

        try
        {
            string query = $@"
                g.V()
                .has('subscriptionId', '{subscriptionId}')
                .hasLabel('{Constants.AppServiceType.ToLower()}')
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";

            var result = await _databaseClient.Query(query);

            if (result == null || !result.Any())
            {
                _logger.LogInformation($"No app services found for subscription {subscriptionId} in graph database.");
                return appServices;
            }

            foreach (var appService in result)
            {
                var properties = appService["properties"];
                
                string id = appService["id"].ToString();
                string resourceId = id.Replace("_", "/");
                
                string name = appService["name"]?.ToString() ?? "";
                string kind = GetFirstPropertyValue(properties, "kind") ?? "app"; 
                string location = GetFirstPropertyValue(properties, "location");
                string sku = GetFirstPropertyValue(properties, "sku") ?? "Unknown";
                string state = GetFirstPropertyValue(properties, "state") ?? "Unknown";
                string resourceGroup = GetFirstPropertyValue(properties, "resourceGroupName");
                int numberOfWOrkers = int.TryParse(GetFirstPropertyValue(properties, "numberOfWorkers"), out int numWorkers) ? numWorkers : 1;
                bool autoHealEnabled = bool.TryParse(GetFirstPropertyValue(properties, "autoHealEnabled"), out bool autoHeal) ? autoHeal : false;
                bool alwaysOnEnabled = bool.TryParse(GetFirstPropertyValue(properties, "alwaysOnEnabled"), out bool alwaysOn) ? alwaysOn : false;
                bool healthCheckEnabled = bool.TryParse(GetFirstPropertyValue(properties, "healthCheckEnalbled"), out bool healthCheck) ? healthCheck : false;

                var appServiceDescriptor = new AppServiceDescriptor(
                    ResourceId: resourceId,
                    Name: name,
                    Kind: kind,
                    Location: location,
                    Sku: sku,
                    State: state,
                    ResourceGroup: resourceGroup,
                    NumberOfWorkers: numberOfWOrkers,
                    AutoHealEnabled: autoHealEnabled,
                    AlwaysOn: alwaysOnEnabled,
                    HealthCheckEnabled: healthCheckEnabled);

                appServices.Add(appServiceDescriptor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in ListAppServicesAsync with subscription {subscriptionId}");
            return new List<AppServiceDescriptor>();
        }

        return appServices;
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
}
