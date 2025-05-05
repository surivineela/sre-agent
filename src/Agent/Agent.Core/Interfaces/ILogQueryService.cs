// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Services;

namespace Agent.Core.Interfaces;
public interface ILogQueryService
{
    /// <summary>
    /// Retrieves all saved queries from query packs in a subscription.
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID to search for query packs</param>
    /// <returns>Collection of query information objects containing query details</returns>
    Task<IEnumerable<QueryInfo>> GetSavedQueriesForSubscriptionAsync(string subscriptionId);

    /// <summary>
    /// Executes a Kusto query against Log Analytics workspaces in a subscription.
    /// The method will attempt to execute the query on each workspace until it succeeds.
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID containing Log Analytics workspaces</param>
    /// <param name="queryText">Kusto query to execute</param>
    /// <param name="startTime">Start time for the query time range</param>
    /// <param name="endTime">End time for the query time range</param>
    /// <returns>JSON string containing query results or error message</returns>
    Task<string> ExecuteLogQueryAsync(string subscriptionId, string queryText, DateTimeOffset startTime, DateTimeOffset endTime);
}
