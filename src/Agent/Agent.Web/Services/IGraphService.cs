// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Gremlin.Net.Driver;
using static Agent.Core.Services.GraphService;
namespace Agent.Core.Services;

public interface IGraphService
{
    /// <summary>
    /// Get the list of subscriptions from the graph database
    /// </summary>
    /// <returns>The subscriptions</returns>
    Task<ResultSet<dynamic>> QuerySubscriptionsAsync();
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
    /// <returns>The app groups</returns>
    Task<ResultSet<dynamic>> GetAppGroupsBySubscriptionAsync(string subscriptionId);
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
}
