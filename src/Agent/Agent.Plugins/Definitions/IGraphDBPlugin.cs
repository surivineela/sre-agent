// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Graph.Schema;
using Gremlin.Net.Driver;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Plugins
{
    /// <summary>
    /// Interface for interacting with the graph database to query and visualize Azure resources and their relationships.
    /// Provides methods for exploring resource connectivity, application topology, and infrastructure visualization.
    /// </summary>
    public interface IGraphDBPlugin
    {
        /// <summary>
        /// The current thread context for the plugin, used to identify the conversation thread when sending messages or images.
        /// </summary>
        Guid? ThreadId { get; set; }

        /// <summary>
        /// Runs a generic query against the graph database. Enables custom Gremlin queries for advanced exploration.
        /// Only performs read operations for security purposes.
        /// </summary>
        /// <param name="query">The Gremlin query to execute against the graph database.</param>
        /// <returns>A ResultSet containing the dynamic query results.</returns>
        Task<ResultSet<dynamic>> Query(string query);

        /// <summary>
        /// Finds all resources that a particular Azure Container App connects to through network connections,
        /// such as Redis caches, databases, and other services. Useful for networking connectivity debugging.
        /// </summary>
        /// <param name="resourceId">Azure Resource Id of the Container App, should begin with /subscriptions/...</param>
        /// <returns>A JSON string representing the network connections and their details.</returns>
        Task<string> FindAllNetworkConnectedResources(string resourceId = "");

        /// <summary>
        /// Returns a structured list of Azure resources that are connected to a specified resource.
        /// Provides an overview of application components in a programmatically accessible format.
        /// </summary>
        /// <param name="resourceId">Azure Resource Id of the application resource to analyze.</param>
        /// <param name="hops">Maximum number of relationship hops to traverse in the graph. Default is 3.</param>
        /// <returns>A list of Node objects representing the connected resources.</returns>
        Task<List<Node>> GetApplicationComponentsSummary(string resourceId, int hops = 3);

        /// <summary>
        /// Creates an interactive visual diagram showing how Azure resources are connected to each other.
        /// Renders a graph visualization as a base64 encoded PNG image for display in the chat.
        /// </summary>
        /// <param name="resourceId">Azure Resource Id of the application resource to visualize.</param>
        /// <param name="hops">Maximum number of relationship hops to include in the visualization. Default is 3.</param>
        /// <param name="threadId">Optional threadId for the current conversation. If null, will use Context.ThreadId.</param>
        /// <returns>A confirmation message indicating the visualization was rendered.</returns>
        Task<string> VisualizeApplicationComponents(string resourceId, int hops = 3, Guid? threadId = null);

        /// <summary>
        /// Analyzes an Azure subscription and identifies distinct applications based on connectivity patterns.
        /// Maps out complete application topologies including all connected resources and their relationships.
        /// </summary>
        /// <param name="subscriptionId">Azure Subscription Id to analyze.</param>
        /// <returns>A list of ApplicationGraph objects representing the discovered applications.</returns>
        Task<List<ApplicationGraph>> DiscoverApplications(string subscriptionId);

        /// <summary>
        /// Generates a visual representation of the topology of a microservice architecture deployed in Azure Kubernetes Service (AKS).
        /// Renders a graph visualization as a base64 encoded PNG image for display in the chat.
        /// </summary>
        /// <param name="AKSClusterResourceId">Azure Resource Id of the AKS cluster.</param>
        /// <param name="_namespace">The Kubernetes namespace to analyze.</param>
        /// <param name="deploymentName">The name of Kubernetes deployment to analyze.</param>
        /// <param name="threadId">Optional threadId for the current conversation. If null, will use Context.ThreadId.</param>
        /// <returns>A confirmation message indicating the visualization was rendered.</returns>
        Task<string> VisualizeAKSMicroserviceTopology(string AKSClusterResourceId, string _namespace, string deploymentName, Guid? threadId = null);

        /// <summary>
        /// Adds a relationship between a Container App node and its source code repository.
        /// Creates a new source code node if it doesn't exist and establishes a SERVES_CODE relationship.
        /// </summary>
        /// <param name="resourceId">Azure Resource Id of the Container App.</param>
        /// <param name="repoUrl">GitHub repository URL for the container app's source code.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddSourceCodeNodeToContainerAppNodeAsync(string resourceId, string repoUrl);

        /// <summary>
        /// Identifies Container Apps in the graph that don't have associated source code repositories.
        /// Useful for ensuring all deployed applications have proper source code tracking.
        /// </summary>
        /// <returns>A list of resource IDs for Container Apps missing source code links.</returns>
        Task<List<string>> GetContainerAppsWithNodesWithoutSourceCodeNodesAsync();

        /// <summary>
        /// Retrieves the dashboard screenshot for a resource and uses an LLM to summarize the general health.
        /// Analyzes metrics, trends, and anomalies to provide an overall health assessment.
        /// </summary>
        /// <param name="resourceName">The name of the Azure resource to analyze.</param>
        /// <param name="resourceType">The type of the Azure resource (e.g., microsoft.app/containerapps).</param>
        /// <returns>A text summary of the resource's health based on dashboard metrics.</returns>
        Task<string> GetGeneralHealthAsync(string resourceName, string resourceType);

        /// <summary>
        /// Searches for resources by a partial resource name and resource type.
        /// Helps locate specific resources in the Azure environment.
        /// </summary>
        /// <param name="resourceName">Partial or complete name of the resource to search for.</param>
        /// <param name="resourceType">The type of resource to search for (e.g., microsoft.app/containerapps).</param>
        /// <returns>A list of ArmResourceNode objects matching the search criteria.</returns>
        Task<List<ArmResourceNode>> SearchResourceAsync(string resourceName, string resourceType);

        /// <summary>
        /// Searches for resources by a partial resource name and resource type.
        /// Helps locate specific resources in the Azure environment.
        /// </summary>
        /// <param name="resourceName">Partial or complete name of the resource to search for.</param>
        /// <returns>A list of ArmResourceNode objects matching the search criteria.</returns>
        Task<List<ArmResourceNode>> SearchResourceByNameAsync(string resourceName);

        /// <summary>
        /// Returns a list of subscription IDs by querying all vertices that have a 'subscriptionId' property.
        /// Useful for discovery and inventory of available subscriptions.
        /// </summary>
        /// <returns>A list of subscription IDs found in the graph.</returns>
        Task<List<dynamic>> ListSubscriptionsAsync();

        /// <summary>
        /// Returns a list of resource groups for a given subscription ID.
        /// </summary>
        /// <param name="subscriptionId">Subscription Id that resource groups belong to.</param>
        /// <returns></returns>
        Task<List<Dictionary<string, object>>> ListResourceGroupsAsync(string subscriptionId);

        /// <summary>
        /// Retrieves and summarizes activity logs for a resource and its dependent resources.
        /// Analyzes recent operations, changes, and potential issues over a specified time period.
        /// </summary>
        /// <param name="resourceId">Azure Resource Id of the resource to analyze.</param>
        /// <param name="daysBack">Number of days of logs to retrieve and analyze. Default is 30.</param>
        /// <param name="threadId">Optional threadId for the current conversation.</param>
        /// <returns>A summary of the activity logs with key insights and potential issues.</returns>
        Task<string> FetchAndSummarizeActivityLogs(string resourceId, int daysBack = 30, Guid? threadId = null);

        /// <summary>
        /// Gets a count of Azure resources of a specified type, optionally grouped by a property.
        /// </summary>
        /// <param name="resourceType">Type of the Azure resource to count (e.g., 'microsoft.app/containerapps', 'microsoft.storage/storageaccounts')</param>
        /// <param name="groupBy">Optional. Property to group by for getting counts by specific attribute (e.g., 'location', 'resourceGroup'). Leave empty for total count.</param>
        /// <returns>
        /// When groupBy is empty: Returns an object with ResourceType and Count properties.
        /// When groupBy is specified: Returns an object with ResourceType, GroupBy, and a Counts dictionary.
        /// </returns>
        Task<dynamic> GetResourceCountAsync(string resourceType, string groupBy = "");

        /// <summary>
        /// Returns a list of resources of a specified type with their complete property bag.
        /// Each resource is represented as a dictionary of property names and values.
        /// The 'updateTs' property is excluded from the results.
        /// </summary>
        /// <param name="resourceType">The type of resource to query (e.g., 'microsoft.app/containerapps')</param>
        /// <returns>A list of dictionaries containing all properties for each resource of the specified type.</returns>
        Task<List<Dictionary<string, object>>> ListResourcesByTypeAsync(string resourceType, string propertyName = "", string propertyValue = "");

        /// <summary>
        /// Retrieves comprehensive information about all managed Azure resources.
        /// Returns count data for various resource types categorized into Azure and other resources,
        /// along with total resource counts.
        /// </summary>
        Task<dynamic> GetManagedResourcesInfoAsync();

        /// <summary>
        /// Retrieves the application health information for a specific resource from the graph database.
        /// This provides metrics like availability, CPU usage, memory usage, latency, and overall health state.
        /// </summary>
        /// <param name="resourceId">Azure Resource Id of the resource to get health information for.</param>
        /// <returns>A AppHealthInfo object containing health metrics, or null if the resource doesn't have health data.</returns>
        Task<string> GetApplicationHealthInfoAsync(string resourceId);

        string GetKnowledgeGraphResourceUsageDashboard();

        Task<Dictionary<string, object>> GetResourceBasicProperties(string resourceId);
        Task<Dictionary<string, object>> GetResourceDetailedProperties(string resourceId);
        Task UpdateRepoNodeWithLastScanTime(string repoUrl);

        Task<String> GetResourceIdForResourceName(string resourceName, string resourceType);
    }
}
