// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Schema;
using Gremlin.Net.Driver;
namespace Agent.Runtime.Services;

public class AppGroupItem
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string ResourceId { get; set; }
    public AppHealthInfo? AppHealthInfo { get; set; } // this is a JSON string of the properties
    public List<AppGroupItem>? SubItems { get; set; } // this is children of the resource
}

public interface IGraphService
{
    /// <summary>
    /// Get the list of subscriptions from the graph database
    /// </summary>
    /// <returns>The subscriptions</returns>
    Task<ResultSet<dynamic>> QuerySubscriptionsAsync();

    /// <summary>
    /// Get the list of all available resource types in the graph
    /// </summary>
    Task<ResultSet<dynamic>> GetResourceTypesAsync();

    /// <summary>
    /// Query the graph database with a given query
    /// </summary>
    /// <param name="query">The query to execute</param>
    /// <returns>The results of the query</returns>
    Task<ResultSet<dynamic>> QueryAsync(string query);

    /// <summary>
    /// Get the app groups for a given subscription
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="resourceType">The resource type to filter by (optional)</param>
    /// <returns>The app groups</returns>
    Task<ResultSet<dynamic>> GetAppGroupsBySubscriptionAsync(string subscriptionId, string? resourceType = null);

    /// <summary>
    /// Get all connected resources for a given app group
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="appGroupId">The app group ID</param>
    /// <param name="resourceId">The resource ID</param>
    /// <returns>The resources</returns>
    Task<ResultSet<AppGroupItem>> GetAppGroupResourcesAsync(string resourceId);

    /// <summary>
    /// Get the resource details for a given resource ID
    /// </summary>
    /// <param name="resourceId">The resource ID</param>
    /// <returns>The graph resource</returns>
    Task<ResultSet<dynamic>> GetGraphResourceAsync(string resourceId);

    /// <summary>
    /// Returns current status of the resource graph generation
    /// Will return either a completed status or an estimated percentage of completion.
    /// </summary>
    /// <returns></returns>
    Task<CrawlerResult> GetGraphProgressAsync();

    Task<List<ArmResourceNode>> GetAllResourceNodes();

    /// <summary>
    /// Update the propertie(s) of a graph resource.
    /// </summary>
    /// <param name="resourceId">Resource id of the graph node.</param>
    /// <param name="property">Properties to update.</param>
    /// <returns></returns>
    Task<ResultSet<dynamic>> UpdateGraphResourceProperties(string resourceId, IDictionary<string, string> properties);
}
