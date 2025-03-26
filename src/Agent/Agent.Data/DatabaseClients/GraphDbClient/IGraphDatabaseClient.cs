// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Gremlin.Net.Driver;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public interface IGraphDatabaseClient
    {
        /// <summary>
        /// Adds or updates a node in the graph.
        /// </summary>
        /// <param name="nodeId">The unique identifier of the node.</param>
        /// <param name="resourceType">The type of the resource.</param>
        /// <param name="properties">A dictionary of properties to associate with the node.</param>
        /// <returns>A boolean indicating whether the node was added (true) or updated (false).</returns>
        Task<bool> AddOrUpdateNodeAsync(
            string nodeLabel,
            string nodeId,
            string resourceType,
            IDictionary<string, object> properties);

        /// <summary>
        /// Adds or updates an edge in the graph.
        /// </summary>
        /// <param name="sourceNodeId">The unique identifier of the source node.</param>
        /// <param name="targetNodeId">The unique identifier of the target node.</param>
        /// <param name="relationshipType">The type of relationship between the nodes.</param>
        /// <param name="properties">edge properties.</param>
        /// <returns>A boolean indicating whether the edge was added (true) or already existed (false).</returns>
        Task<bool> AddOrUpdateEdgeAsync(
            string sourceNodeId,
            string targetNodeId,
            string relationshipType,
            IDictionary<string, object> properties = null);

        /// <summary>
        /// Clears the graph.
        /// </summary>
        /// <returns></returns>
        Task Clear();

        /// <summary>
        /// Queries the graph.
        /// </summary>
        /// <returns></returns>
        Task<ResultSet<dynamic>> Query(string query, int maxMessageSize = 200000);
    }
}
