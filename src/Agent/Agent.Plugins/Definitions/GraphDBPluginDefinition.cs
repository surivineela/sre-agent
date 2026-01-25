// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Graph.Schema;
using Agent.Plugins.Interface;
using Gremlin.Net.Driver;

namespace Agent.Plugins
{
    [AgentToolPlugin(Category = ToolCategories.KnowledgeBase)]
    public class GraphDBPluginDefinition : ContextToolTarget<AgentContext>
    {
        public IGraphDBPlugin _plugin { get; }
        public GraphDBPluginDefinition(IGraphDBPlugin graphDBPlugin)
        {
            _plugin = graphDBPlugin;
        }

        /// <summary>
        /// When implementing this in prod, we need to give this agent a read-only user
        /// </summary>
        [Description("Run a generic query against the graph database. Do NOT perform any write operations.")]
        public async Task<ResultSet<dynamic>> Query(string query)
        {
            return await _plugin.Query(query);
        }

        [Description("Finds all resources that a particular Azure Container App connects to through network connections, such as Redis caches, databases, and other services. Useful for networking connectivity debug")]
        public async Task<string> FindAllNetworkConnectedResources(
            [Description("Azure Resource Id of the Container App, should begin with /subscriptions...., Example: /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo/providers/microsoft.app/containerapps/iot-dashboard")] string resourceId = "")
        {
            return await _plugin.FindAllNetworkConnectedResources(resourceId);
        }

