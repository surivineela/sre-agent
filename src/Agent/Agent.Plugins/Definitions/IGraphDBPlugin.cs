// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Graph.Schema;
using Gremlin.Net.Driver;

namespace Agent.Plugins
{
    public interface IGraphDBPlugin
    {
        string? ThreadId { get; set; }
        
        Task<ResultSet<dynamic>> Query(string query);
        
        // Find all network resources for an app (container app for now)
        // TODO: Refactor to work with all resource types
        Task<string> FindAllNetworkConnectedResources(string resourceId = "");

        // Get all resources that are part of an application context starting from a resource
        Task<List<Node>> GetApplicationComponentsSummary(string resourceId, int hops = 3);

        Task<string> VisualizeApplicationComponents(string resourceId, int hops = 3, Guid? threadId = null);

        // Group resources into logical applications based on connectivity patterns
        Task<List<ApplicationGraph>> DiscoverApplications(string subscriptionId);
        Task AddSourceCodeNodeToContainerAppNodeAsync(string resourceId, string repoUrl);
        Task<List<string>> GetContainerAppsWithNodesWithoutSourceCodeNodesAsync();
    }
}
