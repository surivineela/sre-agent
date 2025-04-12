// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Gremlin.Net.Driver;
using ArmConstants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Core.Services;

public class GraphService : IGraphService
{
    private readonly IGraphDatabaseClient _graphDatabaseClient;
    private readonly ILogger<GraphService> _logger;

    public GraphService(IGraphDatabaseClient graphDatabaseClient, ILogger<GraphService> logger)
    {
        _graphDatabaseClient = graphDatabaseClient;
        _logger = logger;
    }

    public async Task<ResultSet<dynamic>> QuerySubscriptionsAsync()
    {
        _logger.LogInformation("Querying subscriptions from graph database");
        string query = @"g.V().has('resourceType', 'subscriptions')
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
                        .out('{ArmConstants.Relationships.Contains}')
                        .out('{ArmConstants.Relationships.Contains}')
                        .hasLabel(within(
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
                       identity(),
                       repeat(
                           union(
                               inE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS').outV()
                           )
                           .not(has('resourceType', within('resourcegroup', 'subscription')))
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
                ScoreCard = properties != null && properties.ContainsKey("scorecard") ? properties["scorecard"] as Scorecard : null,
                SubItems = subItems
            };
            appGroupItems.Add(appGroupItem);
        }

        return appGroupItems;
    }

    public async Task<ResultSet<AppGroupItem>> GetAppGroupResourcesAsync(string resourceId)
    {
        // Initialize an empty list to hold AppGroupItem objects
        var appGroupItems = new List<AppGroupItem>();

        // Stacy To DO add first app group item to return list

        // First hop: Get related resources for the given resourceId
        var resultSet = await GetRelatedResourcesAsync(resourceId, 1);

        // Iterate through the first hop results to fetch second hop related resources
        var secondHopAppGroupItems = new List<AppGroupItem>();
        foreach (var resource in resultSet)
        {
            var relatedResources = await GetRelatedResourcesAsync(resource["id"], 1); // Second hop
            var thirdHopAppGroupItems = new List<AppGroupItem>();

            // Iterate through the second hop results to fetch third hop related resources
            foreach (var relatedResource in relatedResources)
            {
                var thirdHopResources = await GetRelatedResourcesAsync(relatedResource["id"], 1); // Third hop
                thirdHopAppGroupItems = ConvertToAppGroupItems(thirdHopResources, relatedResource["id"], null);
            }

            secondHopAppGroupItems = ConvertToAppGroupItems(relatedResources, resource["id"], thirdHopAppGroupItems);
        }

        appGroupItems.AddRange(ConvertToAppGroupItems(resultSet, resourceId, secondHopAppGroupItems));

        // Create and return a ResultSet<AppGroupItem> with the collected AppGroupItems
        return new ResultSet<AppGroupItem>(appGroupItems, new Dictionary<string, object>());
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

        return await _graphDatabaseClient.Query(query);
    }


    public class AppGroupItem
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string ResourceId { get; set; }
        public Scorecard? ScoreCard { get; set; } // this is a JSON string of the properties
        public List<AppGroupItem>? SubItems { get; set; } // this is children of the resource
    }
}