        [Description("Returns a structured list of Azure resources connected to a specified resource. " +
            "This function is best used when you need to: 1) Get overview of what resources are part of an application, " +
            "2) See resource names, types, and IDs as a list, " +
            "3) Programmatically process the connected resources, or " +
            "4) Present a text-based summary" +
            " The output is a List<Node> where each Node contains id, name, type, essential properties. " +
            "Use this instead of VisualizeApplicationComponents when you don't need to show the relationships between resources.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<Node>> GetApplicationComponentsSummary(
            [Description("Azure Resource Id of the application resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Maximum number of relationship hops to traverse in the graph. Higher values (1-5) will discover more distant relationships but may include unrelated resources. Default is 3.")] int hops = 3)
        {
            return await _plugin.GetApplicationComponentsSummary(resourceId, hops);
        }

        [Description("Creates an interactive visual diagram showing how Azure resources are connected. " +
    "Use this to: 1) Show the topology/relationships between resources, " +
    "2) Help users understand the architecture visually, 3) Debug connectivity issues, or " +
    "4) Present a complete picture of the application's infrastructure. " +
    "The output includes nodes (resources) and edges (relationships). " +
    "Use instead of GetApplicationComponentsSummary when users ask to 'show', 'visualize', 'draw', or 'diagram' the connections. " +
    "Returns the graph as a base64-encoded string. Input: Azure Resource Id of the application resource to visualize." +
    "Examples of usage: 'Visualize <WebAppName> in my subscription.'" +
    "**Keywords: Visualize, Azure Resource, Topology.**")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> VisualizeApplicationComponents(
            [Description("Azure Resource Id of the application resource to visualize. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Maximum number of relationship hops to include in the visualization. Higher values (1-5) show more distant connections but may make the diagram more complex. Default is 3.")] int hops = 3)
        {
            return await _plugin.VisualizeApplicationComponents(resourceId, hops, Context?.ThreadId);
        }

        // TODO(jianbo): this not working for AKS, need to fix.
        [Description("Analyzes an Azure subscription and returns a List<ApplicationGraph>, where each ApplicationGraph represents a distinct application. " +
    "Each ApplicationGraph contains: id, name, entryPoint (main resource Node), nodes (List<Node> of related resources), and edges (List<Edge> showing relationships). " +
    "Entry points are identified from Container Apps, App Services. " +
    "The function maps out application topologies, including all connected resources and relationships. " +
    "Returns an empty list if no applications are found.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<ApplicationGraph>> DiscoverApplications(
            [Description("Azure Subscription Id to analyze. This is the GUID identifier for the subscription, found in the subscription's overview page or in the resource ID after '/subscriptions/'")] string subscriptionId)
        {
            return await _plugin.DiscoverApplications(subscriptionId);
        }

        [Description("Adds the GitHub repo url node and an edge from the container app node to it")]
        public async Task AddSourceCodeNodeToContainerAppNode(
            [Description("Azure Resource Id of the Container App, should begin with /subscriptions...., Example: /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo/providers/microsoft.app/containerapps/iot-dashboard")] string resourceId = "",
            [Description("GitHub repository url, should begin with https://github.com/..., Example: https://github.com/{ORG_NAME}/{REPO_NAME}")] string repoUrl = "")
        {
            await _plugin.AddSourceCodeNodeToContainerAppNodeAsync(resourceId: resourceId, repoUrl: repoUrl);
        }

        [Description("Adds a tag to a resource to prevent it from being flagged in a scan for a specified period of time.")]
        public async Task<string> AddIgnoreTagToResource(
            [Description("Azure Resource Id of the Container App, should begin with /subscriptions...., Example: /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo/providers/microsoft.app/containerapps/iot-dashboard")]
            string resourceId,
            [Description("The period of time for which to ignore scan results for this resource.")]
            TimeSpan ignoreTagDuration,
            [Description("The action being performed.")]
            string actionTaken
            )
        {
            return await _plugin.AddIgnoreInfoToResource(resourceId, ignoreTagDuration, actionTaken);
        }

        [Description("Gets a list of container apps with nodes in the graph that don't have edges connecting them to source code nodes")]
        public async Task<List<string>> GetContainerAppsWithNodesWithoutSourceCodeNodes()
        {
            return await _plugin.GetContainerAppsWithNodesWithoutSourceCodeNodesAsync();
        }

        [Description("Updates the source code node's lastScanTime property with the updated scan time.")]
        public async Task UpdateRepoNodeWithLastScanTime(string repoUrl)
        {
            await _plugin.UpdateRepoNodeWithLastScanTime(repoUrl);
        }

        [Description("Retrieves information about all managed resources by yourself in your Knowledge Graph. " +
            "This function is useful when you need to: 1) Get an inventory of all Azure resources, " +
            "2) Count resources by type for reporting or monitoring, " +
            "3) Understand the distribution of resources across different services, or " +
            "4) Get aggregate metrics on resource usage. " +
            "The output provides counts for different resource types and totals that can be used for dashboards or resource management.")]
        public async Task<dynamic> GetManagedResourcesInfoAsync()
        {
            return await _plugin.GetManagedResourcesInfoAsync();
        }

        [Description("Searches for Azure resources using flexible filters: name, types, subscription, and/or location. " +
            "At least one filter parameter must be provided. When multiple filters are provided, AND logic is applied. " +
            "Usage examples: " +
            "1) Find subscriptions: SearchResource(resourceTypes: ['microsoft.resources/subscriptions']) " +
            "2) Find resource groups: SearchResource(resourceTypes: ['microsoft.resources/subscriptions/resourcegroups'], subscriptionId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890') " +
            "3) Find by name: SearchResource(resourceName: 'my-app') " +
            "4) Find by type: SearchResource(resourceTypes: ['microsoft.web/sites']) " +
            "5) Find a CATEGORY spanning multiple types (e.g., 'databases'): SearchResource(resourceTypes: ['microsoft.sql/servers', 'microsoft.documentdb/databaseaccounts', 'microsoft.dbforpostgresql/servers']) - pass all types in ONE call " +
            "6) Filter by location: SearchResource(location: 'eastus', resourceTypes: ['microsoft.storage/storageaccounts']) " +
            "7) Combined filters: SearchResource(resourceName: 'api', resourceTypes: ['microsoft.web/sites'], subscriptionId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890', location: 'eastus') " +
            "Returns matching resources with essential fields: resourceId, resourceName, location (plus clusterResourceId and namespace for Kubernetes resources).")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<object>> SearchResource(
            [Description("Optional. Partial or complete name of the resource to search for. The search is case-insensitive and will match any resource containing this string.")] string? resourceName = null,
            [Description("Optional. List of Azure resource types to filter by. Always use full Azure resource type format (e.g., ['microsoft.resources/subscriptions'], ['microsoft.resources/subscriptions/resourcegroups'], ['microsoft.app/containerapps'], ['microsoft.web/sites']).")] List<string>? resourceTypes = null,
            [Description("Optional. Azure subscription ID to filter by. Only resources in this subscription will be returned. If not available, first call SearchResource(resourceTypes: ['microsoft.resources/subscriptions']) to get the list.")] string? subscriptionId = null,
            [Description("Optional. Azure region/location to filter by (e.g., 'eastus', 'westus2'). Only resources in this location will be returned.")] string? location = null,
            [Description("Optional. Maximum number of results to return. Default is 50.")] int limit = 50)
        {
            return await _plugin.SearchResourceAsync(resourceName, resourceTypes, subscriptionId, location, limit);
        }

        [Description("Gets the count of Azure resources of a specified type in the knowledge graph. " +
            "This function is useful when you need to: 1) Get an inventory of resources by type, " +
            "2) Validate quantity of deployed resources against expected counts, " +
            "3) Monitor resource proliferation over time, or " +
            "4) Get statistics about your Azure environment composition. " +
            "Returns a count of matching resources and can group by specific properties.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<dynamic> GetResourceCount(
            [Description("Not empty. Pass 'all' to query all resource types or pass a valid type of the Azure resource to count (e.g., 'microsoft.app/containerapps' for container apps, 'microsoft.web/sites' for webapps, function apps, 'microsoft.containerservice/managedclusters' for AKS)")] string resourceType,
            [Description("Optional. Property to group by for getting counts by specific attribute (currently only allowed 'location', 'resourceGroupName'). Leave empty for total count. It is ignored if 'resourceType' is 'all'.")] string groupBy = "")
        {
            return await _plugin.GetResourceCountAsync(resourceType, groupBy);
        }

        [Description("Returns a general dashboard provided as daily reports for Resource Counts recorded in the knowledge graph. " +
            "This function is useful when you need to: 1) Need to provide a URL to the daily dashboard " +
            "2) Provide a very generic dashboard for the knowledge graph overview at a very high level." +
            "3) When asked Have you created a dashboard?")]
        [AgentTool(ToolMode.Auto)]
        public string GetKnowledgeGraphResourceUsageDashboard()
        {
            return _plugin.GetKnowledgeGraphResourceUsageDashboard();
        }

        [Description("PREFERRED FUNCTION FOR AKS/KUBERNETES VISUALIZATIONS. Generates a detailed visual representation of microservice architectures deployed in Azure Kubernetes Service (AKS). " +
            "ALWAYS USE THIS FUNCTION INSTEAD OF VisualizeApplicationComponents when working with: AKS clusters, Kubernetes, K8s, microservices in Kubernetes, pods, deployments, or Kubernetes namespaces. " +
            "This specialized function provides Kubernetes-aware visualization showing relationships between deployments, pods, services and other Kubernetes resources. " +
            "This is the correct choice for any request to visualize, show, map or diagram: " +
            "1) Kubernetes application architecture, 2) Help users understand the architecture of microservice connections within AKS visually, 3) Debug and troubleshoot microservice issues, or " +
            "Returns a base64-encoded diagram specifically optimized for Kubernetes resource relationships.")]
        public async Task<string> VisualizeAKSMicroserviceTopology(
            [Description("Azure Resource Id of the AKS cluster, should begin with /subscriptions/..., Example: /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo/providers/microsoft.containerservice/managedclusters/iot-dashboard")] string AKSClusterResourceId,
            [Description($"Kubernetes namespace, e.g. 'default', 'kube-system'")] string _namespace,
            [Description($"Name of the Kubernetes deployment, e.g. 'nginx', 'backend'")] string deploymentName)
        {
            return await _plugin.VisualizeAKSMicroserviceTopology(AKSClusterResourceId, _namespace, deploymentName, Context?.ThreadId);
        }

        // TODO(jianbosun): Add prompt to get resource details for AKS resources (combine resourceID with AKS GVK) and register this func to AKS plugin
        [Description("Returns resource-specific configuration properties along with basic metadata for an Azure resource identified by its ResourceId. " +
            "Input must be in Azure ResourceId format (e.g., /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp). " +
            "\nResource-specific properties include:" +
            "\n- For App Service/Web/Function Apps: hosting plan, VNET, TLS, workers, auto-heal, runtime stack, App Insights. " +
            "\n- For App Service Plans: workers, status, zone redundancy, region, kind. " +
            "\n- For Container Apps: state, profile, access, containers, scaling. " +
            "\n- For API Management Services: status, Gateway & Management API URI, capacity. " +
            "Note: Some properties may be in associated resources (e.g., App Service Plan) and need separate queries (example zone redundancy, sku etc). This function returns configuration properties directly attached to the requested resource.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<Dictionary<string, object>> GetResourceDetailedProperties(
             [Description("Azure Resource Id of the resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId)
        {
            return await _plugin.GetResourceDetailedProperties(resourceId);
        }
    }
}
