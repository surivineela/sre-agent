// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Framework;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Schema;
using Agent.Plugins.Interface;
using Azure.Core;
using Gremlin.Net.Driver;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Implementation;

public class GraphDBPlugin : IGraphDBPlugin
{
    /// <summary>
    /// Azure resource types that require querying the ARG resourcecontainers table instead of Resources.
    /// </summary>
    private static readonly HashSet<string> ArgResourceContainerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "microsoft.resources/subscriptions",
        "microsoft.resources/subscriptions/resourcegroups"
    };

    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
    private readonly ILogger<GraphDBPlugin> _logger;
    private readonly DashboardSettings _dashboardSettings;
    private readonly IAuthenticationService _authService;
    private readonly List<string> _crawlRoots;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly AzureResourceGraphClient _azureResourceGraphClient;

    public Guid? ThreadId { get; set; }

    public GraphDBPlugin(
        IGraphDatabaseClient graphDbClient,
        IChatClientProvider chatClientProvider,
        DashboardSettings dashboardSettings,
        IAgentOutboundCommunicationService agentOutboundCommunicationService,
        ILogger<GraphDBPlugin> logger,
        IAuthenticationService authService,
        CrawlerSettings crawlerSettings,
        IHostEnvironment hostEnvironment,
        AzureResourceGraphClient azureResourceGraphClient)
    {
        _graphDbClient = graphDbClient;
        _chatClientProvider = chatClientProvider;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
        _logger = logger;
        _dashboardSettings = dashboardSettings;
        _authService = authService;
        _crawlRoots = [.. crawlerSettings.CrawlRoots.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(root => root.Trim())];

        _hostEnvironment = hostEnvironment;
        _azureResourceGraphClient = azureResourceGraphClient;
    }

    /// <summary>
    /// Executes a generic read-only query against the graph database.
    /// </summary>
    [KernelFunction("query")]
    [Description("Run a generic query against the graph database. Do NOT perform any write operations.")]
    public async Task<ResultSet<dynamic>> Query(string query)
    {
        return await _graphDbClient.Query(query);
    }

    /// <summary>
    /// Finds all network-connected resources for a given resource or all container apps.
    /// </summary>
    public async Task<string> FindAllNetworkConnectedResources(string resourceId = "")
    {
        try
        {
            var vertexFilter = string.IsNullOrEmpty(resourceId)
                ? "hasLabel('microsoft.app/containerapps')"
                : $"hasId('{resourceId.ToLower().Replace("/", "_")}')";

            var query = $@"
    g.V().{vertexFilter}.has('isDeleted', false)
      .outE('USES_REDIS')
      .project('from', 'to', 'label', 'connection_details', 'properties')
      .by(outV().values('resourceId'))
      .by(inV().values('resourceId'))
      .by(label())
      .by(
        __.project('protocol', 'port', 'description', 'auth_mechanism')
        .by(constant('SSL/TLS'))
        .by(constant(6380))
        .by(constant('The container app connects to Redis cache over port 6380 (SSL/TLS encrypted) using an environment variable REDIS_HOST'))
        .by(constant('Access key authentication'))
      )
      .by(valueMap())
";

            var results = await _graphDbClient.Query(query);
            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error finding network connected resources");
            return $"Error finding network connected resources: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets a summary of application components connected to the specified resource.
    /// </summary>
    public async Task<List<Node>> GetApplicationComponentsSummary(string resourceId, int hops = 3)
    {
        _logger.LogInternalInformation($"[GetApplicationComponentsSummary] Invoked with resourceId: {resourceId}");

        var result = await GetApplicationComponentsRaw(resourceId, hops);
        return ConvertResultToNodes(result);
    }

    /// <summary>
    /// Generates a Mermaid diagram visualizing the microservice topology for an AKS deployment.
    /// </summary>
    public async Task<string> VisualizeAKSMicroserviceTopology(
        string AKSClusterResourceId,
        string _namespace,
        string deploymentName,
        Guid? threadId = null)
    {
        _logger.LogInternalInformation($"[VisualizeAKSMicroserviceTopology] Invoked with resourceId: {AKSClusterResourceId}");

        // Validation that resourceId is not null or empty
        if (string.IsNullOrWhiteSpace(AKSClusterResourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(AKSClusterResourceId));
        }
        if (string.IsNullOrWhiteSpace(_namespace))
        {
            throw new ArgumentException("Namespace cannot be null or empty.", nameof(_namespace));
        }
        if (string.IsNullOrWhiteSpace(deploymentName))
        {
            throw new ArgumentException("Deployment name cannot be null or empty.", nameof(deploymentName));
        }

        try
        {
            // ResourceIdentifier will parse and validate the resource ID format
            var resourceIdentifier = new ResourceIdentifier(AKSClusterResourceId);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Invalid Azure resource ID format: {ex.Message}",
                nameof(AKSClusterResourceId));
        }

        // Ensure threadId is not null
        if (threadId == null)
        {
            if (ThreadId != null)
            {
                threadId = ThreadId;
            }
            else
            {
                _logger.LogInternalWarning("[VisualizeAKSMicroserviceTopology] ThreadId is null. Cannot append diagram to message.");
                return "Error: ThreadId is null. Cannot generate visualization without a valid thread ID.";
            }
        }

        var maxRetries = 3;
        var retryDelayMilliseconds = 1000; // 1 second

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await GetAKSMicroserviceTopologyRaw(AKSClusterResourceId, _namespace, deploymentName);
                if (result.Count == 0)
                {
                    _logger.LogInternalWarning($"No components found to visualize, cluster {AKSClusterResourceId}, namespace {_namespace}, deployment {deploymentName}");
                    return "Error: No components found to visualize";
                }

                // First, deserialize the result to a more strongly-typed structure we can work with
                var tempJson = JsonSerializer.Serialize(result);

                var typedResult = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(tempJson)
              ?? [];

                // Filter out pods, services, and nodes from each item
                foreach (var item in typedResult)
                {
                    if (item.TryGetValue("objects", out var objectsValue) && objectsValue is IEnumerable<object> objectsEnumerable)
                    {
                        var objects = objectsEnumerable
                            .OfType<Dictionary<string, object>>()
                            .ToList();

                        var filteredObjects = objects.Where(obj =>
                        {
                            if (obj.TryGetValue("resourceType", out var resourceTypeValue) &&
                                resourceTypeValue is IEnumerable<object> resourceTypeList)
                            {
                                var resourceTypes = resourceTypeList
                                    .OfType<string>()
                                    .ToList();

                                if (resourceTypes.Count > 0)
                                {
                                    var resourceType = resourceTypes[0];
                                    return !resourceType.EndsWith("/services") &&
                                           !resourceType.EndsWith("/pods") &&
                                           !resourceType.EndsWith("/nodes");
                                }
                            }

                            return true; // Keep objects without resourceType
                        }).ToList();

                        item["objects"] = filteredObjects;
                    }
                }

                // --- Deduplication Step (AFTER Filtering) to reduce the input size for the LLM ---
                var uniqueFilteredResults = new List<Dictionary<string, object>>();
                var seenJsonRepresentations = new HashSet<string>();
                var jsonSerializerOptionsForDeduplication = new JsonSerializerOptions { };

                _logger.LogDebug($"[VisualizeAKSMicroserviceTopology] Starting deduplication for {typedResult.Count} filtered items.");
                foreach (var filteredItem in typedResult) // Iterate through the *filtered* list
                {
                    try
                    {
                        // Serialize the filtered object to its JSON string representation
                        var itemJson = JsonSerializer.Serialize(filteredItem, jsonSerializerOptionsForDeduplication);

                        // HashSet.Add returns true if the item was added (i.e., it was unique *after filtering*)
                        if (seenJsonRepresentations.Add(itemJson))
                        {
                            uniqueFilteredResults.Add(filteredItem); // Add the filtered object
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning($"[VisualizeAKSMicroserviceTopology] Could not serialize filtered item for deduplication. Skipping item. Error: {ex.Message}");
                        // Decide if you want to add the item anyway or skip it if serialization fails
                        // uniqueFilteredResults.Add(filteredItem); // Optional: Add even if serialization fails
                    }
                }
                _logger.LogDebug($"[VisualizeAKSMicroserviceTopology] Deduplication complete. Filtered items: {typedResult.Count}, Unique items after filtering: {uniqueFilteredResults.Count}");
                // --- End Deduplication Step ---

                var jsonResult = JsonSerializer.Serialize(uniqueFilteredResults, new JsonSerializerOptions { WriteIndented = true });
                var prompt = $"""
                    Using the provided data of Kubernetes deployments/statefulsets, create a Mermaid diagram that shows the relationships between microservices. Each JSON object in the data represents services that work together, so draw connections between them in the mermaid diagram.
Strict Requirements:
* Please ensure that each unique dependency is listed only once.
* Use deployment/statefulset name as the node identifier, mark the type behind the name if the type is not deployment by using this syntax: **name["name (type)"]**.
* Output ONLY the **VALID**, **RAW** Mermaid specification as plain text starting with 'graph LR;'. Do not include any markdown formatting, code fences, or additional text.
* Use '-->' to represent the dependency between two components.

Input JSON:
{jsonResult}
""";
                var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(prompt, new ChatOptions { Temperature = 0.2f });
                var mermaidSpec = response.Text;
                _logger.LogInternalInformation($"Generated Mermaid specification successfully: {mermaidSpec}");

                var mermaidMessage = $"```mermaid\n{mermaidSpec}\n```";

                var messageId = Guid.NewGuid();

                // Save to database via the outbound service
                await _agentOutboundCommunicationService.AppendAgentImageMessage(threadId.Value, mermaidMessage, messageId);

                // Stream the mermaid data directly to bypass tool call limitations
                await _agentOutboundCommunicationService.AppendAgentStreamMessage(threadId.Value, mermaidMessage, StreamMessageType.Mermaid, messageId);

                // Construct the final response string for the LLM, this helps the LLM to further answer questions regarding the topology
                var llmResponse = $@"I have analyzed the microservice topology starting from '{deploymentName}' in the '{_namespace}' namespace within the cluster '{AKSClusterResourceId}'.

A visual diagram representing these relationships has been generated and added to our chat.

For reference, here is the raw Mermaid specification used to create the diagram:
```mermaid
{mermaidSpec}
```";

                _logger.LogInternalInformation("[VisualizeAKSMicroserviceTopology] Successfully generated visualization and response text.");
                return llmResponse;
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogInternalWarning($"[VisualizeAKSMicroserviceTopology] Attempt {attempt} failed with error: {ex.Message}. Retrying in {retryDelayMilliseconds}ms...");
                    await Task.Delay(retryDelayMilliseconds);
                }
                else
                {
                    _logger.LogInternalError($"[VisualizeAKSMicroserviceTopology] All {maxRetries} attempts to generate the diagram failed. Last error: {ex.Message}");
                    throw;
                }
            }
        }

        return "Error: Unexpected execution path in visualization process";

    }

    /// <summary>
    /// Generates a Mermaid diagram visualizing application components and their relationships.
    /// </summary>
    public async Task<string> VisualizeApplicationComponents(
        string resourceId,
        int hops = 3,
        Guid? threadId = null)
    {
        _logger.LogInternalInformation($"[VisualizeApplicationComponents] Invoked with resourceId: {resourceId}");

        // Validation
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
        }

        try
        {
            // ResourceIdentifier will parse and validate the resource ID format
            var resourceIdentifier = new ResourceIdentifier(resourceId);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Invalid Azure resource ID format: {ex.Message}",
                nameof(resourceId));
        }

        // Ensure threadId is not null
        if (threadId == null || threadId.Equals(Guid.Empty))
        {
            if (ThreadId != null)
            {
                threadId = ThreadId;
            }
            else
            {
                _logger.LogInternalWarning("[VisualizeApplicationComponents] ThreadId is null. Cannot append diagram to message.");
                return "Error: ThreadId is null. Cannot generate visualization without a valid thread ID.";
            }
        }

        var maxRetries = 3;
        var retryDelayMilliseconds = 1000; // 1 second

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await GetApplicationComponentsRaw(resourceId, hops);
                if (result.Count == 0)
                {
                    _logger.LogInternalInformation($"No components found for resourceId: {resourceId}");
                    throw new Exception($"No components found for resourceId: {resourceId}. Was the correct resource ID provided? Alternatively, the Knowledge Graph may not have been built for this component.");
                }
                var jsonResult = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                var prompt = @$"
