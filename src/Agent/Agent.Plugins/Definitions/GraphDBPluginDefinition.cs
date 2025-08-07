// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Graph.Schema;
using Agent.Plugins.Interface;
using Gremlin.Net.Driver;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    [AgentToolPlugin(Category = ToolCategories.KnowledgeBase)]
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
        [AgentTool(ToolMode.Auto)]
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
    "Returns the graph as a base64-encoded string. Input: Azure Resource Id of the application resource to visualize." +
    "Examples of usage: 'Visualize <WebAppName> in my subscription.'" +
    "**Keywords: Visualize, Azure Resource, Topology.**")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> VisualizeApplicationComponents(
            [Description("Azure Resource Id of the application resource to visualize. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Maximum number of relationship hops to include in the visualization. Higher values (1-5) show more distant connections but may make the diagram more complex. Default is 3.")] int hops = 3)
        {
            return await _plugin.VisualizeApplicationComponents(resourceId, hops);
        }

        // TODO(jianbo): this not working for AKS, need to fix.
        [KernelFunction("DiscoverApplications")]
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

        [KernelFunction("AddSourceCodeNodeToContainerAppNode")]
        [Description("Adds the GitHub repo url node and an edge from the container app node to it")]
        public async Task AddSourceCodeNodeToContainerAppNode(
            [Description("Azure Resource Id of the Container App, should begin with /subscriptions...., Example: /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo/providers/microsoft.app/containerapps/iot-dashboard")] string resourceId = "",
            [Description("GitHub repository url, should begin with https://github.com/..., Example: https://github.com/{ORG_NAME}/{REPO_NAME}")] string repoUrl = "")
        {
            await _plugin.AddSourceCodeNodeToContainerAppNodeAsync(resourceId: resourceId, repoUrl: repoUrl);
        }

        [KernelFunction("AddIgnoreTagToResource")]
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
            "The resources themselves also have a health score cord, use this method for verbose analysis." +
            "The output is a text summary that describes the resource's health status, important metrics, and any anomalies or concerns.")]
        public async Task<string> GetGeneralHealth(
            [Description("Name of the Azure resource to analyze. This should be the exact resource name as shown in the Azure portal.")] string resourceName,
            [Description("Type of the Azure resource (e.g., 'microsoft.app/containerapps', 'microsoft.storage/storageaccounts', 'microsoft.documentdb/databaseaccounts', 'microsoft.cache/redis')")] string resourceType)
        {
            return await _plugin.GetGeneralHealthAsync(resourceName, resourceType);
        }

        [KernelFunction("GetManagedResourcesInfo")]
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

        [KernelFunction("SearchResource")]
        [Description("Searches for Azure resources by name pattern and resource type in the knowledge graph. " +
            "This function is useful when you need to: 1) Find specific resources without knowing the exact resource ID, " +
            "2) Locate resources of a particular type across your Azure environment, " +
            "3) Find resources matching a naming pattern, or " +
            "4) Verify if resources exist before performing operations on them. " +
            "Returns a list of matching resources with their details.")]
        [AgentTool(ToolMode.Auto)]
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
        [AgentTool(ToolMode.Auto)]
        public async Task<string> SearchResourceByName(
        [Description("Partial or complete name of the resource to search for. The search is case-insensitive and will match any resource containing this string.")] string resourceName)
        {
            var result = await _plugin.SearchResourceByNameAsync(resourceName);

            // Check if the result is an array/list
            if (result is System.Collections.IEnumerable enumerable && !(result is string))
            {
                var items = enumerable.Cast<object>().ToList();
                var jsonString = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                return $"{jsonString}\n\nTotal number of matching resources found: {items.Count}";
            }

            // If it's not a list, serialize as-is
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
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

        [KernelFunction("ListSubscriptions")]
        [Description("Returns a list of all Azure subscription IDs present in the knowledge graph. " +
            "This function is useful when you need to: 1) Discover available subscriptions, " +
            "2) Verify subscription visibility to the agent, " +
            "3) Get subscription IDs for use with other commands, or " +
            "4) Perform an inventory of monitored subscriptions. " +
            "The output is a list of subscription IDs without additional details.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<dynamic>> ListSubscriptions()
        {
            return await _plugin.ListSubscriptionsAsync();
        }

        [KernelFunction("ListResourceGroups")]
        [Description("Returns a list of all Azure resource groups present in the knowledge graph. " +
            "This function is useful when you need to: 1) Discover available resource groups, " +
            "2) Verify resource group visibility to the agent, " +
            "3) Get resource group names for use with other commands, or " +
            "4) Perform an inventory of monitored resource groups. " +
            "The output is a list of resource group names without additional details.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<Dictionary<string, object>>> ListResourceGroups(
             [Description("The subscription ID for which to list resource groups. This should be the GUID identifier from the subscription.")] string subscriptionId)
        {
            return await _plugin.ListResourceGroupsAsync(subscriptionId);
        }

        [KernelFunction("GetActivityLogsSummary")]
        [Description("Retrieves and analyzes Azure Activity Logs for a resource and its connected components. " +
            "This function is valuable when you need to: 1) Review recent changes made to a resource and its dependencies, " +
            "2) Investigate who made specific configuration changes, " +
            "3) Understand patterns of administrative activity, or " +
            "4) Detect potentially unauthorized or unusual operations. " +
            "The output is a natural language summary highlighting key activities, patterns, and potential concerns.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetActivityLogsSummary(
            [Description("Azure Resource Id of the resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Number of hours of activity logs to retrieve and analyze. Default is 24 hours.")] int hoursBack = 24)
        {
            return await _plugin.FetchAndSummarizeActivityLogs(resourceId, hoursBack);
        }

        [KernelFunction("ListResourcesByType")]
        [Description("Returns a list of Azure resources OR Kubernetes-native resources of a specified type/kind, with their property details as recorded in the knowledge graph. Supports filtering by properties like AKS cluster ID (for Kubernetes), or resource group (for ARM). " +
            "This function is useful when you need to: " +
            "1) Get an inventory of resources of a specific type and any additional filter on property, " +
            "2) Examine tracked configuration properties of resources, " +
            "3) Gather metadata for resources across your Azure environment, or " +
            "The output is a list of resource objects with all their properties. " +
            "Each resource includes details like name, location, resource group, and type-specific configuration." +
            "Use pagination with 'skip' and 'take'. If user asked for listing all resources, set 'take' to a negative number like -1 to return all resources without pagination." +
            "If the total number of matching resources is large, only the first 50 will be returned." +
            "The agent should inform the user that the list is partial if more resources exist, and offer to retrieve more if needed.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<Dictionary<string, object>>> ListResourcesByType(
            [Description(
                "The type or kind of resource to list. " +
                "For Azure ARM resources, use the full ARM type (e.g., 'microsoft.compute/virtualmachines', 'microsoft.containerservice/managedclusters'). " +
                "For Kubernetes resources, use the common K8s kind (e.g., 'Deployment', 'Service', 'Node', 'Namespace', 'StatefulSet', 'ConfigMap', 'Secret', 'PersistentVolumeClaim'). The system will map K8s kinds to their graph representation." +
                "To list all resources, use 'all' as the resource type."
            )] string resourceType,
             [Description(
                "Optional. For Kubernetes resources, this is the property name to filter on (e.g., 'clusterResourceId'). " +
                "For Azure ARM resources, this can be a property name to filter on (e.g., 'resourceGroupName'). "
            )] string propertyName = "",
            [Description(
                "Optional. For Kubernetes resources, this should be Azure Resource Id of the AKS cluster, should begin with /subscriptions/..., Example: /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo/providers/microsoft.containerservice/managedclusters/iot-dashboard" +
                "For Azure ARM resources, this is the value of the property to filter on."
            )] string propertyValue = "",
            [Description("Number of results to skip for pagination. Default is 0.")] int skip = 0,
            [Description("Number of results to return. Use 0 or negative to return all. Default is 50.")] int take = 50)
        {
            return await _plugin.ListResourcesByTypeAsync(resourceType, propertyName, propertyValue, skip, take);
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

        [KernelFunction("VisualizeAKSMicroserviceTopology")]
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
            return await _plugin.VisualizeAKSMicroserviceTopology(AKSClusterResourceId, _namespace, deploymentName);
        }

        [Description("Returns basic metadata of an Azure resource. The input should be in Azure ResourceId format. Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp" +
            "Use this tool when you want to get following properties of an azure resource:" +
            "- subscription id" +
            "- resource group" +
            "- resource type" +
            "- resource name" +
            "- location (or region)" +
            "\nNote: For resources with parent-child relationships like App Service and App Service Plan, or Container Apps and Container App Environment, basic properties only include the core metadata.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<Dictionary<string, object>> GetResourceBasicProperties(
             [Description("Azure Resource Id of the resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId)
        {
            return await _plugin.GetResourceBasicProperties(resourceId);
        }

        // TODO(jianbosun): Add prompt to get resource details for AKS resources (combine resourceID with AKS GVK) and register this func to AKS plugin
        [Description("Returns resource-specific properties along with basic metadata for an Azure resource identified by its ResourceId. " +
            "Input must be in Azure ResourceId format (e.g., /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp). " +
            "\nResource-specific properties include:" +
            "\n- For App Service/Web/Function Apps: hosting plan, VNET, TLS, workers, auto-heal, health checks, runtime stack, App Insights. " +
            "\n- For App Service Plans: workers, status, zone redundancy, region, kind. " +
            "\n- For Container Apps: state, profile, access, containers, scaling. " +
            "\n- For API Management Services: status, Gateway & Management API URI, capacity. " +
            "Note: Some properties may be in associated resources (e.g., App Service Plan) and need separate queries (example zone redundancy, sku etc).This function will return all properties directly attached to the requested resource. Also retuns Health Scorecard for the resource if available")]
        [AgentTool(ToolMode.Auto)]
        public async Task<Dictionary<string, object>> GetResourceDetailedProperties(
             [Description("Azure Resource Id of the resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId)
        {
            return await _plugin.GetResourceDetailedProperties(resourceId);
        }

        [Description("Returns the resource ID of an Azure resource. The input should be the name of the resource format and it's corresponding resource type. Example: (mywebapp, microsoft.web/sites), (myakscluster, microsoft.containerservice/managedclusters)" +
            "Use this tool when you want to get the resource ID of an Azure resource.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetResourceIdForResourceName(
             [Description("Name and type of the resource to fetch the resource ID for. Example: mywebapp")] string resourceName,
             [Description("Azure Resource Type for this resource.")] string resourceType)
        {
            return await _plugin.GetResourceIdForResourceName(resourceName, resourceType);
        }

        [KernelFunction("GetResourceHealthInfo")]
        [Description("Retrieves detailed health metrics for a specific Azure resource from the graph database. " +
            "This function is useful when you need to: " +
            "1) Check the current health state of a resource, " +
            "2) Get performance metrics like CPU, memory usage and availability, " +
            "3) Verify if a resource is active or potentially idle, or " +
            "4) Get insight into the resource's performance characteristics. " +
            "The output provides health state, availability, transaction count, latency, CPU and memory usage when available.")]
        public async Task<string> GetResourceHealthInfo(
    [Description("Azure Resource Id of the resource to get health information for. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId)
        {
            return await _plugin.GetApplicationHealthInfoAsync(resourceId);
        }
    }
}
