// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
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
        [Description("Returns a structured list of Azure resources connected to a specified resource. " +
            "This function is best used when you need to: 1) Get overview of what resources are part of an application, " +
            "2) See resource names, types, and IDs as a list, " +
            "3) Programmatically process the connected resources, or " +
            "4) Present a text-based summary" +
            " The output is a List<Node> where each Node contains id, name, type, essential properties. " +
            "Use this instead of VisualizeApplicationComponents when you don't need to show the relationships between resources.")]
        public async Task<List<Node>> GetApplicationComponentsSummary(
            [Description("Azure Resource Id of the application resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Maximum number of relationship hops to traverse in the graph. Higher values (1-5) will discover more distant relationships but may include unrelated resources. Default is 3.")] int hops = 3)
        {
            return await _plugin.GetApplicationComponentsSummary(resourceId, hops);
        }

        [KernelFunction("VisualizeApplicationComponents")]
        [Description("Creates an interactive visual diagram showing how Azure resources are connected. " +
    "Use this to: 1) Show the topology/relationships between resources, " +
    "2) Help users understand the architecture visually, 3) Debug connectivity issues, or " +
    "4) Present a complete picture of the application's infrastructure. " +
    "The output includes nodes (resources) and edges (relationships). " +
    "Use instead of GetApplicationComponentsSummary when users ask to 'show', 'visualize', 'draw', or 'diagram' the connections. " +
    "Returns the graph as a base64-encoded string. Input: Azure Resource Id of the application resource to visualize.")]
        public async Task<string> VisualizeApplicationComponents(
            [Description("Azure Resource Id of the application resource to visualize. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Maximum number of relationship hops to include in the visualization. Higher values (1-5) show more distant connections but may make the diagram more complex. Default is 3.")] int hops = 3,
            Guid? threadId = null)
        {
            return await _plugin.VisualizeApplicationComponents(resourceId, hops, threadId);
        }

        [KernelFunction("DiscoverApplications")]
        [Description("Analyzes an Azure subscription and returns a List<ApplicationGraph>, where each ApplicationGraph represents a distinct application. " +
    "Each ApplicationGraph contains: id, name, entryPoint (main resource Node), nodes (List<Node> of related resources), and edges (List<Edge> showing relationships). " +
    "Entry points are identified from Container Apps, App Services, and AKS clusters. " +
    "The function maps out application topologies, including all connected resources and relationships. " +
    "Returns an empty list if no applications are found.")]
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
        public async Task<List<string>> GetContainerAppsWithNodesWithoutSourceCodeNodes()
        {
            return await _plugin.GetContainerAppsWithNodesWithoutSourceCodeNodesAsync();
        }

        [KernelFunction("UpdateRepoNodeWithLastScanTime")]
        [Description("Updates the source code node's lastScanTime property with the updated scan time.")]
        public async Task UpdateRepoNodeWithLastScanTime(string repoUrl)
        {
            await _plugin.UpdateRepoNodeWithLastScanTime(repoUrl);
        }

        [KernelFunction("GetGeneralHealth")]
        [Description("Retrieves dashboard metrics for a specific Azure resource and generates an AI-powered health summary. " +
            "This function is useful when you need to: 1) Get a quick health assessment of a resource/general health of the resource for questions like how i my resource doing?, " +
            "2) Understand performance trends and potential issues, " +
            "3) View summarized metrics without accessing the Azure portal, or " +
            "4) Get actionable insights about resource behavior. " +
            "The output is a text summary that describes the resource's health status, important metrics, and any anomalies or concerns.")]
        public async Task<string> GetGeneralHealth(
            [Description("Name of the Azure resource to analyze. This should be the exact resource name as shown in the Azure portal.")] string resourceName,
            [Description("Type of the Azure resource (e.g., 'microsoft.app/containerapps', 'microsoft.storage/storageaccounts', 'microsoft.documentdb/databaseaccounts', 'microsoft.cache/redis')")] string resourceType)
        {
            return await _plugin.GetGeneralHealthAsync(resourceName, resourceType);
        }

        [KernelFunction("SearchResource")]
        [Description("Searches for Azure resources by name pattern and resource type in the knowledge graph. " +
            "This function is useful when you need to: 1) Find specific resources without knowing the exact resource ID, " +
            "2) Locate resources of a particular type across your Azure environment, " +
            "3) Find resources matching a naming pattern, or " +
            "4) Verify if resources exist before performing operations on them. " +
            "Returns a list of matching resources with their details.")]
        public async Task<List<ArmResourceNode>> SearchResource(
            [Description("Partial or complete name of the resource to search for. The search is case-insensitive and will match any resource containing this string.")] string resourceName,
            [Description("Type of the Azure resource to search for (e.g., 'microsoft.app/containerapps', 'microsoft.storage/storageaccounts')")] string resourceType)
        {
            return await _plugin.SearchResourceAsync(resourceName, resourceType);
        }

        [KernelFunction("SearchResourceByName")]
        [Description("Searches for Azure resources by name pattern only in the knowledge graph. " +
            "This function is useful when you need to: 1) Find specific resources without knowing the exact resource ID, " +
            "2) Find resources matching a naming pattern, or " +
            "3) Verify if resources exist before performing operations on them. " +
            "Returns a list of matching resources with their details.")]
        public async Task<List<ArmResourceNode>> SearchResourceByName(
        [Description("Partial or complete name of the resource to search for. The search is case-insensitive and will match any resource containing this string.")] string resourceName)
        {
            return await _plugin.SearchResourceByNameAsync(resourceName);
        }

        [KernelFunction("GetResourceCount")]
        [Description("Gets the count of Azure resources of a specified type in the knowledge graph. " +
            "This function is useful when you need to: 1) Get an inventory of resources by type, " +
            "2) Validate quantity of deployed resources against expected counts, " +
            "3) Monitor resource proliferation over time, or " +
            "4) Get statistics about your Azure environment composition. " +
            "Returns a count of matching resources and can group by specific properties.")]
        public async Task<dynamic> GetResourceCount(
            [Description("Type of the Azure resource to count (e.g., 'microsoft.app/containerapps' for container apps, 'microsoft.web/sites' for webapps, function apps, 'microsoft.containerservice/managedclusters' for AKS)")] string resourceType,
            [Description("Optional. Property to group by for getting counts by specific attribute (currently only allowed 'location', 'resourceGroup'). Leave empty for total count.")] string groupBy = "")
        {
            return await _plugin.GetResourceCountAsync(resourceType, groupBy);
        }

        [KernelFunction("ListSubscriptions")]
        [Description("Returns a list of all Azure subscription IDs present in the knowledge graph. " +
            "This function is useful when you need to: 1) Discover available subscriptions, " +
            "2) Verify subscription visibility to the agent, " +
            "3) Get subscription IDs for use with other commands, or " +
            "4) Perform an inventory of monitored subscriptions. " +
            "The output is a list of subscription IDs without additional details.")]
        public async Task<List<dynamic>> ListSubscriptions()
        {
            return await _plugin.ListSubscriptionsAsync();
        }

        [KernelFunction("GetActivityLogsSummary")]
        [Description("Retrieves and analyzes Azure Activity Logs for a resource and its connected components. " +
            "This function is valuable when you need to: 1) Review recent changes made to a resource and its dependencies, " +
            "2) Investigate who made specific configuration changes, " +
            "3) Understand patterns of administrative activity, or " +
            "4) Detect potentially unauthorized or unusual operations. " +
            "The output is a natural language summary highlighting key activities, patterns, and potential concerns.")]
        public async Task<string> GetActivityLogsSummary(
            [Description("Azure Resource Id of the resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Number of days of activity logs to retrieve and analyze. Default is 1 days.")] int daysBack = 1,
            Guid? threadId = null)
        {
            return await _plugin.FetchAndSummarizeActivityLogs(resourceId, daysBack, threadId);
        }

        [KernelFunction("ListResourcesByType")]
        [Description("Returns a list of Azure resources of a specified type with their property details as recorded in the knowledge graph. " +
            "This function is useful when you need to: 1) Get an inventory of resources of a specific type, " +
            "2) Examine tracked configuration properties of resources, " +
            "3) Gather metadata for resources across your Azure environment, or " +
            "The output is a list of resource objects with all their properties. " +
            "Each resource includes details like name, location, resource group, and type-specific configuration.")]
        public async Task<List<Dictionary<string, object>>> ListResourcesByType([Description("The Azure resource type to query (e.g., 'microsoft.app/containerapps', 'microsoft.compute/virtualmachines', 'microosft.web/sites' for webapps/app serivce")]string resourceType)
        {
            return await _plugin.ListResourcesByTypeAsync(resourceType);
        }

        [Description("Returns a general dashboard provided as daily reports for Resource Counts recorded in the knowledge graph. " +
            "This function is useful when you need to: 1) Need to provide a URL to the daily dashboard " +
            "2) Provide a very generic dashboard for the knowledge graph overview at a very high level." +
            "3) When asked Have you created a dashboard?")]
        public string GetKnowledgeGraphResourceUsageDashboard()
        {
            return _plugin.GetKnowledgeGraphResourceUsageDashboard();
        }

        [Description("Returns basic metadata of an Azure resource. The input should be in Azure ResourceId format. Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp" +
            "Use this tool when you want to get following properties of an azure resource:" +
            "- subscription id" +
            "- resource group" +
            "- resource type" +
            "- resource name" +
            "- location (or region)" +
            "\nNote: For resources with parent-child relationships like App Service and App Service Plan, or Container Apps and Container App Environment, basic properties only include the core metadata.")]
        public async Task<Dictionary<string, object>> GetResourceBasicProperties(
             [Description("Azure Resource Id of the resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId)
        {
            return await _plugin.GetResourceBasicProperties(resourceId);
        }

        [Description("Returns resource-specific properties along with basic metadata for an Azure resource identified by its ResourceId. " +
            "Input must be in Azure ResourceId format (e.g., /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp). " +
            "\nResource-specific properties include:" +
            "\n- For App Service/Web/Function Apps: hosting plan, VNET, TLS, workers, auto-heal, health checks, runtime stack, App Insights. " +
            "\n- For App Service Plans: workers, status, zone redundancy, region, kind. " +
            "\n- For Container Apps: state, profile, access, containers, scaling. " +
            "Note: Some properties may be in associated resources (e.g., App Service Plan) and need separate queries.This function will return all properties directly attached to the requested resource.")]
        public async Task<Dictionary<string, object>> GetResourceDetailedProperties(
             [Description("Azure Resource Id of the resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId)
        {
            return await _plugin.GetResourceDetailedProperties(resourceId);
        }
    }
}
