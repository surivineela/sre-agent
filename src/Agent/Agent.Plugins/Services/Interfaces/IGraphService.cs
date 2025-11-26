// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Schema;
using Gremlin.Net.Driver;
namespace Agent.Plugins.Services.Interfaces;

public class AppGroupItem
{
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public required string Type { get; set; }
    public required string ResourceId { get; set; }
    public AppHealthInfo? AppHealthInfo { get; set; } // this is a JSON string of the properties
    public List<AppGroupItem>? SubItems { get; set; } // this is children of the resource
    public string? RelationToParent { get; set; } // this is the edge label to the parent resource
    public bool? IsRelationReversed { get; set; } = false; // this is true if the relation is reversed
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

    // RepoUrl is empty if no source code repo is linked to the app group
    public record AppGroupWithRepo(string ResourceId, string Name, string Type, string RepoUrl, DateTime? LinkedTimestamp, string? Namespace);
    /// <summary>
    /// Get all app groups in the graph database with their associated code repositories if exists.
    /// </summary>
    /// <param name="appGroupId"></param>
    /// <returns></returns>
    Task<List<AppGroupWithRepo>> GetAppGroupsWithRepo();

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

    /// <summary>
    /// Search for resources in the graph database by name and/or type with pagination
    /// </summary>
    /// <param name="name">Optional name filter (case-insensitive partial match)</param>
    /// <param name="type">Optional resource type filter (exact match, case-insensitive)</param>
    /// <param name="subscriptionId">Optional subscription ID filter</param>
    /// <param name="pageIndex">Zero-based page index</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of resources</returns>
    Task<(List<ResourceSearchResult> Resources, int TotalCount)> SearchResourcesAsync(
        string? name = null,
        string? type = null,
        string? subscriptionId = null,
        int pageIndex = 0,
        int pageSize = 20);
}