You are a graph visualization expert. Convert the following Azure Gremlin Query Results JSON data representing Azure resources and their relationships into a Mermaid graph specification.
Input JSON:
{jsonResult}
Requirements:
1. Generate ONLY the Mermaid specification, no explanations or other text. This is very critical to graph generation later.
2. Use 'graph LR' (left-right) direction
3. Each node should be labeled with its resource name and type
4. Use resource IDs as node identifiers
5. Include all relationships between nodes
6. Do not include any explanatory text or markdown, just the raw Mermaid specification
Example format (DO NOT USE THE SAME EXAMPLE, just for reference):
graph LR
    A[Resource1] --> B[Resource2]
    B --> C[Resource3]
Output ONLY the raw Mermaid specification as plain text starting with 'graph LR'. Do not include any markdown formatting, code fences, or additional text.";
                var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(prompt);
                var mermaidSpec = response.Text;
                _logger.LogInternalInformation("Generated Mermaid specification successfully");

                var mermaidMessage = $"```mermaid\n{mermaidSpec}\n```";

                var messageId = Guid.NewGuid();

                // Save to database via the outbound service
                await _agentOutboundCommunicationService.AppendAgentImageMessage(threadId.Value, mermaidMessage, messageId);

                // Stream the mermaid data directly to bypass tool call limitations
                await _agentOutboundCommunicationService.AppendAgentStreamMessage(threadId.Value, mermaidMessage, StreamMessageType.Mermaid, messageId);

                return "Visualization Ready! Your frontend will render the Mermaid diagram.";
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogInternalWarning($"[VisualizeApplicationComponents] Attempt {attempt} failed with error: {ex.Message}. Retrying in {retryDelayMilliseconds}ms...");
                    await Task.Delay(retryDelayMilliseconds);
                }
                else
                {
                    _logger.LogInternalError($"[VisualizeApplicationComponents] All {maxRetries} attempts to generate the diagram failed. Last error: {ex.Message}");
                    throw;
                }
            }
        }

        return "Error: Unexpected execution path in visualization process";
    }

    private async Task<ResultSet<dynamic>> GetAKSMicroserviceTopologyRaw(string resourceId, string namespaceName, string deploymentName)
    {
        try
        {
            var formattedResourceId = resourceId.ToLower().Replace("/", "_");
            var deploymentResourceId = $"{formattedResourceId}_apps_v1_namespaces_{namespaceName}_deployments_{deploymentName}";

            var query = $@"
g.V().has('id', '{deploymentResourceId}').has('isDeleted', false)
.repeat(out('LINKED', 'CONNECTED', 'OWNED_BY', 'HOSTED_ON', 'SQL_CONNECTED', 'POSTGRESQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS', 'BACKED_BY'))
.emit().dedup()
.path().by(valueMap('resourceName', 'resourceType'))";

            _logger.LogInternalInformation($"Executing AKS microservice topology query for resource: {resourceId}, namespace: {namespaceName}, deployment: {deploymentName ?? "all"}");
            return await _graphDbClient.Query(query);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error executing AKS microservice topology Gremlin query");
            throw;
        }
    }

    private async Task<ResultSet<dynamic>> GetApplicationComponentsRaw(string resourceId, int hops = 3)
    {
        _logger.LogInternalInformation($"[GetApplicationComponentsRaw] Invoked with resourceId: {resourceId}");

        try
        {
            var query = $@"g.V().has('id', '{resourceId.ToLower().Replace("/", "_")}').has('isDeleted', false)
                    .union(
                        identity(),
                        repeat(
                            union(
                                outE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'POSTGRESQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS', 'USES', 'USES_ACTION', 'USES_TRIGGER', 'USES_TRIGGER_ACTION').inV().has('isDeleted', false),
                                inE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'POSTGRESQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS', 'USES', 'USES_ACTION', 'USES_TRIGGER', 'USES_TRIGGER_ACTION').outV().has('isDeleted', false)
                            )
                            .not(has('resourceType', within('resourcegroup', 'subscription')))
                            .simplePath()
                        )
                        .times({hops})
                        .emit()
                    )
                    .dedup()
                    .project('id', 'name', 'type', 'properties')
                    .by(id())
                    .by(coalesce(values('resourceName'), constant('')))
                    .by(label())
                    .by(valueMap())";

            var result = await Query(query);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error finding application components");
            throw;
        }
    }

    /// <summary>
    /// Discovers all applications (Container Apps, App Services, AKS clusters) in a subscription.
    /// </summary>
    public async Task<List<ApplicationGraph>> DiscoverApplications(string subscriptionId)
    {
        _logger.LogInternalInformation($"[DiscoverApplications] Invoked with subscription {subscriptionId}");

        try
        {
            var entryPointQuery = BuildDiscoverApplicationsQuery(subscriptionId);
            var entryPointResult = await Query(entryPointQuery);
            var entryPoints = ConvertResultToNodes(entryPointResult);

            var applications = new List<ApplicationGraph>();

            foreach (var entryPoint in entryPoints)
            {
                _logger.LogDebug($"Processing application entry point: {entryPoint.Name} ({entryPoint.Type})");

                var resourceId = ((IEnumerable<object>)entryPoint.Properties["resourceId"]).First().ToString();
                var components = await GetApplicationComponentsSummary(resourceId ?? string.Empty, 3);

                if (components.Count == 0)
                {
                    _logger.LogInternalWarning($"No components found for application: {entryPoint.Name}");
                    continue;
                }

                var application = new ApplicationGraph
                {
                    Id = entryPoint.Id,
                    Name = entryPoint.Name,
                    EntryPoint = new SimpleNode(entryPoint),
                    Nodes = [.. components.Select(c => new SimpleNode(c))]
                };

                applications.Add(application);
            }

            return applications;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error discovering applications");
            return [];
        }
    }

    private string BuildDiscoverApplicationsQuery(string subscriptionId)
    {
        return $@"g.V().has('subscriptionId', '{subscriptionId.ToLower()}').has('isDeleted', false)
                .out('{Constants.Relationships.Contains}')
                .out('{Constants.Relationships.Contains}')
                .hasLabel(within(
                    '{Constants.ContainerAppType.ToLower()}',
                    '{Constants.AppServiceType.ToLower()}',
                    '{Constants.AzureKubernetesServiceType.ToLower()}'
                ))
                .has('isDeleted', false)
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";
    }

    private List<Node> ConvertResultToNodes(ResultSet<dynamic> result)
    {
        var nodes = new List<Node>();

        foreach (var item in result)
        {
            var properties = new Dictionary<string, object>();
            foreach (var prop in item["properties"])
            {
                properties[prop.Key] = prop.Value;
            }

            var node = new Node(
                item["id"],
                item["name"],
                item["type"],
                properties
            );

            nodes.Add(node);
        }

        return nodes;
    }

    /// <summary>
    /// Adds an ignore/suppress configuration to a resource for a specified duration.
    /// </summary>
    public async Task<string> AddIgnoreInfoToResource(string resourceId, TimeSpan ignoreTagDuration, string actionTaken)
    {
        try
        {
            var resourceNodeId = CrawlerExtensions.GetSanitizedCosmosDBId(resourceId);
            var query = $@"g.V().hasId('{resourceNodeId}').has('isDeleted', false)";
            var resourceNodeResults = await _graphDbClient.Query(query);

            if (resourceNodeResults.Count == 0)
            {
                _logger.LogInternalWarning($"Resource with ID {resourceId} not found in graph database.");
                return string.Empty;
            }

            // Calculate expiration time
            var absoluteExpiration = DateTimeOffset.Now + ignoreTagDuration;

            // Create a unique ID for the auth configuration node
            var configNodeId = $"{resourceNodeId}_auth_config";

            // Prepare properties for the configuration node
            var properties = new Dictionary<string, object>
            {
                { "resourceId", resourceId },
                { "targetResourceId", resourceId },
                { "last_checked_at", DateTime.UtcNow },
                { "notification_ignoreuntil", absoluteExpiration },
                { "latest_status", actionTaken },
                { "resourceName", $"AuthConfig-{resourceId.Split('/').Last()}" },
                { "updateTs", DateTime.UtcNow.Ticks },
                { "isDeleted", false }
            };

            // Create the auth configuration node
            await _graphDbClient.AddOrUpdateNodeAsync(
                "resource_auth_config",
                configNodeId,
                "resource_auth_config",
                properties);

            // Create an edge from the resource node to the auth configuration node
            await _graphDbClient.AddOrUpdateEdgeAsync(
                resourceNodeId,
                configNodeId,
                "HAS_AUTH_CONFIG");

            _logger.LogInternalInformation($"Created auth configuration for resource {resourceId} with ignoreuntil {absoluteExpiration}");
            return "Auth configuration created successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error creating auth configuration node.");
            return "Error creating auth configuration.";
        }
    }

    /// <summary>
    /// Links a source code repository to a container app in the knowledge graph.
    /// </summary>
    public async Task AddSourceCodeNodeToContainerAppNodeAsync(string resourceId, string repoUrl)
    {
        try
        {
            var containerAppNodeId = resourceId.ToLower().Replace("/", "_");
            var vertexFilter = $"hasId('{containerAppNodeId}')";
            var query = $@"g.V().{vertexFilter}.has('isDeleted', false)";
            var containerAppNodeResults = await _graphDbClient.Query(query);
            if (containerAppNodeResults.Count == 0)
            {
                return;
            }

            var sourceCodeNodeId = repoUrl.ToLower().Replace("/", "_"); var checkSourceCodeNodeQuery = $"g.V('{sourceCodeNodeId}').hasLabel('microsoft.source/repository').has('isDeleted', false)";
            var sourceCodeNodeResults = await _graphDbClient.Query(checkSourceCodeNodeQuery);

            if (sourceCodeNodeResults.Count == 0)
            {
                var properties = new Dictionary<string, object>
                {
                    { "resourceId", repoUrl },
                    { "subscriptionId", "githubrepo-sub" },
                    { "resourceGroupName", "githubrepo-rg" },
                    { "resourceName", sourceCodeNodeId },
                    { "updateTs", DateTime.UtcNow.Ticks }
                };

                await _graphDbClient.AddOrUpdateNodeAsync("microsoft.source/repository", sourceCodeNodeId, "microsoft.source/repository", properties);
            }

            await _graphDbClient.AddOrUpdateEdgeAsync(containerAppNodeId, sourceCodeNodeId, Constants.Relationships.ServesCode);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error finding network connected resources");
        }
    }

    public async Task<List<string>> GetContainerAppsWithNodesWithoutSourceCodeNodesAsync()
    {
        var queryResults = await _graphDbClient.Query(@"                g.V().has('resourceType', 'microsoft.app/containerapps').has('isDeleted', false)
                .not(outE().hasLabel('SERVES_CODE').inV().has('resourceType', 'microsoft.source/repository').has('isDeleted', false))
                .values('resourceId')");

        var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();

        return resources;
    }

    /// <summary>
    /// Updates a repository node with the last scan timestamp.
    /// </summary>
    public async Task UpdateRepoNodeWithLastScanTime(string repoUrl)
    {
        var queryResults = await _graphDbClient.Query($@"                g.V().has('resourceId', '{repoUrl}').has('isDeleted', false)
                .values('id', 'label', 'resourceId', 'subscriptionId', 'resourceGroupName', 'resourceName', 'updateTs', 'resourceType')");
        var propertiesArray = queryResults.ToList();
        var id = (string)propertiesArray[0];
        var label = (string)propertiesArray[1];
        var resourceId = (string)propertiesArray[2];
        var subscriptionId = (string)propertiesArray[3];
        var resourceGroupName = (string)propertiesArray[4];
        var resourceName = (string)propertiesArray[5];
        var updateTs = (long)propertiesArray[6];
        var resourceType = (string)propertiesArray[7];

        var properties = new Dictionary<string, object>
        {
            { "resourceId", repoUrl },
            { "subscriptionId", subscriptionId},
            { "resourceGroupName", resourceGroupName },
            { "resourceName", resourceName },
            { "updateTs", updateTs },
            { "lastScanTime", DateTime.UtcNow }
        };

        await _graphDbClient.AddOrUpdateNodeAsync(label, id, resourceType, properties);
    }

    public async Task<Dictionary<string, object>> GetResourceBasicProperties(string resourceId)
    {
        var query = $@"
                g.V('{CrawlerExtensions.GetSanitizedCosmosDBId(resourceId)}').has('isDeleted', false)
                .project('subscriptionId', 'resourceGroupName', 'resourceType', 'resourceName', 'location', 'kind', 'linuxFxVersion')
                .by(coalesce(values('subscriptionId'), constant('unknown')))
                .by(coalesce(values('resourceGroupName'), constant('unknown')))
                .by(coalesce(values('resourceType'), constant('unknown')))
                .by(coalesce(values('resourceName'), constant('unknown')))
                .by(coalesce(values('location'), constant('unknown')))
                .by(coalesce(values('kind'), constant('')))
                .by(coalesce(values('linuxFxVersion'), constant('')))
                ";

        var results = await _graphDbClient.Query<Dictionary<string, object>>(query);

        return results.FirstOrDefault([]);
    }

    /// <summary>
    /// Gets detailed properties for a resource from the knowledge graph.
    /// </summary>
    public async Task<Dictionary<string, object>> GetResourceDetailedProperties(string resourceId)
    {
        resourceId = Regex.Replace(resourceId, $"^{Regex.Escape(Constants.AzureManagementPrefix)}", "", RegexOptions.IgnoreCase);

        if (!ResourceIdentifier.TryParse(resourceId, out _))
        {
            throw new Exception("Invalid Azure resource Id, should be of form /subscriptions/<>/resourceGroups/<>/providers/<providerName>/<resourceType>/<resourceName>");
        }
        var query = $@"g.V('{CrawlerExtensions.GetSanitizedCosmosDBId(resourceId.ToLower())}').has('isDeleted', false).properties().as('p').where(select('p').key().is(neq('appHealthInfo'))).group().by(select('p').key()).by(select('p').value())";

        var results = await _graphDbClient.Query<Dictionary<string, object>>(query);
        return results.FirstOrDefault([]);
    }

    /// <summary>
    /// Returns the URL to the knowledge graph resource usage dashboard.
    /// </summary>
    public string GetKnowledgeGraphResourceUsageDashboard()
    {
        if (string.IsNullOrEmpty(_dashboardSettings?.PrometheusUrl))
        {
            return "Dashboard is not configured for this agent. Must use Knowledge graph queries. If user wants agent to deploy a dashboard they must configure Dashboard Settings.";
        }

        return $"Dashboard URL: {_dashboardSettings.GrafanaUrl}/d/{AgentNameHelper.GetMainDashboardUid(_hostEnvironment.IsProduction())}/sre-azure-resource-overview?orgId=1&refresh=1m";
    }

    /// <summary>
    /// Searches for resources by partial name, resource types, and/or other filters.
    /// At least one filter parameter must be provided (resourceName, resourceTypes, subscriptionId, or location).
    /// When multiple filters are provided, AND logic is applied.
    /// Results include resourceId, resourceName, location (plus clusterResourceId/namespace for K8s).
    /// </summary>
    public async Task<List<object>> SearchResourceAsync(
        string? resourceName,
        List<string>? resourceTypes = null,
        string? subscriptionId = null,
        string? location = null,
        int limit = 50)
    {
        var normalizedTypes = NormalizeResourceTypes(resourceTypes);

        var hasName = !string.IsNullOrWhiteSpace(resourceName);
        var hasTypes = normalizedTypes.Count > 0;
        var hasSubscriptionId = !string.IsNullOrWhiteSpace(subscriptionId);
        var hasLocation = !string.IsNullOrWhiteSpace(location);

        if (!hasName && !hasTypes && !hasSubscriptionId && !hasLocation)
        {
            throw new ArgumentException("At least one filter parameter must be provided for search (resourceName, resourceTypes, subscriptionId, or location).");
        }

        try
        {
            var queryBuilder = new StringBuilder("g.V().has('isDeleted', false)");

            if (hasName)
            {
                var sanitizedResourceName = SanitizeInputForQuery(resourceName!);
                queryBuilder.Append($".where(values('resourceName').is(containing('{sanitizedResourceName}')))");
            }

            if (hasTypes)
            {
                var typeFilters = normalizedTypes.Select(t => $"hasLabel('{t}')");
                queryBuilder.Append($".or({string.Join(", ", typeFilters)})");
            }

            if (hasSubscriptionId)
            {
                var sanitizedSubId = SanitizeInputForQuery(subscriptionId!);
                queryBuilder.Append($".has('subscriptionId', '{sanitizedSubId}')");
            }

            if (hasLocation)
            {
                var sanitizedLoc = SanitizeInputForQuery(location!);
                queryBuilder.Append($".has('location', '{sanitizedLoc}')");
            }

            queryBuilder.Append($@"
                .project('resourceId', 'resourceName', 'resourceType', 'location', 'namespace', 'clusterResourceId')
                .by(coalesce(values('resourceId'), constant('')))
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(coalesce(values('location'), constant('')))
                .by(coalesce(values('namespace'), constant('')))
                .by(coalesce(values('clusterResourceId'), constant('')))
                .limit({limit})");

            var query = queryBuilder.ToString();
            _logger.LogInternalInformation("Executing SearchResourceAsync with query: {Query}", query);

            var result = await _graphDbClient.Query(query);
            var resources = result.Select(item => (object)BuildResourceDictionary(item)).ToList();

            // If knowledge graph returned no results, fall back to Azure Resource Graph
            if (resources.Count == 0)
            {
                _logger.LogInternalInformation("No results from knowledge graph, falling back to Azure Resource Graph");
                var argResults = await QueryAzureResourceGraphAsync(resourceName, resourceTypes, subscriptionId, location, limit);
                return argResults;
            }

            _logger.LogInternalInformation("SearchResourceAsync returned {Count} results", resources.Count);
            return resources;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error searching for resources with name '{Name}' and types '{Types}'", resourceName, resourceTypes != null ? string.Join(", ", resourceTypes) : "none");
            throw;
        }
    }

    /// <summary>
    /// Queries Azure Resource Graph by name pattern, resource types, and/or other filters.
    /// Returns slim fields: resourceId, resourceName, location.
    /// Uses AND logic when multiple filters are provided.
    /// </summary>
    private async Task<List<object>> QueryAzureResourceGraphAsync(
        string? resourceName,
        List<string>? resourceTypes,
        string? subscriptionId,
        string? location,
        int limit)
    {
        var resources = new List<object>();

        try
        {
            var subscriptionIds = GetSubscriptionIdsFromCrawlRoots(subscriptionId);

            if (subscriptionIds.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(subscriptionId))
                {
                    _logger.LogInternalWarning("Requested subscription '{SubscriptionId}' is not in crawl roots, returning empty results", subscriptionId);
                    return resources;
                }

                _logger.LogInternalError("No subscription IDs found in crawl roots, cannot query Azure Resource Graph");
                throw new InvalidOperationException("No subscription IDs found in crawl roots, cannot query Azure Resource Graph. Please add managed resources to your agent configuration.");
            }

            // Check if all requested types are container types (subscriptions/resourcegroups)
            var isContainerQuery = resourceTypes != null &&
                resourceTypes.Count > 0 &&
                resourceTypes.All(t => ArgResourceContainerTypes.Contains(t));

            var tableName = isContainerQuery ? "resourcecontainers" : "Resources";
            var filters = BuildArgQueryFilters(resourceName, resourceTypes, location, isContainerQuery);
            var whereClause = filters.Count > 0 ? $"| where {string.Join(" and ", filters)}" : "";
            var projection = GetArgProjection(isContainerQuery);

            var query = $@"
                {tableName}
                {whereClause}
                {projection}
                | limit {limit}
            ";

            _logger.LogInternalInformation("Executing Azure Resource Graph query with filters: name='{Name}', types='{Types}', subscriptionId='{SubId}', location='{Loc}'",
                resourceName ?? "none",
                resourceTypes != null ? string.Join(", ", resourceTypes) : "none",
                subscriptionId ?? "none",
                location ?? "none");

            var result = await _azureResourceGraphClient.Query([.. subscriptionIds], query);

            if (result?.Data is not null)
            {
                var jsonArray = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result.Data.ToString());
                if (jsonArray is not null)
                {
                    resources.AddRange(jsonArray);
                }
            }

            _logger.LogInternalInformation("Azure Resource Graph returned {Count} results", resources.Count);
            return resources;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error querying Azure Resource Graph");
            throw new InvalidOperationException("Error querying Azure Resource Graph", ex);
        }
    }

    private async Task<dynamic> GetAllResourceCountAsync()
    {
        try
        {
            var resourceIdFilter = string.Join(",", _crawlRoots.Select(r => $@"has('resourceId', startingWith('{r.ToLower()}'))"));
            var query = !string.IsNullOrEmpty(resourceIdFilter) ? $@"g.V().has('isDeleted', false).or({resourceIdFilter}).groupCount().by(label())" : $@"g.V().has('isDeleted', false).groupCount().by(label())";
            var result = await _graphDbClient.Query<Dictionary<string, object>>(query);

            if (result is null || result.Count == 0)
            {
                _logger.LogInternalWarning("No resources found in the graph");
                return new { Status = "failed", Message = "No resources found in the graph" };
            }
            var excludedResourceTypes = new HashSet<string>
            {
                "microsoft.source/repository",
                "microsoft.authorization/roleassignments",
                "microsoft.authorization/roledefinitions",
                "microsoft.alertsmanagement/smartdetectoralertrules",
                "microsoft.insights/metricalerts"
            };
            var counts = result.First().Where(kvp => kvp.Key.StartsWith("microsoft") && !excludedResourceTypes.Contains(kvp.Key) && kvp.Key.Count(c => c == '/') == 1).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return counts;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting all resource count");
            throw;
        }
    }

    /// <summary>
    /// Gets the count of resources of a specified type, optionally grouped by a property.
    /// </summary>
    public async Task<dynamic> GetResourceCountAsync(string resourceType, string groupBy = "")
    {
        if (string.Equals(resourceType, "all", StringComparison.OrdinalIgnoreCase))
        {
            return await GetAllResourceCountAsync();
        }

        try
        {
            string query;

            if (string.IsNullOrWhiteSpace(groupBy))
            {
                query = $@"
                g.V().hasLabel('{resourceType.ToLower()}').has('isDeleted', false)
                .count()
            ";

                var result = await _graphDbClient.Query(query);

                long count = 0;
                if (result != null && result.Count > 0)
                {
                    count = Convert.ToInt64(result.First());
                    _logger.LogInternalInformation("Found {Count} resources of type '{Type}'", count, resourceType);
                }

                return new { ResourceType = resourceType, Count = count };
            }
            else
            {
                query = $@"
    g.V().hasLabel('{resourceType.ToLower()}').has('isDeleted', false)
    .project('id', 'propertyValue')
    .by(id())
    .by(
        coalesce(
            choose(has('{groupBy}'),
                properties('{groupBy}').value(),
                constant('Unknown')
            ),
            constant('Unknown')
        )
    )
";

                var result = await _graphDbClient.Query(query);
                var groupedResults = new Dictionary<string, long>();

                foreach (var item in result)
                {
                    try
                    {
                        var groupValue = "Unknown";

                        try
                        {
                            groupValue = item.propertyValue?.ToString() ?? "Unknown";
                        }
                        catch
                        {
                            // Fallback to dictionary-style access
                            if (item is IDictionary<string, object> dict && dict.ContainsKey("propertyValue"))
                            {
                                groupValue = dict["propertyValue"]?.ToString() ?? "Unknown";
                            }
                        }

                        // Update the count in our dictionary
                        if (!groupedResults.ContainsKey(groupValue))
                        {
                            groupedResults[groupValue] = 0;
                        }

                        groupedResults[groupValue]++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning(ex, "Error processing result item");
                    }
                }

                _logger.LogInternalInformation("Found resource counts grouped by '{GroupBy}' for type '{Type}'", groupBy, resourceType);
                return new { ResourceType = resourceType, GroupBy = groupBy, Counts = groupedResults };
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting count for resource type '{Type}' grouped by '{GroupBy}'", resourceType, groupBy);
            throw;
        }
    }

    /// <summary>
    /// Gets a summary of all managed Azure resources and their counts by type.
    /// </summary>
    public async Task<dynamic> GetManagedResourcesInfoAsync()
    {
        try
        {
            var azureResourceTypes = new List<string>
            {
                "microsoft.app/containerapps",
                "microsoft.sql/servers",
                "microsoft.documentdb/databaseaccounts", // Cosmos DB
                "microsoft.cache/redis",
                "microsoft.web/sites", // WebApps
                "microsoft.containerservice/managedclusters", // Kubernetes
                "microsoft.network/virtualnetworks",
                "microsoft.storage/storageaccounts",
                "microsoft.servicebus/namespaces",
            };

            var otherResourceTypes = new List<string>
            {
                "microsoft.source/repository"
            };

            var allResourceTypes = azureResourceTypes.Concat(otherResourceTypes).ToList();

            var resourceTypeTasks = new List<Task<dynamic>>();
            foreach (var resourceType in allResourceTypes)
            {
                resourceTypeTasks.Add(GetResourceCountAsync(resourceType));
            }
            var results = await Task.WhenAll(resourceTypeTasks);

            var managedResources = new Dictionary<string, long>();
            var otherResources = new Dictionary<string, long>();

            foreach (var result in results)
            {
                string resourceType = result.ResourceType.ToString();
                long count = result.Count;

                if (count > 0)
                {
                    var simpleName = resourceType.Split('/').Last();

                    if (azureResourceTypes.Contains(resourceType.ToLower()))
                    {
                        managedResources[simpleName] = count;
                    }
                    else
                    {
                        otherResources[simpleName] = count;
                    }
                }
            }

            var totalAzureCount = managedResources.Values.Sum();
            var totalOtherCount = otherResources.Values.Sum();

            return new
            {
                AzureResources = managedResources,
                OtherResources = otherResources,
                TotalAzureResourceCount = totalAzureCount,
                TotalOtherResourceCount = totalOtherCount,
                TotalResourceCount = totalAzureCount + totalOtherCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting managed resources information");
            throw;
        }
    }

    #region Private Utilities

    /// <summary>
    /// Escapes single quotes to prevent injection attacks in Gremlin/KQL queries.
    /// </summary>
    private static string SanitizeInputForQuery(string input)
    {
        return input.Replace("'", "''");
    }

    /// <summary>
    /// Normalizes resource types for queries by lowercasing them.
    /// </summary>
    private static List<string> NormalizeResourceTypes(List<string>? resourceTypes)
    {
        if (resourceTypes == null || resourceTypes.Count == 0)
            return [];

        return [.. resourceTypes.Select(t => t.ToLower())];
    }

    /// <summary>
    /// Builds a resource dictionary from graph query result item.
    /// Handles both standard Azure resources and Kubernetes namespaced resources.
    /// </summary>
    private static Dictionary<string, string> BuildResourceDictionary(dynamic item)
    {
        var resourceId = item["resourceId"]?.ToString() ?? string.Empty;
        var name = item["resourceName"]?.ToString() ?? string.Empty;
        var resourceLocation = item["location"]?.ToString() ?? string.Empty;
        var resourceType = item["resourceType"]?.ToString() ?? string.Empty;
        var namespaceValue = item["namespace"]?.ToString() ?? string.Empty;
        var clusterResourceId = item["clusterResourceId"]?.ToString() ?? string.Empty;

        var isK8sNamespacedResource = resourceType.StartsWith("k8s/") &&
            !string.IsNullOrEmpty(namespaceValue) &&
            !string.IsNullOrEmpty(clusterResourceId);

        var result = new Dictionary<string, string>
        {
            ["resourceId"] = resourceId,
            ["resourceName"] = name,
            ["location"] = resourceLocation
        };

        if (isK8sNamespacedResource)
        {
            result["clusterResourceId"] = clusterResourceId;
            result["namespace"] = namespaceValue;
        }

        return result;
    }

    /// <summary>
    /// Extracts subscription IDs from crawl roots, optionally filtering to a specific subscription.
    /// </summary>
    private HashSet<string> GetSubscriptionIdsFromCrawlRoots(string? subscriptionIdFilter = null)
    {
        var subscriptionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var crawlRoot in _crawlRoots)
        {
            if (ResourceIdentifier.TryParse(crawlRoot, out var id) && id is not null && !string.IsNullOrWhiteSpace(id.SubscriptionId))
            {
                subscriptionIds.Add(id.SubscriptionId);
            }
        }

        if (!string.IsNullOrWhiteSpace(subscriptionIdFilter))
        {
            var sanitizedSubId = SanitizeInputForQuery(subscriptionIdFilter);
            subscriptionIds = subscriptionIds
                .Where(s => s.Equals(sanitizedSubId, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return subscriptionIds;
    }

    /// <summary>
    /// Builds ARG query filters based on provided parameters.
    /// For container queries, also searches in displayName for subscription friendly names.
    /// </summary>
    private static List<string> BuildArgQueryFilters(
        string? resourceName,
        List<string>? argTypes,
        string? location,
        bool isContainerQuery)
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(resourceName))
        {
            var sanitizedName = SanitizeInputForQuery(resourceName);
            if (isContainerQuery)
            {
                filters.Add($"(name contains '{sanitizedName}' or properties.displayName contains '{sanitizedName}')");
            }
            else
            {
                filters.Add($"name contains '{sanitizedName}'");
            }
        }

        if (argTypes != null && argTypes.Count > 0)
        {
            var typeConditions = argTypes.Select(t => $"type =~ '{t.ToLower()}'");
            filters.Add($"({string.Join(" or ", typeConditions)})");
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var sanitizedLoc = SanitizeInputForQuery(location);
            filters.Add($"location =~ '{sanitizedLoc}'");
        }

        return filters;
    }

    /// <summary>
    /// Gets the ARG projection clause based on query type.
    /// Container queries include displayName and subscriptionId for friendly names.
    /// </summary>
    private static string GetArgProjection(bool isContainerQuery)
    {
        if (isContainerQuery)
        {
            return "| project resourceId = id, resourceName = name, location, displayName = coalesce(properties.displayName, name), subscriptionId";
        }

        return "| project resourceId = id, resourceName = name, location";
    }

    #endregion

    #region Internal Methods (not exposed to LLM)

    /// <summary>
    /// Returns a list of subscription IDs by querying all vertices that have a 'subscriptionId' property.
    /// </summary>
    public async Task<List<dynamic>> ListSubscriptionsAsync()
    {
        try
        {
            var query = $@"g.V().has('resourceType', '{SubscriptionNode.Type}').has('isDeleted', false)
                          .project('name', 'id')
                          .by('subscriptionName')
                          .by('subscriptionId')";

            var result = await _graphDbClient.Query(query);

            _logger.LogInternalInformation("Found {Count} subscriptions", result.Count);

            // Return the list of subscription objects with name and id properties intact
            return [.. result];
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error listing subscriptions");
            throw;
        }
    }

    /// <summary>
    /// Returns a list of resource groups for a given subscription ID.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> ListResourceGroupsAsync(string subscriptionId)
    {
        try
        {
            // Query with the correct lowercase 'resourcegroups' type
            var query = $@"g.V().has('resourceType', 'resourcegroups').has('isDeleted', false)
                         .has('subscriptionId', '{subscriptionId}')
                         .project('subscriptionId', 'resourceGroupName', 'resourceType', 'resourceId', 'location')
                         .by(coalesce(values('subscriptionId'), constant('')))
                         .by(coalesce(values('resourceGroupName'), constant('')))
                         .by(coalesce(values('resourceType'), constant('')))
                         .by(coalesce(values('resourceId'), constant('')))
                         .by(coalesce(values('location'), constant('')))";

            var result = await _graphDbClient.Query(query);
            var resources = new List<Dictionary<string, object>>();

            foreach (var item in result)
            {
                var propertyBag = new Dictionary<string, object>
                {
                    ["subscriptionId"] = item["subscriptionId"]?.ToString() ?? string.Empty,
                    ["resourceGroupName"] = item["resourceGroupName"]?.ToString() ?? string.Empty,
                    ["resourceType"] = item["resourceType"]?.ToString() ?? string.Empty,
                    ["resourceId"] = item["resourceId"]?.ToString() ?? string.Empty,
                    ["location"] = item["location"]?.ToString() ?? string.Empty
                };

                resources.Add(propertyBag);
            }

            _logger.LogInternalInformation("Found {Count} resource groups for subscription '{SubscriptionId}'", resources.Count, subscriptionId);
            return resources;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error listing resource groups for subscription '{SubscriptionId}'", subscriptionId);
            throw;
        }
    }

    /// <summary>
    /// Returns a list of resources of a specified type with their complete property bag.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> ListResourcesByTypeAsync(
        string resourceType, string propertyName, string propertyValue, int skip = 0, int take = 50)
    {
        var actualGraphResourceType = resourceType.ToLower();

        var kindToGraphTypeMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "deployment", "k8s/apps/v1/deployments" }, { "deployments", "k8s/apps/v1/deployments" },
            { "statefulset", "k8s/apps/v1/statefulsets" }, { "statefulsets", "k8s/apps/v1/statefulsets" },
            { "node", "k8s/core/v1/nodes" }, { "nodes", "k8s/core/v1/nodes" },
            { "service", "k8s/core/v1/services" }, { "services", "k8s/core/v1/services" },
            { "namespace", "k8s/core/v1/namespaces" }, { "namespaces", "k8s/core/v1/namespaces" },
            { "configmap", "k8s/core/v1/configmaps" }, { "configmaps", "k8s/core/v1/configmaps" },
            { "secret", "k8s/core/v1/secrets" }, { "secrets", "k8s/core/v1/secrets" },
            { "persistentvolumeclaim", "k8s/core/v1/persistentvolumeclaims" }, { "persistentvolumeclaims", "k8s/core/v1/persistentvolumeclaims" },
            { "pvc", "k8s/core/v1/persistentvolumeclaims" }
        };

        if (kindToGraphTypeMapping.ContainsKey(resourceType))
        {
            actualGraphResourceType = kindToGraphTypeMapping[resourceType];
        }

        try
        {
            var sb = new StringBuilder();
            sb.Append("g.V().has('isDeleted', false)");

            if (actualGraphResourceType != "all")
            {
                sb.Append($".hasLabel('{actualGraphResourceType}')");
            }

            if (!string.IsNullOrEmpty(propertyName) && !string.IsNullOrEmpty(propertyValue))
            {
                sb.Append($".has('{propertyName}', '{propertyValue}')");
            }

            sb.Append(@".project('subscriptionId', 'resourceGroupName', 'resourceName', 'resourceType', 'clusterResourceId', 'namespace')
                    .by(coalesce(values('subscriptionId'), constant('')))
                    .by(coalesce(values('resourceGroupName'), constant('')))
                    .by(coalesce(values('resourceName'), constant('')))
                    .by(coalesce(values('resourceType'), constant('')))
                    .by(coalesce(values('clusterResourceId'), constant('')))
                    .by(coalesce(values('namespace'), constant('')))");

            // Add skip and take only if take > 0
            if (take > 0)
            {
                sb.Append($".limit({take})");
                _logger.LogInternalInformation("Will take {take} resources of type '{ResourceType}'", take, actualGraphResourceType);
            }

            var result = await _graphDbClient.Query(sb.ToString());
            var resources = new List<Dictionary<string, object>>();

            foreach (var item in result)
            {
                // Create a new dictionary for each resource
                var propertyBag = new Dictionary<string, object>
                {
                    ["subscriptionId"] = item?["subscriptionId"]?.ToString() ?? string.Empty,
                    ["resourceGroupName"] = item?["resourceGroupName"]?.ToString() ?? string.Empty,
                    ["resourceName"] = item?["resourceName"]?.ToString() ?? string.Empty,
                    ["resourceType"] = item?["resourceType"]?.ToString() ?? string.Empty
                };

                if (!string.IsNullOrEmpty(item?["clusterResourceId"]?.ToString()))
                {
                    propertyBag["clusterResourceId"] = item?["clusterResourceId"]?.ToString() ?? string.Empty;
                }
                if (!string.IsNullOrEmpty(item?["namespace"]?.ToString()))
                {
                    propertyBag["namespace"] = item?["namespace"]?.ToString() ?? string.Empty;
                }

                resources.Add(propertyBag);
            }

            _logger.LogInternalInformation("Found {Count} resources of type '{ActualResourceType}' matching filters.", resources.Count, actualGraphResourceType);
            return resources;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error listing resources of type '{Type}'", actualGraphResourceType);
            throw;
        }
    }

    #endregion
}
