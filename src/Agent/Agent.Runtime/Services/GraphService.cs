// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Data.DatabaseClients.GraphDbClient;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Logging;
using ArmConstants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Runtime.Services;

public class GraphService : IGraphService
{
    private readonly IGraphDatabaseClient _graphDatabaseClient;
    private readonly ILogger<GraphService> _logger;
    private readonly string _grafanaUrl;
    private readonly string _grafanaToken;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DashboardSettings _dashboardSettings;

    private readonly Dictionary<string, string> _dashboardsToProcessByResourceType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "microsoft.app/containerapps", "azure-container-apps-container-app-view" },
            { "microsoft.storage/storageaccounts", "azure-insights-storage-accounts" },
            { "microsoft.documentdb/databaseaccounts", "azure-insights-cosmos-db" },
            { "microsoft.cache/redis", "azure-redis" },
            { "microsoft.web/sites", "azure-app-service-monitoring" },
            // Pending: webapp, sql
        };

    public GraphService(IGraphDatabaseClient graphDatabaseClient, DashboardSettings dashboardSettings, ILogger<GraphService> logger, IHttpClientFactory httpClientFactory)
    {
        _graphDatabaseClient = graphDatabaseClient;
        _logger = logger;
        _dashboardSettings = dashboardSettings;

        _grafanaUrl = dashboardSettings.GrafanaUrl.TrimEnd('/');
        _grafanaToken = dashboardSettings.GrafanaApiKey;
        _httpClientFactory = httpClientFactory;
    }

    private HttpClient GetHttpClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_grafanaToken}");
        return client;
    }

    public async Task<ResultSet<dynamic>> QuerySubscriptionsAsync()
    {
        _logger.LogInformation("Querying subscriptions from graph database");
        string query = $@"g.V().has('resourceType', '{SubscriptionNode.Type}')
                         .project('name', 'id')
                         .by('subscriptionName')
                         .by('subscriptionId')";

        return await _graphDatabaseClient.Query(query);
    }

    public async Task<ResultSet<dynamic>> QueryAsync(string query)
    {
        return await _graphDatabaseClient.Query(query);
    }

    public async Task<ResultSet<dynamic>> GetAppGroupsBySubscriptionAsync(string subscriptionId)
    {
        _logger.LogInformation("Querying app groups for subscription {subscriptionId}", subscriptionId);
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
        }

        string query = $@"g.V().has('subscriptionId', '{subscriptionId.ToLower()}')
                        .has('resourceType', within(
                            '{ArmConstants.ContainerAppType.ToLower()}',
                            '{ArmConstants.AppServiceType.ToLower()}',
                            '{ArmConstants.AzureKubernetesServiceType.ToLower()}',
                        ))
                        .project('id', 'name', 'type', 'properties')
                        .by(id())
                        .by(coalesce(values('resourceName'), constant('')))
                        .by(label())
                        .by(valueMap())";

        return await _graphDatabaseClient.Query(query);
    }

    private async Task<ResultSet<dynamic>> GetRelatedResourcesAsync(string resourceId, int hops)
    {
        string query = $@"g.V().has('id', '{resourceId.ToLower().Replace("/", "_")}')
                    .union(
                        repeat(
                            union(
                                outE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS').inV(),
                                inE('LINKED', 'CONNECTED', 'HOSTED_ON').outV()
                            )
                            .not(has('resourceType', within('resourcegroups', 'subscription')))
                            .simplePath()
                        )
                        .times({hops})
                        .emit()
                    )
                    .dedup()
                    .project('id', 'name', 'type', 'properties')
                    .by(id())
                    .by(coalesce(values('resourceName'), constant('')))
                    .by(label())
                    .by(valueMap())";

        var resultSet = await _graphDatabaseClient.Query(query);
        return resultSet;
    }

    private List<AppGroupItem> ConvertToAppGroupItems(ResultSet<dynamic> result, string entryPointResourceId, List<AppGroupItem> subItems)
    {
        var appGroupItems = new List<AppGroupItem>();
        foreach (var item in result)
        {
            var properties = item["properties"] as IDictionary<string, object>;

            var appGroupItem = new AppGroupItem
            {
                Name = item["name"]?.ToString() ?? string.Empty,
                Type = item["type"]?.ToString() ?? string.Empty,
                ResourceId = item["id"],
                AppHealthInfo = properties != null && properties.ContainsKey("appHealthInfo") ? properties["appHealthInfo"] as AppHealthInfo : null,
                SubItems = subItems
            };
            appGroupItems.Add(appGroupItem);
        }

        return appGroupItems;
    }

    public async Task<ResultSet<AppGroupItem>> GetAppGroupResourcesAsync(string resourceId)
    {
        int hops = 2;

        // HashSet to track visited nodes to avoid cycles
        var processedNodes = new HashSet<string>();
        var appGroupItems = await ProcessResourceHierarchyAsync(resourceId, processedNodes, hops);

        return new ResultSet<AppGroupItem>(appGroupItems, new Dictionary<string, object>());
    }

    // Recursive method to explore the connected resources for a given resource
    private async Task<List<AppGroupItem>> ProcessResourceHierarchyAsync(string resourceId, HashSet<string> processedNodes, int remainingLevels)
    {
        if (remainingLevels <= 0 || processedNodes.Contains(resourceId))
        {
            return new List<AppGroupItem>();
        }

        processedNodes.Add(resourceId);

        var resultSet = await GetRelatedResourcesAsync(resourceId, 1);

        if (resultSet == null || !resultSet.Any())
        {
            return new List<AppGroupItem>();
        }

        var appGroupItems = new List<AppGroupItem>();

        foreach (var resource in resultSet)
        {
            string relatedResourceId = resource["id"];

            if (processedNodes.Contains(relatedResourceId))
            {
                continue;
            }

            var childItems = await ProcessResourceHierarchyAsync(relatedResourceId, processedNodes, remainingLevels - 1);

            var properties = resource["properties"] as IDictionary<string, object>;

            var item = new AppGroupItem
            {
                Name = resource["name"],
                Type = resource["type"],
                ResourceId = relatedResourceId,
                AppHealthInfo = properties != null && properties.ContainsKey("appHealthInfo") ? properties["appHealthInfo"] as AppHealthInfo : null,
                SubItems = childItems.Count > 0 ? childItems : null
            };

            appGroupItems.Add(item);
        }

        return appGroupItems;
    }

    public async Task<List<ArmResourceNode>> GetAllResourceNodes()
    {
        _logger.LogInformation("Fetching all resource nodes from the graph database.");
        var allResourceNodes = await _graphDatabaseClient.Query("g.V().project('resourceType', 'resourceName','resourceGroupName','subscriptionId', 'resourceId').by(coalesce(values('resourceType'), constant('MISSING'))).by(coalesce(values('resourceName'), constant('MISSING'))).by(coalesce(values('resourceGroupName'), constant('MISSING'))).by(coalesce(values('subscriptionId'), constant('MISSING'))).by(coalesce(values('resourceId'), constant('MISSING')))");

        if (allResourceNodes is null || allResourceNodes.Count == 0)
        {
            _logger.LogWarning("No resource nodes found in the graph database.");
            return [];
        }

        _logger.LogInformation($"Fetched {allResourceNodes.Count} resource nodes from the graph database.");

        return [.. allResourceNodes.Select(node => new ArmResourceNode
            {
                ResourceType = node["resourceType"],
                ResourceName = node["resourceName"],
                ResourceGroupName = node["resourceGroupName"],
                SubscriptionId = node["subscriptionId"],
                ResourceId = node["resourceId"]
            })];
    }

    public async Task<ResultSet<dynamic>> GetGraphResourceAsync(string resourceId)
    {
        _logger.LogInformation("Querying graph resource {resourceId}", resourceId);
        string query = $@"g.V().has('id', '{resourceId}')
                    .project('id', 'name', 'type', 'properties')
                    .by(id())
                    .by(coalesce(values('resourceName'), constant('')))
                    .by(label())
                    .by(valueMap())";
        var result = await _graphDatabaseClient.Query(query);

        foreach (var item in result)
        {
            try
            {
                var dict = (IDictionary<string, object>)item;

                if (!dict.TryGetValue("properties", out var propertiesObj) || propertiesObj == null)
                {
                    _logger.LogWarning("Properties not found or null");
                    continue;
                }

                var properties = (IDictionary<string, object>)propertiesObj;
                string resourceType = GetFirstValueAsString(properties, "resourceType")?.ToLowerInvariant() ?? "";
                string resourceName = dict["name"]?.ToString() ??
                                     GetFirstValueAsString(properties, "resourceName") ?? "";
                string resourceGroupName = GetFirstValueAsString(properties, "resourceGroupName") ?? "";
                string subscription = GetFirstValueAsString(properties, "subscriptionId") ?? "";
                if (_dashboardsToProcessByResourceType.TryGetValue(resourceType, out string dashboardType))
                {
                    string baseUrl = $"{_grafanaUrl}/d/{dashboardType}";
                    var queryParams = new Dictionary<string, string>
                {
                    { "var-ds", "azure-monitor-oob" },
                    { "var-ns", resourceType },
                    { "var-sub", subscription },
                    { "var-rg", resourceGroupName.ToLowerInvariant() },
                    { "var-resource", resourceName.ToLowerInvariant() }
                };

                    // Add dashboard-specific parameters
                    switch (dashboardType)
                    {
                        case "azure-container-apps-container-app-view":
                            queryParams["var-containerapp"] = resourceName.ToLowerInvariant();
                            break;
                        case "azure-redis":
                        case "azure-app-service-monitoring":
                            queryParams["var-name"] = resourceName.ToLowerInvariant();
                            break;
                    }

                    // Try to get actual dashboard URL from Grafana API
                    string dashboardUrl = baseUrl;
                    try
                    {
                        using var httpClient = GetHttpClient();
                        var dashboardResponse = await httpClient.GetAsync($"{_grafanaUrl}/api/search?type=dash-db");
                        dashboardResponse.EnsureSuccessStatusCode();
                        var dashboardsContent = await dashboardResponse.Content.ReadAsStringAsync();
                        var dashboards = JsonSerializer.Deserialize<JsonElement>(dashboardsContent);

                        foreach (var dashboard in dashboards.EnumerateArray())
                        {
                            if (dashboard.TryGetProperty("url", out var urlElement) &&
                                urlElement.GetString().Contains(dashboardType, StringComparison.OrdinalIgnoreCase))
                            {
                                dashboardUrl = $"{urlElement.GetString()}";
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get dashboard URL from API, using base URL");
                    }

                    // Add query parameters to URL
                    dashboardUrl = AddQueryParameters(dashboardUrl, queryParams);

                    // Add the dashboard URL to the result
                    ((IDictionary<string, object>)item)["dashboardUrl"] = dashboardUrl;
                }
                else
                {
                    // No dashboard available for this resource type
                    ((IDictionary<string, object>)item)["dashboardUrl"] = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add dashboard URL to result");
                // Ensure the property exists even if there's an error
                try
                {
                    ((IDictionary<string, object>)item)["dashboardUrl"] = null;
                }
                catch
                {
                    // best try
                }
            }
        }

        return result;
    }

    private string GetFirstValueAsString(IDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        // Handle the IEnumerableSelectIterator using non-generic IEnumerable
        if (value is System.Collections.IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            if (enumerator.MoveNext() && enumerator.Current != null)
            {
                return enumerator.Current.ToString();
            }
        }

        return value.ToString();
    }

    private string AddQueryParameters(string url, Dictionary<string, string> parameters)
    {
        // Return the original URL if there are no parameters to add
        if (string.IsNullOrEmpty(url) || parameters == null || parameters.Count == 0)
        {
            return url;
        }

        // Convert dictionary to a query string with URL-encoded parameters
        var queryString = string.Join("&", parameters
            .Select(param => $"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}"));

        // Determine the correct separator: ? if no query exists, otherwise use &
        char separator = url.Contains("?") ? '&' : '?';

        // Append the query string to the URL
        return $"{url}{separator}{queryString}";
    }

    public async Task<ResultSet<dynamic>> UpdateGraphResourceProperties(string resourceId, IDictionary<string, string> properties)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));
        }

        if (properties == null || !properties.Any())
        {
            throw new ArgumentException("Properties cannot be null or empty", nameof(properties));
        }

        _logger.LogInformation("Updating properties for resource {resourceId}", resourceId);
        
        // check if the vertex exists
        string checkQuery = $"g.V().has('id', '{resourceId}').count()";

        var checkResult = await _graphDatabaseClient.Query(checkQuery);
        
        if (checkResult == null || !checkResult.Any() || Convert.ToInt64(checkResult.First()) == 0)
        {
            _logger.LogWarning($"Resource {resourceId} not found in the graph database");
            throw new KeyNotFoundException($"Resource with ID {resourceId} not found");
        }

        var bindings = new Dictionary<string, object>();  

        string updateQuery = $"g.V().has('id', '{resourceId}')"; // TODO: currently we are using the resource id as is (_resource_capps_sample_). Refactor this to use /resource/resourceId format.

        foreach (var property in properties)
        {
            updateQuery += $".property('{property.Key}', {getValue(property.Value)})";
        }

        var now = DateTime.UtcNow.Ticks;
        // update timestamp
        string tsParamName = "updateTs";

        updateQuery += $".property('{tsParamName}', {now})";
        
        _logger.LogDebug("Executing property update query for {resourceId}", resourceId);
        try
        {
            var result = await _graphDatabaseClient.Query(updateQuery);
            _logger.LogInformation("Successfully updated properties for resource {resourceId}", resourceId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update properties for resource {resourceId}", resourceId);
            throw;
        }
    }

    private string getValue(object val)
    {
        switch (val)
        {
            case int i:
                return i.ToString();
            case long l:
                return l.ToString();
            default:
                return $"'{val}'";
        }
    }
}
