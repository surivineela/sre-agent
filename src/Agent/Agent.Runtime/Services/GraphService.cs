// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Agent.Graph.Schema;
using Agent.Logging;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Logging;
using ArmConstants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Runtime.Services;

public class GraphService : IGraphService
{
    private readonly IGraphDatabaseClient _graphDatabaseClient;
    private readonly ILogger<GraphService> _logger;
    private readonly string _grafanaUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DashboardSettings _dashboardSettings;
    private readonly IAuthenticationService _authenticationService;
    private readonly ICrawlerService _crawlerService;

    private readonly Dictionary<string, string> _dashboardsToProcessByResourceType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "microsoft.app/containerapps", "azure-container-apps-container-app-view" },
            { "microsoft.storage/storageaccounts", "azure-insights-storage-accounts" },
            { "microsoft.documentdb/databaseaccounts", "azure-insights-cosmos-db" },
            { "microsoft.cache/redis", "azure-redis" },
            { "microsoft.web/sites", "azure-app-service-monitoring" },
            // Pending: webapp, sql
        };

    // Define the allowed Kubernetes resource types
    private readonly string[] allowedTypes = { "namespaces", "deployments", "statefulsets" };

    public GraphService(IGraphDatabaseClient graphDatabaseClient, DashboardSettings dashboardSettings, ILogger<GraphService> logger, IHttpClientFactory httpClientFactory, IAuthenticationService authenticationService, ICrawlerService crawlerService)
    {
        _graphDatabaseClient = graphDatabaseClient;
        _logger = logger;
        _dashboardSettings = dashboardSettings;

        _grafanaUrl = dashboardSettings.GrafanaUrl.TrimEnd('/');
        _httpClientFactory = httpClientFactory;
        _authenticationService = authenticationService;
        _crawlerService = crawlerService;
    }

    private async Task<HttpClient> GetHttpClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Clear();
        var token = await _authenticationService.GetGrafanaAccessToken();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        return client;
    }

    public async Task<ResultSet<dynamic>> QuerySubscriptionsAsync()
    {
        _logger.LogInternalInformation("Querying subscriptions from graph database");
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

    public async Task<ResultSet<dynamic>> GetResourceTypesAsync()
    {
        var resourceTypes = new List<dynamic>
        {
            ArmConstants.ContainerAppType.ToLower(),
            ArmConstants.AppServiceType.ToLower(),
            ArmConstants.AzureKubernetesServiceType.ToLower(),
            ArmConstants.AzureKubernetesServiceDeploymentType.ToLower(),
            ArmConstants.AzureKubernetesServiceStatefulSetType.ToLower()
        };

        return new ResultSet<dynamic>(resourceTypes, new Dictionary<string, object>());
    }

    public async Task<List<IGraphService.AppGroupWithRepo>> GetAppGroupsWithRepo()
    {
        _logger.LogInternalInformation("Querying app groups with repositories from graph database");
        string query = $@"g.V().has('resourceType', within('{ArmConstants.ContainerAppType.ToLower()}', '{ArmConstants.AppServiceType.ToLower()}', '{ArmConstants.AzureKubernetesServiceType.ToLower()}', '{ArmConstants.AzureKubernetesServiceDeploymentType.ToLower()}', '{ArmConstants.AzureKubernetesServiceStatefulSetType.ToLower()}'))
                         .project('resourceId', 'name', 'type', 'repo', 'linkedTimestamp', 'clusterResourceId')
                         .by(coalesce(values('resourceId'), constant('')))
                         .by(coalesce(values('resourceName'), constant('')))
                         .by(label())
                         .by(
                                coalesce(
                                    out('{ArmConstants.Relationships.ServesCode}').values('resourceId'),
                                    constant('')
                                )
                         )
                         .by(
                                coalesce(
                                    out('{ArmConstants.Relationships.ServesCode}').values('updateTs'),
                                    constant(0)
                                )
                         )
                         .by(coalesce(values('clusterResourceId'), constant('')))";

        var azureResourceApps = await _graphDatabaseClient.Query<Dictionary<string, object>>(query);
        _logger.LogInternalInformation("Found {count} app groups with repositories", azureResourceApps.Count);
        return azureResourceApps.Select(item =>
        {
            var resourceId = item["resourceId"]?.ToString() ?? string.Empty;
            string name = item["name"]?.ToString() ?? string.Empty;
            string type = item["type"]?.ToString() ?? string.Empty;
            string repoUrl = item["repo"]?.ToString() ?? string.Empty;
            long linkedTimestamp = Convert.ToInt64(item["linkedTimestamp"] ?? 0);

            if (type == ArmConstants.AzureKubernetesServiceDeploymentType.ToLower() || type == ArmConstants.AzureKubernetesServiceStatefulSetType.ToLower())
            {
                // For Kubernetes resources, use the clusterResourceId
                resourceId = item["clusterResourceId"]?.ToString() ?? string.Empty;
            }

            return new IGraphService.AppGroupWithRepo(resourceId, name, type, repoUrl, linkedTimestamp == 0 ? null : new DateTime(linkedTimestamp, DateTimeKind.Utc));
        }).ToList();
    }

    public async Task<ResultSet<dynamic>> GetAppGroupsBySubscriptionAsync(string subscriptionId, string? resourceType = null)
    {
        _logger.LogInternalInformation("Querying app groups for subscription {subscriptionId}", subscriptionId);
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            throw new ArgumentException("Subscription ID cannot be null or empty", nameof(subscriptionId));
        }
        var resourceTypeFilter = $@"within(
                            '{ArmConstants.ContainerAppType.ToLower()}',
                            '{ArmConstants.AppServiceType.ToLower()}',
                            '{ArmConstants.AzureKubernetesServiceType.ToLower()}'
                        )";
        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            if (resourceType.Contains("k8s/"))
            {
                resourceTypeFilter = $@"within(
                            '{ArmConstants.AzureKubernetesServiceType.ToLower()}'
                        )";
            }
            else
            {
                resourceTypeFilter = $@"within(
                            '{resourceType.ToLower()}'
                        )";
            }
        }

        string query = $@"g.V().has('subscriptionId', '{subscriptionId.ToLower()}')
                        .has('resourceType', {resourceTypeFilter})
                        .project('id', 'name', 'type', 'properties')
                        .by(id())
                        .by(coalesce(values('resourceName'), constant('')))
                        .by(label())
                        .by(valueMap())";

        var azureResourceApps = await _graphDatabaseClient.Query(query);

        // Check if we have any AKS resources in the results
        var aksResources = azureResourceApps.Where(resource =>
            resource["type"].ToString().Equals(ArmConstants.AzureKubernetesServiceType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (aksResources.Any())
        {
            var k8sResourceTypeFilter = $@"within(
                            '{ArmConstants.AzureKubernetesServiceDeploymentType.ToLower()}',
                            '{ArmConstants.AzureKubernetesServiceStatefulSetType.ToLower()}'
                        )";
            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                k8sResourceTypeFilter = $@"within(
                            '{resourceType.ToLower()}'
                        )";
            }

            _logger.LogInternalInformation("Found {count} AKS resources, fetching their deployments and statefulsets", aksResources.Count);

            var allResults = new List<dynamic>(azureResourceApps);

            foreach (var aksResource in aksResources)
            {
                // Replace direct property access with GetFirstValueAsString method
                string aksResourceId = GetFirstValueAsString(aksResource["properties"] as IDictionary<string, object>, "resourceId");
                if (string.IsNullOrWhiteSpace(aksResourceId))
                {
                    _logger.LogInternalWarning("AKS resource ID is null or empty, skipping query for deployments and statefulsets.");
                    continue;
                }
                _logger.LogInternalInformation("Querying deployments and statefulsets for AKS clusterResourceId {resourceId}", aksResourceId);

                // Query to get deployments and statefulsets for this AKS cluster
                string k8sQuery = $@"g.V().has('clusterResourceId', '{aksResourceId}')
                            .has('resourceType',  {k8sResourceTypeFilter})
                            .project('id', 'name', 'type', 'properties')
                            .by(id())
                            .by(coalesce(values('resourceName'), constant('')))
                            .by(label())
                            .by(valueMap())";

                var k8sResources = await _graphDatabaseClient.Query(k8sQuery);

                if (k8sResources != null && k8sResources.Any())
                {
                    _logger.LogInternalInformation("Found {count} deployments/statefulsets for AKS resource {resourceId}",
                        k8sResources.Count, aksResourceId);
                    allResults.AddRange(k8sResources);
                }
            }

            return new ResultSet<dynamic>(allResults, new Dictionary<string, object>());
        }

        return azureResourceApps;
    }

    private async Task<ResultSet<dynamic>> GetRelatedResourcesAsync(string resourceId, int hops)
    {
        string query = $@"g.V().has('id', '{resourceId.ToLower().Replace("/", "_")}')
                    .union(
                        repeat(
                            union(
                                outE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS', 'SERVES_CODE').inV(),
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

        // If it's a Kubernetes resource, we should use a simplified query that only includes
        // direct outgoing relationships to reduce complexity and improve performance
        if (resourceId.Contains("microsoft.containerservice_managedclusters"))
        {
            query = $@"g.V().has('id', '{resourceId.ToLower().Replace("/", "_")}')
                    .union(
                        outE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS', 'SERVES_CODE', 'REFERENCES').inV()
                        .not(has('resourceType', within('resourcegroups', 'subscription')))
                    )
                    .dedup()
                    .project('id', 'name', 'type', 'properties')
                    .by(id())
                    .by(coalesce(values('resourceName'), constant('')))
                    .by(label())
                    .by(valueMap())";
        }

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

        // Only apply K8s filter for Azure Kubernetes Service resources
        Func<dynamic, bool> nodeFilter = resource => true; // Default to allow all resources
        if (resourceId.Contains("microsoft.containerservice_managedclusters") && !resourceId.Contains("namespaces"))
        {
            nodeFilter = K8sResourceFilter;
        }

        var appGroupItems = await ProcessResourceHierarchyAsync(resourceId, processedNodes, hops, nodeFilter);

        return new ResultSet<AppGroupItem>(appGroupItems, new Dictionary<string, object>());
    }

    // Renamed filter method to determine if a Kubernetes resource should be included
    private bool K8sResourceFilter(dynamic resource)
    {
        // If it's a Kubernetes resource, check if it's an allowed type
        bool isKubernetesResource = resource["type"].StartsWith("k8s/", StringComparison.OrdinalIgnoreCase);

        if (isKubernetesResource)
        {
            // Skip if the resource type doesn't contain any of the allowed types
            return allowedTypes.Any(type => resource["type"].Contains(type, StringComparison.OrdinalIgnoreCase));
        }

        // Include all non-Kubernetes resources
        return true;
    }

    // Recursive method to explore the connected resources for a given resource
    private async Task<List<AppGroupItem>> ProcessResourceHierarchyAsync(string resourceId, HashSet<string> processedNodes, int remainingLevels, Func<dynamic, bool> nodeFilter)
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

            // Apply the node filter function
            if (!nodeFilter(resource))
            {
                continue;
            }

            var childItems = await ProcessResourceHierarchyAsync(relatedResourceId, processedNodes, remainingLevels - 1, nodeFilter);

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
        _logger.LogInternalInformation("Fetching all resource nodes from the graph database.");
        var allResourceNodes = await _graphDatabaseClient.Query("g.V().project('resourceType', 'resourceName','resourceGroupName','subscriptionId', 'resourceId', 'properties').by(coalesce(values('resourceType'), constant('MISSING'))).by(coalesce(values('resourceName'), constant('MISSING'))).by(coalesce(values('resourceGroupName'), constant('MISSING'))).by(coalesce(values('subscriptionId'), constant('MISSING'))).by(coalesce(values('resourceId'), constant('MISSING'))).by(valueMap())");

        if (allResourceNodes is null || allResourceNodes.Count == 0)
        {
            _logger.LogInternalWarning("No resource nodes found in the graph database.");
            return [];
        }

        _logger.LogInternalInformation($"Fetched {allResourceNodes.Count} resource nodes from the graph database.");

        var resources = new List<ArmResourceNode>();
        foreach (var node in allResourceNodes)
        {
            if (node is IDictionary<string, object> dict)
            {
                AppHealthInfo appHealthInfo = null;

                if (dict.TryGetValue("properties", out var propertiesObj) && propertiesObj != null)
                {
                    var properties = (IDictionary<string, object>)propertiesObj;

                    if (properties.TryGetValue("appHealthInfo", out var appHealthInfoObj) && appHealthInfoObj != null)
                    {
                        var options = new JsonSerializerOptions
                        {
                            IncludeFields = true,
                        };

                        var jsonStringList = ((IEnumerable<object>)appHealthInfoObj)
                            .OfType<string>()
                            .ToList();

                        if (jsonStringList.Count > 0 && jsonStringList[0] != null)
                        {
                            // Deserialize the first object (or all, if needed)
                            appHealthInfo = JsonSerializer.Deserialize<AppHealthInfo>(jsonStringList[0], options);
                        }
                    }
                }

                var armResourceNode = new ArmResourceNode
                {
                    ResourceType = node["resourceType"],
                    ResourceName = node["resourceName"],
                    ResourceGroupName = node["resourceGroupName"],
                    SubscriptionId = node["subscriptionId"],
                    ResourceId = node["resourceId"],
                    AppHealthInfo = appHealthInfo
                };

                resources.Add(armResourceNode);
            }
        }

        return resources;
    }

    public async Task<ResultSet<dynamic>> GetGraphResourceAsync(string resourceId)
    {
        _logger.LogInternalInformation("Querying graph resource {resourceId}", resourceId);
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
                    _logger.LogInternalWarning("Properties not found or null");
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
                        using var httpClient = await GetHttpClient();
                        var dashboardResponse = await httpClient.GetAsync($"{_grafanaUrl}/api/search?type=dash-db");
                        dashboardResponse.EnsureSuccessStatusCode();
                        var dashboardsContent = await dashboardResponse.Content.ReadAsStringAsync();
                        var dashboards = JsonSerializer.Deserialize<JsonElement>(dashboardsContent);

                        foreach (var dashboard in dashboards.EnumerateArray())
                        {
                            if (dashboard.TryGetProperty("url", out var urlElement) &&
                                urlElement.GetString().Contains(dashboardType, StringComparison.OrdinalIgnoreCase))
                            {
                                dashboardUrl = $"{_grafanaUrl}{urlElement.GetString()}";
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning(ex, "Failed to get dashboard URL from API, using base URL");
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
                _logger.LogInternalWarning(ex, "Failed to add dashboard URL to result");
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

        _logger.LogInternalInformation("Updating properties for resource {resourceId}", resourceId);

        // check if the vertex exists
        string checkQuery = $"g.V().has('id', '{resourceId}').count()";

        var checkResult = await _graphDatabaseClient.Query(checkQuery);

        if (checkResult == null || !checkResult.Any() || Convert.ToInt64(checkResult.First()) == 0)
        {
            _logger.LogInternalWarning($"Resource {resourceId} not found in the graph database");
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
            _logger.LogInternalInformation("Successfully updated properties for resource {resourceId}", resourceId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to update properties for resource {resourceId}", resourceId);
            throw;
        }
    }

    public Task<CrawlerResult> GetGraphProgressAsync()
    {
        return _crawlerService.GetCrawlerResult();
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
