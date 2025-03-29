// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Graph.Schema;
using Gremlin.Net.Driver;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class GraphDBPluginDefinition
    {
        public IGraphDBPlugin _plugin { get; }
        public GraphDBPluginDefinition(IGraphDBPlugin graphDBPlugin)
        {
            _plugin = graphDBPlugin;
        }

        /// <summary>
        /// When implementing this in prod, we need to give this agent a read-only user
        /// </summary>
        [KernelFunction("query")]
        [Description("Run a generic query against the graph database. Do NOT perform any write operations.")]
        public async Task<ResultSet<dynamic>> Query(string query)
        {
            return await _plugin.Query(query);
        }

        [KernelFunction("FindAllNetworkConnectedResources")]
        [Description("Finds all resources that a particular Azure Container App connects to through network connections, such as Redis caches, databases, and other services. Useful for networking connectivity debug")]
        public async Task<string> FindAllNetworkConnectedResources(
            [Description("Azure Resource Id of the Container App, should begin with /subscriptions...., Example: /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo/providers/microsoft.app/containerapps/iot-dashboard")] string resourceId = "")
        {
            return await _plugin.FindAllNetworkConnectedResources(resourceId);
        }

        [KernelFunction("GetApplicationComponentsSummary")]
        [Description("Returns a structured list of Azure resources that are connected to a specified resource. " +
            "This function is best used when you need to: 1) Get a quick overview of what resources are part of an application, " +
            "2) See resource names, types, and IDs in a list format, " +
            "3) Programmatically process the connected resources, or " +
            "4) Present a text-based summary to the user." +
            " The output is a List<Node> where each Node contains id, name, type, and essential properties. " +
            "Use this instead of VisualizeApplicationComponents when you don't need to show the relationships between resources.")]
        public async Task<List<Node>> GetApplicationComponentsSummary(
            [Description("Azure Resource Id of the application resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Maximum number of relationship hops to traverse in the graph. Higher values (1-5) will discover more distant relationships but may include unrelated resources. Default is 3.")] int hops = 3)
        {
            return await _plugin.GetApplicationComponentsSummary(resourceId, hops);
        }

        [KernelFunction("VisualizeApplicationComponents")]
        [Description("Creates an interactive visual diagram showing how Azure resources are connected to each other. " +
            "This function is best used when you need to: 1) Show the topology and relationships between resources, " +
            "2) Help users understand the architecture visually, 3) Debug connectivity issues, or " +
            "4) Present a complete picture of the application's infrastructure. " +
            "The output includes both nodes (resources) and edges (relationships) in a format suitable for visualization. " +
            "Use this instead of GetApplicationComponentsSummary when users ask to 'show', 'visualize', 'draw', or 'diagram' the connections, or when they need to understand how resources are linked together." +
            "This method will return the graph as base64 encoded string with the format: 'data:image/png;base64,base64Image', which you can use to render on the chat message." +
            "Azure Resource Id of the application resource to visualize. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")]
        public async Task<string> VisualizeApplicationComponents(
            [Description("Azure Resource Id of the application resource to visualize. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Maximum number of relationship hops to include in the visualization. Higher values (1-5) show more distant connections but may make the diagram more complex. Default is 3.")] int hops = 3,
            Guid? threadId = null)
        {
            return await _plugin.VisualizeApplicationComponents(resourceId, hops, threadId);
        }

        [KernelFunction("DiscoverApplications")]
        [Description("Analyzes an Azure subscription and returns a List<ApplicationGraph> where each ApplicationGraph represents a distinct application. " +
            "Each ApplicationGraph contains: id, name, entryPoint (the main resource Node), nodes (List<Node> of all related resources), and edges (List<Edge> showing relationships between nodes). " +
            "Entry points are identified from Container Apps, App Services, and AKS clusters. " +
            "The function maps out complete application topologies including all connected resources and their relationships. Returns an empty list if no applications are found or on error.")]
        public async Task<List<ApplicationGraph>> DiscoverApplications(
            [Description("Azure Subscription Id to analyze. This is the GUID identifier for the subscription, found in the subscription's overview page or in the resource ID after '/subscriptions/'")] string subscriptionId)
        {
            return await _plugin.DiscoverApplications(subscriptionId);
        }

        [KernelFunction("AddSourceCodeNodeToContainerAppNode")]
        [Description("Adds the GitHub repo url node and an edge from the container app node to it")]
        public async Task AddSourceCodeNodeToContainerAppNode(
            [Description("Azure Resource Id of the Container App, should begin with /subscriptions...., Example: /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo/providers/microsoft.app/containerapps/iot-dashboard")] string resourceId = "",
            [Description("GitHub repository url, should begin with https://github.com/..., Example: https://github.com/{ORG_NAME}/{REPO_NAME}")] string repoUrl = "")
        {
            await _plugin.AddSourceCodeNodeToContainerAppNodeAsync(resourceId: resourceId, repoUrl: repoUrl);
        }

        [KernelFunction("GetContainerAppsWithNodesWithoutSourceCodeNodes")]
        [Description("Gets a list of container apps with nodes in the graph that don't have edges connecting them to source code nodes")]
        public async Task GetContainerAppsWithNodesWithoutSourceCodeNodes()
        {
            await _plugin.GetContainerAppsWithNodesWithoutSourceCodeNodesAsync();
        }
    }
}
