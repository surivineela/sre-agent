// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Exceptions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Schema;
using Agent.Plugins.Interface;
using Azure;
using Azure.Core;
using Gremlin.Net.Driver;
using Microsoft.Azure.Management.Monitor.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using Microsoft.Rest.Azure.OData;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class GraphDBPlugin : IGraphDBPlugin
    {
        // Using ThreadContext from your original code. When a thread ID is needed, we pull it from here.
        public Guid? ThreadId { get; set; }

        private readonly IGraphDatabaseClient _graphDbClient;
        private readonly IChatClientProvider _chatClientProvider;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
        private readonly ILogger<GraphDBPlugin> _logger;

        // Additional fields for health dashboard screenshot and LLM summarization
        private readonly string _grafanaUrl;
        private readonly string _puppeteerScreenshotApiUrl;
        private readonly HttpClient _httpClient;
        private readonly DashboardSettings _dashboardSettings;
        private readonly IAuthenticationService _authService;
        private readonly List<string> _crawlRoots;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly AzureResourceGraphClient _azureResourceGraphClient;

        private const string MermaidServiceAPI = "https://mermaid-renderer.salmonhill-ad96bd78.eastus2.azurecontainerapps.io/render";

        private readonly Dictionary<string, string> _dashboardsToProcessByResourceType = new(StringComparer.OrdinalIgnoreCase)
        {
            { "microsoft.app/containerapps", "azure-container-apps-container-app-view" },
            { "microsoft.storage/storageaccounts", "azure-insights-storage-accounts" },
            { "microsoft.documentdb/databaseaccounts", "azure-insights-cosmos-db" },
            { "microsoft.cache/redis", "azure-redis" },
            { "microsoft.web/sites", "azure-app-service-monitoring" },
            // Pending: webapp, sql
        };

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

            _grafanaUrl = dashboardSettings.GrafanaUrl.TrimEnd('/');
            _puppeteerScreenshotApiUrl = "https://test-capp.ambitiouspond-10f27fe1.canadaeast.azurecontainerapps.io";
            _httpClient = new HttpClient();
            _authService = authService;
            _crawlRoots = crawlerSettings.CrawlRoots.Split([','], StringSplitOptions.RemoveEmptyEntries)
                .Select(root => root.Trim())
                .ToList();

            _hostEnvironment = hostEnvironment;
            _azureResourceGraphClient = azureResourceGraphClient;
        }

        /// <summary>
        /// When implementing this in prod, we need to give this agent a read-only user
        /// </summary>
        [KernelFunction("query")]
        [Description("Run a generic query against the graph database. Do NOT perform any write operations.")]
        public async Task<ResultSet<dynamic>> Query(string query)
        {
            return await _graphDbClient.Query(query);
        }

        public async Task<string> FindAllNetworkConnectedResources(string resourceId = "")
        {
            try
            {
                string vertexFilter = string.IsNullOrEmpty(resourceId)
                    ? "hasLabel('microsoft.app/containerapps')"
                    : $"hasId('{resourceId.ToLower().Replace("/", "_")}')"; // Replacing "/" with "_" as graph IDs use underscores

                string query = $@"
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

        public async Task<List<Node>> GetApplicationComponentsSummary(string resourceId, int hops = 3)
        {
            _logger.LogInternalInformation($"[GetApplicationComponentsSummary] Invoked with resourceId: {resourceId}");

            var result = await GetApplicationComponentsRaw(resourceId, hops);
            return ConvertResultToNodes(result);
        }

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

            int maxRetries = 3;
            int retryDelayMilliseconds = 1000; // 1 second

            for (int attempt = 1; attempt <= maxRetries; attempt++)
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
                    string tempJson = JsonSerializer.Serialize(result);

                    var typedResult = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(tempJson)
                  ?? new List<Dictionary<string, object>>();


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
                            string itemJson = JsonSerializer.Serialize(filteredItem, jsonSerializerOptionsForDeduplication);

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

                    string mermaidMessage = $"```mermaid\n{mermaidSpec}\n```";

                    Guid messageId = Guid.NewGuid();

                    // Save to database via the outbound service
                    await _agentOutboundCommunicationService.AppendAgentImageMessage(threadId.Value, mermaidMessage, messageId);

                    // Stream the mermaid data directly to bypass tool call limitations
                    await _agentOutboundCommunicationService.AppendAgentStreamMessage(threadId.Value, mermaidMessage, StreamMessageType.Mermaid, messageId);

                    // Construct the final response string for the LLM, this helps the LLM to further answer questions regarding the topology
                    string llmResponse = $@"I have analyzed the microservice topology starting from '{deploymentName}' in the '{_namespace}' namespace within the cluster '{AKSClusterResourceId}'.

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

            int maxRetries = 3;
            int retryDelayMilliseconds = 1000; // 1 second

            for (int attempt = 1; attempt <= maxRetries; attempt++)
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

                    string mermaidMessage = $"```mermaid\n{mermaidSpec}\n```";


                    Guid messageId = Guid.NewGuid();

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

        private async Task<string> GenerateMermaidGraph(string mermaidSpec)
        {
            try
            {
                _logger.LogInternalInformation("Calling Mermaid rendering service to generate graph visualization");

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);

                    var jsonPayload = new { spec = mermaidSpec };
                    var requestContent = new StringContent(
                        JsonSerializer.Serialize(jsonPayload),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    try
                    {
                        var response = await httpClient.PostAsync(MermaidServiceAPI, requestContent);

                        if (response.IsSuccessStatusCode)
                        {
                            var jsonResponse = await response.Content.ReadAsStringAsync();
                            var responseObject = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                            var base64Image = responseObject.GetProperty("image_base64").GetString();

                            _logger.LogInternalInformation("Successfully generated graph visualization");
                            return base64Image ?? string.Empty;
                        }
                        else
                        {
                            var errorMessage = await response.Content.ReadAsStringAsync();
                            _logger.LogInternalError($"Error calling Mermaid rendering service. Status: {response.StatusCode}, Message: {errorMessage}");
                            return $"Error generating visualization: {response.StatusCode}. The rendering service returned an error. Please try a smaller graph or different visualization settings.";
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogInternalWarning("The visualization request timed out after {Timeout} seconds", httpClient.Timeout.TotalSeconds);
                        return $"The visualization request timed out after {httpClient.Timeout.TotalSeconds} seconds. The graph may be too complex to render. Here's the raw Mermaid specification that you can paste into a Mermaid editor:\n\n```mermaid\n{mermaidSpec}\n```";
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogInternalError(ex, "HTTP request error while calling Mermaid rendering service");
                        return $"Error connecting to the visualization service: {ex.Message}. Please check network connectivity or try again later.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Exception occurred while calling Mermaid rendering service");
                return $"Error generating visualization: {ex.Message}";
            }
        }

        // TODO: Consider refactoring this method to use a more generic approach for querying the graph database, for now it's only support Deployment, we need to support StatefulSet as well.
        private async Task<ResultSet<dynamic>> GetAKSMicroserviceTopologyRaw(string resourceId, string namespaceName, string deploymentName)
        {
            try
            {
                string formattedResourceId = resourceId.ToLower().Replace("/", "_");

                // Query with specific deployment as starting point
                string deploymentResourceId = $"{formattedResourceId}_apps_v1_namespaces_{namespaceName}_deployments_{deploymentName}";

                // We are leaving out contains for now
                string query = $@"
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
                string query = $@"g.V().has('id', '{resourceId.ToLower().Replace("/", "_")}').has('isDeleted', false)
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

        public async Task<List<ApplicationGraph>> DiscoverApplications(string subscriptionId)
        {
            _logger.LogInternalInformation($"[DiscoverApplications] Invoked with subscription {subscriptionId}");

            try
            {
                string entryPointQuery = BuildDiscoverApplicationsQuery(subscriptionId);
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
                        Nodes = components.Select(c => new SimpleNode(c)).ToList()
                    };

                    applications.Add(application);
                }

                return applications;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error discovering applications");
                return new List<ApplicationGraph>();
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
                // For consistency, we assume the properties are already a dictionary.
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
        /// Flattens nested property values from Gremlin query results.
        /// </summary>
        private Dictionary<string, object> FlattenProperties(dynamic properties)
        {
            var flatProps = new Dictionary<string, object>();
            foreach (var kvp in properties)
            {
                if (kvp.Value is IEnumerable<object> arr)
                {
                    var first = arr.FirstOrDefault();
                    if (first is IDictionary<string, object> dict && dict.ContainsKey("value"))
                    {
                        flatProps[kvp.Key] = dict["value"];
                    }
                    else
                    {
                        flatProps[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    flatProps[kvp.Key] = kvp.Value;
                }
            }
            return flatProps;
        }

        /// <summary>
        /// Converts Gremlin query results to ArmResourceNode objects.
        /// </summary>
        private List<ArmResourceNode> ConvertResultToArmResourceNodes(ResultSet<dynamic> result)
        {
            var nodes = new List<ArmResourceNode>();

            foreach (var item in result)
            {
                var properties = FlattenProperties(item["properties"]);
                var node = new ArmResourceNode
                {
                    ResourceName = item["resourceName"],
                    ResourceType = item["resourceType"],
                    SubscriptionId = properties.ContainsKey("subscriptionId") ? properties["subscriptionId"].ToString() : string.Empty,
                };

                nodes.Add(node);
            }

            return nodes;
        }

        public async Task<string> AddIgnoreInfoToResource(string resourceId, TimeSpan ignoreTagDuration, string actionTaken)
        {
            try
            {
                // Format the resource node ID
                var resourceNodeId = CrawlerExtensions.GetSanitizedCosmosDBId(resourceId);                // Check if the resource node exists
                string query = $@"g.V().hasId('{resourceNodeId}').has('isDeleted', false)";
                var resourceNodeResults = await _graphDbClient.Query(query);

                if (!resourceNodeResults.Any())
                {
                    _logger.LogInternalWarning($"Resource with ID {resourceId} not found in graph database.");
                    return string.Empty;
                }

                // Calculate expiration time
                var absoluteExpiration = DateTimeOffset.Now + ignoreTagDuration;

                // Create a unique ID for the auth configuration node
                string configNodeId = $"{resourceNodeId}_auth_config";

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

        public async Task AddSourceCodeNodeToContainerAppNodeAsync(string resourceId, string repoUrl)
        {
            try
            {
                var containerAppNodeId = resourceId.ToLower().Replace("/", "_");
                string vertexFilter = $"hasId('{containerAppNodeId}')";
                string query = $@"g.V().{vertexFilter}.has('isDeleted', false)";
                var containerAppNodeResults = await _graphDbClient.Query(query);
                if (!containerAppNodeResults.Any())
                {
                    return;
                }

                string sourceCodeNodeId = repoUrl.ToLower().Replace("/", "_"); string checkSourceCodeNodeQuery = $"g.V('{sourceCodeNodeId}').hasLabel('microsoft.source/repository').has('isDeleted', false)";
                var sourceCodeNodeResults = await _graphDbClient.Query(checkSourceCodeNodeQuery);

                if (!sourceCodeNodeResults.Any())
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
                .project('subscriptionId', 'resourceGroupName', 'resourceType', 'resourceName', 'location')
                .by(coalesce(values('subscriptionId'), constant('unknown')))
                .by(coalesce(values('resourceGroupName'), constant('unknown')))
                .by(coalesce(values('resourceType'), constant('unknown')))
                .by(coalesce(values('resourceName'), constant('unknown')))
                .by(coalesce(values('location'), constant('unknown')))
                ";

            var results = await _graphDbClient.Query<Dictionary<string, object>>(query);

            return results.FirstOrDefault(new Dictionary<string, object>());
        }

        public async Task<Dictionary<string, object>> GetResourceDetailedProperties(string resourceId)
        {
            resourceId = Regex.Replace(resourceId, $"^{Regex.Escape(Constants.AzureManagementPrefix)}", "", RegexOptions.IgnoreCase);

            if (!ResourceIdentifier.TryParse(resourceId, out _))
            {
                throw new Exception("Invalid Azure resource Id, should be of form /subscriptions/<>/resourceGroups/<>/providers/<providerName>/<resourceType>/<resourceName>");
            }
            var query = $@"g.V('{CrawlerExtensions.GetSanitizedCosmosDBId(resourceId.ToLower())}').has('isDeleted', false).properties().as('p').group().by(select('p').key()).by(select('p').value())";

            var results = await _graphDbClient.Query<Dictionary<string, object>>(query);
            return results.FirstOrDefault(new Dictionary<string, object>());
        }

        public async Task<string> GetResourceIdForResourceName(string resourceName, string resourceType)
        {
            try
            {
                var query = $@"g.V().has(""resourceName"", ""{resourceName.ToLower()}"").has(""resourceType"", ""{resourceType.ToLower()}"").has('isDeleted', false).values('resourceId')";
                var results = await _graphDbClient.Query<string>(query);
                return results.FirstOrDefault("");
            }
            catch (RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
            {
                throw new ToolExecutionUnauthorizedException($"Unauthorized access to resource {resourceName}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving resource ID for {resourceName} of type {resourceType}: {ex.Message}", ex);
            }
        }
        #region Additional Methods
        /// <summary>
        /// Retrieves the resource dashboard screenshot and uses an LLM to summarize the general health.
        /// </summary>
        public async Task<string> GetGeneralHealthAsync(string resourceName, string resourceType)
        {
            try
            {
                if (string.IsNullOrEmpty(_dashboardSettings?.PrometheusUrl))
                {
                    return "Dashboard is not configured for this agent. Must use Knowledge graph queries. If user wants agent to deploy a dashboard they must configure Dashboard Settings.";
                }

                // Validate inputs
                if (string.IsNullOrEmpty(resourceName) || string.IsNullOrEmpty(resourceType))
                {
                    _logger.LogInternalWarning("Resource name or type cannot be empty");
                    return "Failed to generate health summary: Resource name or type not provided.";
                }

                // Normalize resource type to case-insensitive comparison
                resourceType = resourceType.ToLowerInvariant();

                // Check if the resource type is supported
                if (!_dashboardsToProcessByResourceType.TryGetValue(resourceType, out var dashboardType))
                {
                    _logger.LogInternalWarning("Unsupported resource type for dashboard: {ResourceType}", resourceType);
                    return $"Failed to generate health summary: Dashboard not available for resource type '{resourceType}'.";
                }

                // Look up the resource in our database/graph
                var resourceNodes = await SearchResourceAsync(resourceName, resourceType);
                if (resourceNodes == null || !resourceNodes.Any())
                {
                    _logger.LogInternalWarning("Resource not found: {ResourceName} of type {ResourceType}", resourceName, resourceType);
                    return $"Failed to generate health summary: Resource '{resourceName}' of type '{resourceType}' was not found in the knwoledge graph.";
                }

                // Use the first matching resource
                var resource = resourceNodes.First();

                // Generate dashboard URL based on resource type
                string baseUrl = $"{_grafanaUrl}/d/{dashboardType}";

                // Prepare query parameters based on resource type
                var queryParams = new Dictionary<string, string>
                {
                    { "var-ds", "azure-monitor-oob" },
                    { "var-ns", resourceType },
                    { "var-sub", resource.SubscriptionId ?? string.Empty },
                    { "var-rg", resource.ResourceGroupName?.ToLowerInvariant() ?? string.Empty },
                    { "var-resource", resource.ResourceName?.ToLowerInvariant() ?? string.Empty }
                };

                switch (dashboardType)
                {
                    case "azure-container-apps-container-app-view":
                        queryParams["var-containerapp"] = resource.ResourceName?.ToLowerInvariant() ?? string.Empty;
                        break;
                    case "azure-redis":
                        queryParams["var-name"] = resource.ResourceName?.ToLowerInvariant() ?? string.Empty;
                        break;
                    case "azure-app-service-monitoring":
                        queryParams["var-name"] = resource.ResourceName?.ToLowerInvariant() ?? string.Empty;
                        break;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                var grafanaAccessToken = await _authService.GetGrafanaAccessToken();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {grafanaAccessToken}");
                var dashboardResponse = await _httpClient.GetAsync($"{_grafanaUrl}/api/search?type=dash-db");
                dashboardResponse.EnsureSuccessStatusCode();
                var dashboardsContent = await dashboardResponse.Content.ReadAsStringAsync();
                var dashboards = JsonSerializer.Deserialize<JsonElement>(dashboardsContent);
                string dashboardUrl = string.Empty;
                foreach (var dashboard in dashboards.EnumerateArray())
                {
                    if (dashboard.TryGetProperty("url", out var urlElement))
                    {
                        var url = urlElement.GetString();
                        if (!string.IsNullOrEmpty(url) && url.Contains(dashboardType, StringComparison.OrdinalIgnoreCase))
                        {
                            dashboardUrl = url;
                            break;
                        }
                    }
                }


                if (string.IsNullOrEmpty(dashboardUrl))
                {
                    dashboardUrl = baseUrl;
                }

                // Build the final dashboard URL
                dashboardUrl = AddQueryParameters(dashboardUrl, queryParams);

                _logger.LogInternalInformation("Generated dashboard URL: {DashboardUrl} for resource {ResourceName}", dashboardUrl, resourceName);

                // Capture screenshot for the dashboard
                var screenshotResponse = await CaptureDashboardScreenshotAsync(dashboardUrl);
                if (string.IsNullOrEmpty(screenshotResponse?.Screenshot))
                {
                    _logger.LogInternalWarning("No screenshot captured for resource {ResourceName}", resourceName);
                    return $"Failed to capture dashboard screenshot for resource '{resourceName}'. Dashboard URL: {dashboardUrl}";
                }

                if (ThreadId != null)
                {
                    // TODO: read threadcontext from cosmos db
                    var dashboardMessage = $"![DailyReport Dashboard](data:image/png;base64,{screenshotResponse.Screenshot})\r";

                    Guid messageId = Guid.NewGuid();

                    // Save to database via the outbound service
                    await _agentOutboundCommunicationService.AppendAgentImageMessage(ThreadId.Value, dashboardMessage, messageId);

                    // Stream the complete image message directly to bypass tool call limitations
                    await _agentOutboundCommunicationService.AppendAgentStreamMessage(ThreadId.Value, dashboardMessage, StreamMessageType.Image, messageId);
                }

                string healthSummary = await SummarizeDashboardScreenshotAsync(screenshotResponse.Screenshot, dashboardUrl);
                _logger.LogInternalInformation("General health summary generated successfully for resource {ResourceName}.  Dashboard URL: {dashboardUrl}", resourceName);
                return $"Summary--------{healthSummary}------\nDashboard URL from where the summary was retrieved: {_grafanaUrl}{dashboardUrl}";
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error generating general health summary for resource {ResourceName} of type {ResourceType}", resourceName, resourceType);
                return $"Error generating health summary: {ex.Message}";
            }
        }

        public string GetKnowledgeGraphResourceUsageDashboard()
        {
            if (string.IsNullOrEmpty(_dashboardSettings?.PrometheusUrl))
            {
                return "Dashboard is not configured for this agent. Must use Knowledge graph queries. If user wants agent to deploy a dashboard they must configure Dashboard Settings.";
            }

            return $"Dashboard URL: {_dashboardSettings.GrafanaUrl}/d/{AgentNameHelper.GetMainDashboardUid(_hostEnvironment.IsProduction())}/sre-azure-resource-overview?orgId=1&refresh=1m";
        }

        public async Task<List<Dictionary<string, object>>> ListResourcesByTypeAsync(
            string resourceType, string propertyName, string propertyValue, int skip = 0, int take = 50)
        {
            string actualGraphResourceType = resourceType.ToLower();

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
                StringBuilder sb = new StringBuilder();
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
                    var propertyBag = new Dictionary<string, object>();
                    // Add label
                    propertyBag["subscriptionId"] = item?["subscriptionId"]?.ToString() ?? string.Empty;
                    propertyBag["resourceGroupName"] = item?["resourceGroupName"]?.ToString() ?? string.Empty;
                    propertyBag["resourceName"] = item?["resourceName"]?.ToString() ?? string.Empty;
                    propertyBag["resourceType"] = item?["resourceType"]?.ToString() ?? string.Empty;


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

        /// <summary>
        /// Searches for resources by a partial resource name and resource type.
        /// </summary>
        public async Task<List<ArmResourceNode>> SearchResourceAsync(string resourceName, string resourceType)
        {
            try
            {
                string modifiedResourceName = SanitizeInputForQuery(resourceName);
                string query = $@"
                g.V().hasLabel('{resourceType.ToLower()}').has('isDeleted', false)
                .where(values('resourceName').is(containing('{modifiedResourceName}')))
                .project('id', 'resourceName', 'resourceType', 'subscriptionId', 'resourceGroupName', 'location', 'resourceId')
                .by(id())
                .by(values('resourceName'))
                .by(label())
                .by(values('subscriptionId'))
                .by(values('resourceGroupName'))
                .by(coalesce(values('location'), constant('')))
                .by(values('resourceId'))
";

                var result = await _graphDbClient.Query(query);
                var resources = new List<ArmResourceNode>();

                foreach (var item in result)
                {
                    var node = new ArmResourceNode
                    {
                        ResourceName = item["resourceName"]?.ToString() ?? string.Empty,
                        ResourceType = item["resourceType"]?.ToString() ?? string.Empty,
                        SubscriptionId = item["subscriptionId"]?.ToString() ?? string.Empty,
                        ResourceGroupName = item["resourceGroupName"]?.ToString() ?? string.Empty,
                        Location = item["location"]?.ToString() ?? string.Empty,
                        ResourceId = item["resourceId"]?.ToString() ?? string.Empty
                    };

                    resources.Add(node);
                }

                return resources;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error searching for resource with name '{Name}' and type '{Type}'", resourceName, resourceType);
                throw;
            }
        }

        /// <summary>
        /// Searches for resources by a partial resource name.
        /// </summary>
        public async Task<dynamic> SearchResourceByNameAsync(string resourceName)
        {
            try
            {
                string modifiedResourceName = SanitizeInputForQuery(resourceName);
                string query = $@"
                g.V().has('isDeleted', false)
                .where(values('resourceName').is(containing('{modifiedResourceName}')))
                .project('id', 'resourceName', 'resourceType', 'subscriptionId', 'resourceGroupName', 'location', 'resourceId', 'namespace', 'clusterResourceId', 'group', 'apiVersion', 'kind', 'updateTs')
                .by(id())
                .by(values('resourceName'))
                .by(label())
                .by(coalesce(values('subscriptionId'), constant('')))
                .by(coalesce(values('resourceGroupName'), constant('')))
                .by(coalesce(values('location'), constant('')))
                .by(coalesce(values('resourceId'), constant('')))
                .by(coalesce(values('namespace'), constant('')))
                .by(coalesce(values('clusterResourceId'), constant('')))
                .by(coalesce(values('group'), constant('')))
                .by(coalesce(values('apiVersion'), constant('')))
                .by(coalesce(values('kind'), constant('')))
                .by(coalesce(values('updateTs'), constant(0)))
";

                // Use the raw result directly
                var result = await _graphDbClient.Query(query);

                // Create a composite key for each resource considering multiple dimensions
                // Resources are unique if they differ by any of: subscription, resource group, resource type,
                // kind, namespace, or cluster (for k8s resources)
                var uniqueResources = result
                    .GroupBy(item =>
                    {
                        string resourceName = item["resourceName"]?.ToString() ?? string.Empty;
                        string subscriptionId = item["subscriptionId"]?.ToString() ?? string.Empty;
                        string resourceGroupName = item["resourceGroupName"]?.ToString() ?? string.Empty;
                        string resourceType = item["resourceType"]?.ToString() ?? string.Empty;
                        string kind = item["kind"]?.ToString() ?? string.Empty;
                        string namespaceValue = item["namespace"]?.ToString() ?? string.Empty;
                        string clusterResourceId = item["clusterResourceId"]?.ToString() ?? string.Empty;

                        // Create a composite key from these properties
                        return $"{resourceName}|{subscriptionId}|{resourceGroupName}|{resourceType}|{kind}|{namespaceValue}|{clusterResourceId}";
                    })
                    .ToDictionary(
                        group => group.Key,
                        group => group.OrderByDescending(item =>
                           {
                               if (item["updateTs"] is long updateTsLong)
                               {
                                   return updateTsLong;
                               }
                               else if (item["updateTs"] is string updateTsString && long.TryParse(updateTsString, out long parsedValue))
                               {
                                   return parsedValue;
                               }
                               return 0;
                           }
                        ).First() // Take the one with latest updateTs
                    );

                // Convert to a more generic list that won't lose information
                var resources = new List<object>();

                // Use the filtered unique resources
                foreach (var item in uniqueResources.Values)
                {
                    // Determine if this is a Kubernetes resource by checking if the resourceType starts with "k8s/"
                    string resourceType = item["resourceType"]?.ToString() ?? string.Empty;
                    string namespaceValue = item["namespace"]?.ToString() ?? string.Empty;
                    string clusterResourceId = item["clusterResourceId"]?.ToString() ?? string.Empty;

                    // Check if this is a Kubernetes namespaced resource
                    if (resourceType.StartsWith("k8s/") && !string.IsNullOrEmpty(namespaceValue) && !string.IsNullOrEmpty(clusterResourceId))
                    {
                        var k8sNode = new KubernetesNamespacedResourceNode(
                            null,
                            clusterResourceId,
                            namespaceValue,
                            item["subscriptionId"]?.ToString() ?? string.Empty,
                            item["resourceGroupName"]?.ToString() ?? string.Empty,
                             item["location"]?.ToString() ?? string.Empty,
                            item["resourceName"]?.ToString() ?? string.Empty,
                            item["group"]?.ToString() ?? string.Empty,
                            item["apiVersion"]?.ToString() ?? string.Empty,
                            item["kind"]?.ToString() ?? string.Empty
                        );

                        k8sNode.Location = item["location"]?.ToString() ?? string.Empty;

                        resources.Add(k8sNode);
                    }
                    else
                    {
                        var node = new ArmResourceNode
                        {
                            ResourceName = item["resourceName"]?.ToString() ?? string.Empty,
                            ResourceType = item["resourceType"]?.ToString() ?? string.Empty,
                            SubscriptionId = item["subscriptionId"]?.ToString() ?? string.Empty,
                            ResourceGroupName = item["resourceGroupName"]?.ToString() ?? string.Empty,
                            Location = item["location"]?.ToString() ?? string.Empty,
                            ResourceId = item["resourceId"]?.ToString() ?? string.Empty
                        };

                        resources.Add(node);
                    }
                }

                // Return as dynamic to preserve all data
                return resources;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error searching for resource with name '{Name}'", resourceName);
                throw;
            }
        }

        private async Task<dynamic> GetAllResourceCountAsync()
        {
            try
            {
                string resourceIdFilter = string.Join(",", _crawlRoots.Select(r => $@"has('resourceId', startingWith('{r.ToLower()}'))"));
                string query = !string.IsNullOrEmpty(resourceIdFilter) ? $@"g.V().has('isDeleted', false).or({resourceIdFilter}).groupCount().by(label())" : $@"g.V().has('isDeleted', false).groupCount().by(label())";
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
                // todo: do we need to show k8s resources in the count?
                var counts = result.First().Where(kvp => kvp.Key.StartsWith("microsoft") && !excludedResourceTypes.Contains(kvp.Key) && kvp.Key.Count(c => c == '/') == 1).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                return counts;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error getting all resource count");
                throw;
            }
        }

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
                {                    // Simple count query when no groupBy is specified
                    query = $@"
                g.V().hasLabel('{resourceType.ToLower()}').has('isDeleted', false)
                .count()
            ";

                    var result = await _graphDbClient.Query(query);

                    long count = 0;
                    if (result != null && result.Count > 0)
                    {
                        // Convert the first result to long
                        count = Convert.ToInt64(result.First());
                        _logger.LogInternalInformation("Found {Count} resources of type '{Type}'", count, resourceType);
                    }

                    return new { ResourceType = resourceType, Count = count };
                }
                else
                {                    // todo: this query linearly lists all resources of the type and manually groups them by the property, which could be slow
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
                            string groupValue = "Unknown";

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

                // Combine all resource types for processing
                var allResourceTypes = azureResourceTypes.Concat(otherResourceTypes).ToList();

                // Create and execute tasks for getting counts of each resource type
                var resourceTypeTasks = new List<Task<dynamic>>();
                foreach (var resourceType in allResourceTypes)
                {
                    resourceTypeTasks.Add(GetResourceCountAsync(resourceType));
                }
                var results = await Task.WhenAll(resourceTypeTasks);

                // Process results
                var managedResources = new Dictionary<string, long>();
                var otherResources = new Dictionary<string, long>();

                foreach (var result in results)
                {
                    string resourceType = result.ResourceType.ToString();
                    long count = result.Count;

                    if (count > 0)
                    {
                        // Extract a more readable name from the full resource type
                        string simpleName = resourceType.Split('/').Last();

                        // Add to the appropriate dictionary based on resource type
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

                // Calculate totals
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

        /// <summary>
        /// Returns a list of subscription IDs by querying all vertices that have a 'subscriptionId' property.
        /// </summary>
        public async Task<List<dynamic>> ListSubscriptionsAsync()
        {
            try
            {
                string query = $@"g.V().has('resourceType', '{SubscriptionNode.Type}').has('isDeleted', false)
                          .project('name', 'id')
                          .by('subscriptionName')
                          .by('subscriptionId')";

                var result = await _graphDbClient.Query(query);

                _logger.LogInternalInformation("Found {Count} subscriptions", result.Count);

                // Return the list of subscription objects with name and id properties intact
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error listing subscriptions");
                throw;
            }
        }



        public async Task<List<Dictionary<string, object>>> ListResourceGroupsAsync(string subscriptionId)
        {
            try
            {                // Query with the correct lowercase 'resourcegroups' type
                string query = $@"g.V().has('resourceType', 'resourcegroups').has('isDeleted', false)
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
                    var propertyBag = new Dictionary<string, object>();
                    propertyBag["subscriptionId"] = item["subscriptionId"]?.ToString() ?? string.Empty;
                    propertyBag["resourceGroupName"] = item["resourceGroupName"]?.ToString() ?? string.Empty;
                    propertyBag["resourceType"] = item["resourceType"]?.ToString() ?? string.Empty;
                    propertyBag["resourceId"] = item["resourceId"]?.ToString() ?? string.Empty;
                    propertyBag["location"] = item["location"]?.ToString() ?? string.Empty;

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

        public async Task<(List<Dictionary<string, object>> ActivityLogs, List<Node> Components)> FetchActivityLogsAndComponents(string resourceId, int hoursBack = 24, Guid? threadId = null)
        {
            _logger.LogInternalInformation($"[FetchActivityLogs] Invoked with resourceId: {resourceId}");

            if (string.IsNullOrWhiteSpace(resourceId))
            {
                throw new ArgumentException("Resource ID cannot be null or empty.", nameof(resourceId));
            }

            try
            {
                var resourceIdentifier = new ResourceIdentifier(resourceId);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Invalid Azure resource ID format: {ex.Message}",
                    nameof(resourceId));
            }

            if (threadId == null)
            {
                if (ThreadId != null)
                {
                    threadId = ThreadId;
                }
            }

            int maxRetries = 3;
            int retryDelayMilliseconds = 1000; // 1 second

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var components = await GetApplicationComponentsSummary(resourceId, 3);

                    if (components.Count == 0)
                    {
                        _logger.LogInternalWarning($"No components found for resourceId: {resourceId}");
                        throw new ArgumentException($"No components found for resourceId: {resourceId}. Was the correct resource ID provided? Alternatively, the Knowledge Graph may not have been built for this component.");
                    }

                    var resourceIds = components.Select(c => c.Id).ToList();
                    var rgs = ExtractUniqueResourceGroups(resourceIds);
                    _logger.LogInternalInformation($"Found {resourceIds.Count} related resources for activity log analysis");

                    var allActivityLogs = new List<Dictionary<string, object>>();

                    foreach (var id in rgs)
                    {
                        var logs = await FetchActivityLogsForResource(id.Replace("_", "/"), hoursBack);
                        var distinctLogs = logs
                            .Where(l => l != null
                                        && l.ContainsKey("operationName")
                                        && l["operationName"]?.ToString()?.Contains("write", StringComparison.OrdinalIgnoreCase) == true)
                            .ToList();

                        var distinct = logs.Select(l => l["operationName"].ToString()).Distinct().ToList();

                        if (logs.Count > 0)
                        {
                            allActivityLogs.AddRange(logs);
                        }
                        _logger.LogInternalInformation($"Fetched {logs.Count} activity logs for resource {id}");
                    }

                    if (allActivityLogs.Count == 0)
                    {
                        throw new ArgumentException("No activity logs found for the specified resource and its dependencies in the last " + hoursBack + " hours.");
                    }

                    allActivityLogs = allActivityLogs
                     .OrderByDescending(log =>
                     {
                         if (log != null && log.TryGetValue("eventTimestamp", out var timestampValue) && timestampValue != null)
                         {
                             if (DateTimeOffset.TryParse(timestampValue.ToString(), out var timestamp))
                             {
                                 return timestamp;
                             }
                         }
                         return DateTimeOffset.MinValue;
                     })
                     .ToList();

                    _logger.LogInternalInformation($"Total activity logs collected: {allActivityLogs.Count}");
                    return (allActivityLogs, components);
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogInternalWarning($"[FetchAndSummarizeActivityLogs] Attempt {attempt} failed with error: {ex.Message}. Retrying in {retryDelayMilliseconds}ms...");
                        await Task.Delay(retryDelayMilliseconds);
                    }
                    else
                    {
                        _logger.LogInternalError($"[FetchAndSummarizeActivityLogs] All {maxRetries} attempts failed. Last error: {ex.Message}");
                        throw;
                    }
                }
            }

            throw new ArgumentException("Error: Unexpected execution path in fetching and summarizing activity logs");
        }


        public HashSet<string> ExtractUniqueResourceGroups(IEnumerable<string> resourceIds)
        {
            var resourceGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var resourceId in resourceIds)
            {
                // Convert underscores back to slashes if they were replaced
                string normalizedId = resourceId.Replace("_", "/");

                // Split the path into segments
                string[] segments = normalizedId.Split('/');

                // Look for the subscription and resourceGroups in the segments
                for (int i = 0; i < segments.Length - 3; i++)
                {
                    if (string.Equals(segments[i], "subscriptions", StringComparison.OrdinalIgnoreCase) &&
                        i + 2 < segments.Length &&
                        string.Equals(segments[i + 2], "resourceGroups", StringComparison.OrdinalIgnoreCase) &&
                        i + 3 < segments.Length)
                    {
                        // We found the pattern /subscriptions/{subId}/resourceGroups/{rgName}
                        // Build the path: /subscriptions/{subId}/resourceGroups/{rgName}
                        string path = $"/subscriptions/{segments[i + 1]}/resourceGroups/{segments[i + 3]}";
                        resourceGroups.Add(path);
                        break; // Once we find the pattern in this resource ID, we can move to the next one
                    }
                }
            }

            return resourceGroups;
        }

        /// <summary>
        /// Retrieves the application health information for a specific resource from the graph database.
        /// This provides metrics like availability, CPU usage, memory usage, latency, and overall health state.
        /// </summary>
        /// <param name="resourceId">Azure Resource Id of the resource to get health information for.</param>
        /// <returns>A AppHealthInfo object containing health metrics, or null if the resource doesn't have health data.</returns>
        public async Task<string> GetApplicationHealthInfoAsync(string resourceId)
        {
            try
            {
                string sanitizedResourceId = SanitizeInputForQuery(resourceId.ToLowerInvariant());

                var resourceID = new ResourceIdentifier(sanitizedResourceId);

                string query = $"g.V().has('resourceId', '{sanitizedResourceId}').has('isDeleted', false).project('properties').by(valueMap()).by(unfold())";

                var result = await _graphDbClient.Query<dynamic>(query);

                if (result == null || !result.Any())
                {
                    string message = $"Resource with ID {resourceId} not found in graph database.";
                    _logger.LogInternalWarning(message);
                    return message;
                }

                var resource = result.First();
                var properties = resource["properties"];

                if (properties.ContainsKey("appHealthInfo"))
                {
                    string appHealthInfoJson = properties["appHealthInfo"].ToString();

                    // Deserialize the JSON string to AppHealthInfo object
                    AppHealthInfo? healthInfo = JsonSerializer.Deserialize<AppHealthInfo>(appHealthInfoJson);

                    return FormatHealthInfoMessage(healthInfo, resourceId);
                }

                _logger.LogInternalInformation($"Resource with ID {resourceId} does not have health information.");
                return $"Resource with ID {resourceId} does not have health information.";
            }
            catch (FormatException ex)
            {
                _logger.LogInternalWarning($"Invalid resource id : {resourceId}, exception : {ex}");
                return $"Invalid resource id : {resourceId}";
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error retrieving health information for resource {resourceId}: {ex.Message}");
                return $"Error retrieving health information for resource {resourceId}: {ex.Message}";
            }
        }

        private async Task<List<Dictionary<string, object>>> FetchActivityLogsForResource(string resourceId, int hoursBack)
        {
            _logger.LogInternalInformation($"[FetchActivityLogsForResource] Fetching activity logs for: {resourceId}");

            try
            {
                var resourceIdentifier = new ResourceIdentifier(resourceId);
                var subscriptionId = resourceIdentifier.SubscriptionId;
                var resourceGroupName = resourceIdentifier.ResourceGroupName;

                var credential = await _authService.GetArmOperationCredential();
                var defaultToken = credential.GetToken(new TokenRequestContext(["https://management.azure.com/.default"]), CancellationToken.None).Token;
                var defaultTokenCredentials = new TokenCredentials(defaultToken);
                var azureCredentials = new Microsoft.Azure.Management.ResourceManager.Fluent.Authentication.AzureCredentials(defaultTokenCredentials, defaultTokenCredentials, null, AzureEnvironment.AzureGlobalCloud);

                var restClient = RestClient.Configure()
                    .WithBaseUri("https://management.azure.com")
                    .WithCredentials(azureCredentials)
                    .Build();

                var monitorClient = new MonitorManagementClient(restClient)
                {
                    SubscriptionId = subscriptionId
                };

                var startTime = DateTime.UtcNow.AddHours(-hoursBack);
                var endTime = DateTime.UtcNow;

                var filter = new ODataQuery<EventData>(eventData =>
                    eventData.EventTimestamp >= startTime &&
                    eventData.EventTimestamp <= endTime &&
                    eventData.ResourceGroupName == resourceGroupName);

                var filterString = $"eventTimestamp ge {startTime:yyyy-MM-ddTHH:mm:ssZ} and " +
                  $"eventTimestamp le {endTime:yyyy-MM-ddTHH:mm:ssZ} and " +
                  $"eventChannels eq 'Admin, Operation' and " +
                  $"resourceGroupName eq '{resourceGroupName}'";

                var odataQuery = new ODataQuery<EventData>(filterString);

                IPage<EventData> eventsPage = await monitorClient.ActivityLogs.ListAsync(
                    odataQuery: odataQuery,
                    cancellationToken: default);

                var logs = new List<Dictionary<string, object>>();

                do
                {
                    foreach (var eventData in eventsPage)
                    {
                        // Filter out GET operations and routine read activities
                        var operationName = eventData?.OperationName?.Value ?? string.Empty;
                        if (ShouldSkipOperation(operationName))
                        {
                            continue;
                        }

                        var log = new Dictionary<string, object>
                        {
                            ["eventTimestamp"] = eventData?.EventTimestamp?.ToString("o") ?? string.Empty,
                            ["operationName"] = operationName,
                            ["caller"] = eventData?.Caller ?? string.Empty,
                            ["status"] = eventData?.Status?.Value ?? string.Empty,
                            ["correlationId"] = eventData?.CorrelationId ?? string.Empty,
                            ["eventTimestamp"] = eventData?.EventTimestamp?.ToString("o") ?? string.Empty,
                            ["level"] = eventData?.Level?.ToString() ?? string.Empty
                        };

                        // Only add caller IP for failed operations or deployments
                        if (eventData?.HttpRequest != null &&
                            (eventData.Status?.Value?.Contains("Failed") == true ||
                             operationName.Contains("deployment", StringComparison.OrdinalIgnoreCase)))
                        {
                            log["callerIpAddress"] = eventData.HttpRequest.ClientIpAddress;
                        }

                        // Only add authorization for security-relevant operations
                        if (eventData?.Authorization != null && IsSecurityRelevantOperation(operationName))
                        {
                            log["authorizationAction"] = eventData.Authorization.Action;
                        }

                        // Only include essential properties for failed operations or deployments
                        if (eventData?.Properties != null &&
                            (eventData.Status?.Value?.Contains("Failed") == true ||
                             operationName.Contains("deployment", StringComparison.OrdinalIgnoreCase)))
                        {
                            var filteredProperties = FilterEssentialProperties(eventData.Properties);
                            if (filteredProperties.Any())
                            {
                                log["properties"] = JsonSerializer.Serialize(filteredProperties);
                            }
                        }

                        logs.Add(log);
                    }

                    // Get next page if available
                    if (eventsPage.NextPageLink != null)
                    {
                        eventsPage = await monitorClient.ActivityLogs.ListNextAsync(eventsPage.NextPageLink);
                    }
                    else
                    {
                        break;
                    }
                } while (true);

                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error fetching activity logs for {resourceId}");
                return new List<Dictionary<string, object>>();
            }
        }

        private string SanitizeInputForQuery(string input)
        {
            // Sanitize the input to prevent injection attacks
            return input.Replace("'", "''").Replace(".", "").Replace("//", "");
        }

        private bool ShouldSkipOperation(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return true;

            var operationLower = operationName.ToLowerInvariant();

            // Skip GET operations and other read-only activities
            var skipPatterns = new[]
            {
                "/read",
                "/get",
                "/list",
                "/query",
                "/search",
                "/browse",
                "/validate",
                "/check",
                "microsoft.insights/logs/read",
                "microsoft.insights/metrics/read",
                "microsoft.authorization/permissions/read",
                "microsoft.authorization/roleassignments/read",
                "microsoft.resources/subscriptions/read",
                "microsoft.resources/resourcegroups/read"
            };

            return skipPatterns.Any(pattern => operationLower.Contains(pattern));
        }

        private bool IsSecurityRelevantOperation(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                return false;

            var operationLower = operationName.ToLowerInvariant();

            // Include authorization for security-relevant operations
            var securityPatterns = new[]
            {
                "roleassignment",
                "policy",
                "permission",
                "authentication",
                "authorization",
                "security",
                "delete",
                "microsoft.keyvault",
                "microsoft.authorization"
            };

            return securityPatterns.Any(pattern => operationLower.Contains(pattern));
        }

        private Dictionary<string, object> FilterEssentialProperties(IDictionary<string, string> properties)
        {
            var essentialProperties = new Dictionary<string, object>();

            // Only include properties that are useful for troubleshooting and change tracking
            var essentialKeys = new[]
            {
                "statusCode",
                "statusMessage",
                "errorCode",
                "errorMessage",
                "failureReason",
                "correlationId",
                "trackingId",
                "deploymentName",
                "templateHash",
                "resourceName",
                "provisioningState",
                "duration",
                // Additional properties for change tracking
                "changeType",
                "changedProperties",
                "beforeSnapshot",
                "afterSnapshot",
                "configurationChanges",
                "resourceChanges",
                "deploymentParameters",
                "templateParameters",
                "outputResources",
                "modifiedBy",
                "changeDescription",
                "changeReason",
                "policyViolation",
                "complianceState"
            };

            foreach (var key in essentialKeys)
            {
                if (properties.ContainsKey(key) && properties[key] != null)
                {
                    essentialProperties[key] = properties[key];
                }
            }

            return essentialProperties;
        }



        #endregion

        #region Dummy Screenshot & LLM Summary Methods

        private async Task<ScreenshotResponse?> CaptureDashboardScreenshotAsync(string dashboardUrl)
        {
            try
            {
                var payload = new
                {
                    grafanaEndpoint = _grafanaUrl,
                    grafanaToken = await _authService.GetGrafanaAccessToken(),
                    dashboardUrl
                };

                string requestJson = JsonSerializer.Serialize(payload);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                _logger.LogInternalInformation("Requesting screenshot for dashboard: {DashboardUrl}", dashboardUrl);

                var response = await _httpClient.PostAsync($"{_puppeteerScreenshotApiUrl}/screenshot", content);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                var screenshotResponse = JsonSerializer.Deserialize<ScreenshotResponse>(responseJson);

                _logger.LogInternalInformation("Screenshot captured successfully for dashboard: {DashboardUrl}", dashboardUrl);
                return screenshotResponse;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error capturing dashboard screenshot for {DashboardUrl}", dashboardUrl);
                return new ScreenshotResponse { Screenshot = string.Empty };
            }
        }

        private string AddQueryParameters(string url, Dictionary<string, string> parameters)
        {
            // Return the original URL if there are no parameters to add
            if (string.IsNullOrEmpty(url) || parameters == null || parameters.Count == 0)
            {
                return url;
            }

            // Convert dictionary to a query string with URL-encoded parameters
            var queryString = string.Join("&", parameters
                .Select(param => $"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}"));

            // Determine the correct separator: ? if no query exists, otherwise use &
            char separator = url.Contains("?") ? '&' : '?';

            // Append the query string to the URL
            return $"{url}{separator}{queryString}";
        }

        private async Task<string> SummarizeDashboardScreenshotAsync(string base64Screenshot, string dashboardUrl)
        {
            try
            {
                var messages = new List<ChatMessage>();
                var prompt = $"Please analyze the dashboard screenshot for the dashboard at {dashboardUrl}. " +
                             $"Based on the visual data, provide a concise summary of the resource's health and any notable observations.";

                _logger.LogInternalInformation("Sending screenshot data to LLM for summarization for dashboard: {DashboardUrl}", dashboardUrl);
                messages.Add(new ChatMessage(ChatRole.System, prompt));
                var screenshot = Convert.FromBase64String(base64Screenshot);
                var content = new List<AIContent>
                    {
                        new Microsoft.Extensions.AI.TextContent($"This is a screenshot of the dashboard: {dashboardUrl}"),
                        new DataContent(screenshot, "image/png")
                    };
                messages.Add(new ChatMessage(ChatRole.User, content));

                var options = new ChatOptions
                {
                    Temperature = (float)0.2,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["response_format"] = "text"
                    }
                };

                var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(messages, options);
                string summary = response.Text;

                _logger.LogInternalInformation("Dashboard summary generated successfully for {DashboardUrl}", dashboardUrl);
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error summarizing dashboard screenshot for {DashboardUrl}", dashboardUrl);
                return $"Error summarizing dashboard screenshot: {ex.Message}";
            }
        }

        private string FormatHealthInfoMessage(AppHealthInfo? healthInfo, string resourceId)
        {
            var resourceName = resourceId?.Split('/')?.Last() ?? "Unknown";
            var sb = new StringBuilder();

            sb.AppendLine($"## Health Scorecard for {resourceName}");
            sb.AppendLine();

            // Overall health state
            sb.AppendLine($"**Overall Health State**: {GetHealthStateEmoji(healthInfo?.Health)} {healthInfo?.Health}");
            sb.AppendLine();

            // Key metrics section
            sb.AppendLine("### Key Metrics");

            if (healthInfo?.Availability.HasValue == true)
            {
                sb.AppendLine($"- **Availability**: {healthInfo.Availability.Value:P2}");
            }

            if (healthInfo?.AvgCpuUsage.HasValue == true)
            {
                sb.AppendLine($"- **CPU Usage**: {healthInfo.AvgCpuUsage.Value:P2}");
            }

            if (healthInfo?.AvgMemoryUsage.HasValue == true)
            {
                sb.AppendLine($"- **Memory Usage**: {healthInfo.AvgMemoryUsage.Value:P2}");
            }

            if (healthInfo?.AvgLatencyInMs.HasValue == true)
            {
                sb.AppendLine($"- **Average Latency**: {healthInfo.AvgLatencyInMs.Value:N2} ms");
            }

            if (healthInfo?.Transactions.HasValue == true)
            {
                sb.AppendLine($"- **Transaction Volume**: {healthInfo.Transactions.Value:N0} requests");
            }

            // Activity status
            sb.AppendLine();
            sb.AppendLine($"**Activity Status**: {(healthInfo?.IsActive == true ? "🟢 Active" : "🔴 Inactive")}");

            if (healthInfo?.TimeSinceLastActivity.HasValue == true)
            {
                var timeSince = DateTime.UtcNow - healthInfo.TimeSinceLastActivity.Value;
                sb.AppendLine($"**Last Activity**: {FormatTimeSpan(timeSince)} ago");
            }

            sb.AppendLine();

            string lastUpdated = healthInfo != null
                 ? healthInfo.LastDataCaptureTimeStampInUTC.ToString("yyyy-MM-dd HH:mm:ss")
                 : "Unknown";

            sb.AppendLine($"*Last updated: {lastUpdated} UTC*");

            return sb.ToString();
        }


        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays > 1)
            {
                return $"{timeSpan.TotalDays:N1} days";
            }
            else if (timeSpan.TotalHours > 1)
            {
                return $"{timeSpan.TotalHours:N1} hours";
            }
            else if (timeSpan.TotalMinutes > 1)
            {
                return $"{timeSpan.TotalMinutes:N1} minutes";
            }
            else
            {
                return $"{timeSpan.TotalSeconds:N0} seconds";
            }
        }

        private string GetHealthStateEmoji(ScorecardHealthState? healthState)
        {
            return healthState switch
            {
                ScorecardHealthState.Healthy => "✅",
                ScorecardHealthState.Degraded => "⚠️",
                ScorecardHealthState.Unhealthy => "❌",
                _ => "❓"
            };
        }


        private async Task<List<ChangeRecord>> FetchDiffsFromARGByWindowAsync(
    DateTime anchorUtc, string subscriptionId, string? resourceGroupHintOrResourceId, CancellationToken ct = default)
        {
            var startUtc = anchorUtc.AddMinutes(-30).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
            var endUtc = anchorUtc.AddMinutes(+30).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

            var armCred = await _authService.GetArmOperationCredential();
            var token = armCred.GetToken(
                new TokenRequestContext(["https://management.azure.com/.default"]), ct).Token;

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Optional narrowing: try to filter results to RG/name hints to avoid noise
            var nameHint = resourceGroupHintOrResourceId ?? string.Empty;

            var kql = $@"
let start_t = datetime({startUtc});
let end_t   = datetime({endUtc});
resourcechanges
| extend changeTime = todatetime(properties.changeAttributes.timestamp),
         targetResourceId = tostring(properties.targetResourceId)
| where changeTime between (start_t .. end_t)
| where isempty('{nameHint}') or targetResourceId has '{nameHint}'
| extend changeType = tostring(properties.changeType),
         changedBy = tostring(properties.changeAttributes.changedBy),
         clientType = tostring(properties.changeAttributes.clientType),
         prevSnap = tostring(properties.changeAttributes.previousResourceSnapshotId),
         newSnap = tostring(properties.changeAttributes.newResourceSnapshotId),
         changes = properties.changes
| project changeTime, targetResourceId, changeType, changedBy, clientType, changes, prevSnap, newSnap
| order by changeTime asc";

            var payload = new
            {
                subscriptions = new[] { subscriptionId },
                query = kql,
                options = new { resultFormat = "objectArray" }
            };

            var resp = await http.PostAsync(
                "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                ct);

            resp.EnsureSuccessStatusCode();

            using var s = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct);

            var list = new List<ChangeRecord>();
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var row in data.EnumerateArray())
            {
                list.Add(new ChangeRecord
                {
                    ChangeTime = row.GetProperty("changeTime").GetDateTime(),
                    TargetResourceId = row.GetProperty("targetResourceId").GetString() ?? "",
                    ChangeType = row.GetProperty("changeType").GetString() ?? "",
                    ChangedBy = row.GetProperty("changedBy").GetString() ?? "",
                    ClientType = row.GetProperty("clientType").GetString() ?? "",
                    ChangesJson = row.GetProperty("changes").GetRawText(),
                    PreviousSnapshotId = row.GetProperty("prevSnap").GetString(),
                    NewSnapshotId = row.GetProperty("newSnap").GetString()
                });
            }

            return list;
        }

        private static string FormatDiffsForPrompt(IEnumerable<ChangeRecord> diffs, int maxRows = 50)
        {
            // Keep it compact for the model; you can switch to a real markdown table if you prefer.
            var sb = new StringBuilder();
            int i = 0;
            foreach (var d in diffs.OrderBy(x => x.ChangeTime))
            {
                if (i++ >= maxRows) break;
                sb.AppendLine($"- [{d.ChangeTime:O}] {d.TargetResourceId} ({d.ChangeType}) by {d.ChangedBy} via {d.ClientType}");
                sb.AppendLine($"  changes: {d.ChangesJson}");
                if (!string.IsNullOrEmpty(d.PreviousSnapshotId) || !string.IsNullOrEmpty(d.NewSnapshotId))
                    sb.AppendLine($"  snapshots: prev={d.PreviousSnapshotId}, new={d.NewSnapshotId}");
            }
            return sb.ToString();
        }

        private async Task<string> AnalyzeChangeHistoryWithLLM_Extended(
            List<Dictionary<string, object>> changeHistory,
            Dictionary<string, object>? deploymentDetails,
            List<Dictionary<string, object>> resourceChanges,
            List<ChangeRecord> diffs,
            string correlationId,
            string resourceId)
        {
            var changeHistoryJson = JsonSerializer.Serialize(changeHistory, new JsonSerializerOptions { WriteIndented = true });
            var deploymentDetailsJson = deploymentDetails != null
                ? JsonSerializer.Serialize(deploymentDetails, new JsonSerializerOptions { WriteIndented = true })
                : "No deployment details available";
            var resourceChangesJson = JsonSerializer.Serialize(resourceChanges, new JsonSerializerOptions { WriteIndented = true });
            var diffsCompact = FormatDiffsForPrompt(diffs);

            var prompt = $@"
You are an Azure change analysis expert.

Correlation ID: {correlationId}
Resource ID: {resourceId}

Use **Change Analysis (property-level diffs)** as the source of truth.
Provide:
1) Change summary (what changed, when, and who)
2) Operation timeline (from Activity Log)
3) Property-level BEFORE → AFTER highlights (from diffs)
4) Impact analysis (what resources were affected)
5) Validation & issues
6) Rollback/mitigation pointers

=== DIFFS (from Change Analysis) ===
{diffsCompact}

=== ACTIVITY LOG (context) ===
{changeHistoryJson}

=== DEPLOYMENT DETAILS ===
{deploymentDetailsJson}

=== OTHER RESOURCE CHANGES (deployment ops) ===
{resourceChangesJson}

Respond in a concise, structured format with bullet points and short sentences.";
            var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(prompt);
            return response.Text;
        }

        private async Task<List<Dictionary<string, object>>> FetchChangeHistoryByCorrelationId(
    string correlationId, string subscriptionId, string resourceGroupName)
        {
            try
            {
                // Build Track1 RestClient with ARM token
                var armCred = await _authService.GetArmOperationCredential();
                var token = armCred.GetToken(
                    new TokenRequestContext(["https://management.azure.com/.default"]),
                    CancellationToken.None).Token;

                var tokenCreds = new TokenCredentials(token);
                var azureCreds = new Microsoft.Azure.Management.ResourceManager.Fluent.Authentication.AzureCredentials(
                    tokenCreds, tokenCreds, null, AzureEnvironment.AzureGlobalCloud);

                var restClient = RestClient.Configure()
                    .WithBaseUri("https://management.azure.com")
                    .WithCredentials(azureCreds)
                    .Build();

                var monitorClient = new MonitorManagementClient(restClient)
                {
                    SubscriptionId = subscriptionId
                };

                var endTime = DateTimeOffset.UtcNow;
                var startTime = endTime.AddDays(-30);

                // Escape single quotes for OData
                string corr = correlationId.Replace("'", "''");
                string rg = resourceGroupName.Replace("'", "''");

                // Narrow to Admin/Operation channel + RG for performance
                const string isoZ = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
                var startUtc = startTime.UtcDateTime.ToString(isoZ, CultureInfo.InvariantCulture);
                var endUtc = endTime.UtcDateTime.ToString(isoZ, CultureInfo.InvariantCulture);

                // Allowed pattern: time window + correlationId (nothing else)
                var filterString =
                    $"eventTimestamp ge '{startUtc}' and " +
                    $"eventTimestamp le '{endUtc}' and " +
                    $"correlationId eq '{corr}'";

                var odataQuery = new ODataQuery<EventData>(filterString);

                var changeEvents = new List<Dictionary<string, object>>();

                IPage<EventData> page = await monitorClient.ActivityLogs.ListAsync(
                    odataQuery: odataQuery, cancellationToken: default);

                while (true)
                {
                    foreach (var e in page)
                    {
                        if (!string.Equals(e?.ResourceGroupName, resourceGroupName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var evt = new Dictionary<string, object>
                        {
                            ["eventTimestamp"] = e?.EventTimestamp?.ToString("o") ?? string.Empty,
                            ["operationName"] = e?.OperationName?.Value ?? string.Empty,
                            ["caller"] = e?.Caller ?? string.Empty,
                            ["status"] = e?.Status?.Value ?? string.Empty,
                            ["correlationId"] = e?.CorrelationId ?? string.Empty,
                            ["level"] = e?.Level?.ToString() ?? string.Empty,
                            ["resourceId"] = e?.ResourceId ?? string.Empty,
                            ["resourceGroup"] = e?.ResourceGroupName ?? string.Empty,
                            ["description"] = e?.Description ?? string.Empty,
                            ["eventName"] = e?.EventName?.Value ?? string.Empty,
                            ["category"] = e?.Category?.Value ?? string.Empty
                        };

                        if (e?.Properties != null && e.Properties.Count > 0)
                            evt["properties"] = JsonSerializer.Serialize(e.Properties);

                        if (e?.Authorization != null)
                            evt["authorization"] = new Dictionary<string, object>
                            {
                                ["action"] = e.Authorization.Action ?? string.Empty,
                                ["role"] = e.Authorization.Role ?? string.Empty,
                                ["scope"] = e.Authorization.Scope ?? string.Empty
                            };

                        // Add IP only when useful
                        var isFailed = (e?.Status?.Value ?? "").IndexOf("Failed", StringComparison.OrdinalIgnoreCase) >= 0;
                        var isDeploy = (e?.OperationName?.Value ?? "").IndexOf("deployment", StringComparison.OrdinalIgnoreCase) >= 0;
                        if ((isFailed || isDeploy) && e?.HttpRequest?.ClientIpAddress != null)
                            evt["callerIpAddress"] = e.HttpRequest.ClientIpAddress;

                        changeEvents.Add(evt);
                    }

                    if (string.IsNullOrEmpty(page?.NextPageLink)) break;
                    page = await monitorClient.ActivityLogs.ListNextAsync(page.NextPageLink);
                }

                return changeEvents
                    .OrderBy(e =>
                    {
                        if (e.TryGetValue("eventTimestamp", out var v) &&
                            DateTimeOffset.TryParse(v?.ToString(), out var dto))
                            return dto;
                        return DateTimeOffset.MinValue;
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error fetching change history by correlation ID {correlationId}");
                return new List<Dictionary<string, object>>();
            }
        }

        private async Task<Dictionary<string, object>?> FetchDeploymentDetails(string correlationId, string subscriptionId, string resourceGroupName)
        {
            try
            {
                var resourceManagementClient = await CreateResourceClientAsync(subscriptionId);
                // List recent deployments and find one matching the correlation ID
                var deployments = await resourceManagementClient.Deployments.ListByResourceGroupAsync(resourceGroupName);

                foreach (var deployment in deployments)
                {
                    if (deployment?.Properties?.CorrelationId == correlationId)
                    {
                        return new Dictionary<string, object>
                        {
                            ["deploymentName"] = deployment.Name ?? string.Empty,
                            ["provisioningState"] = deployment.Properties?.ProvisioningState?.ToString() ?? string.Empty,
                            ["timestamp"] = deployment.Properties?.Timestamp?.ToString("o") ?? string.Empty,
                            ["mode"] = deployment.Properties?.Mode?.ToString() ?? string.Empty,
                            ["templateHash"] = deployment.Properties?.TemplateHash ?? string.Empty,
                            ["parameters"] = deployment.Properties?.Parameters != null ?
                                JsonSerializer.Serialize(deployment.Properties.Parameters) : string.Empty,
                            ["outputs"] = deployment.Properties?.Outputs != null ?
                                JsonSerializer.Serialize(deployment.Properties.Outputs) : string.Empty,
                            ["error"] = deployment.Properties?.Error != null ?
                                JsonSerializer.Serialize(deployment.Properties.Error) : string.Empty
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Could not fetch deployment details for correlation ID {correlationId}");
                return null;
            }
        }

        private async Task<List<Dictionary<string, object>>> FetchResourceChanges(string correlationId, string resourceId)
        {
            try
            {
                var ri = new ResourceIdentifier(resourceId);
                var subscriptionId = ri.SubscriptionId;
                var resourceGroupName = ri.ResourceGroupName;

                if (string.IsNullOrEmpty(subscriptionId) || string.IsNullOrEmpty(resourceGroupName))
                {
                    return new List<Dictionary<string, object>>();
                }

                var resourceClient = await CreateResourceClientAsync(subscriptionId);

                // Get recent deployments in the RG and find a correlationId match first
                IPage<DeploymentExtendedInner> page = await resourceClient.Deployments.ListByResourceGroupAsync(resourceGroupName);
                DeploymentExtendedInner? targetDeployment = null;

                while (true)
                {
                    foreach (var d in page)
                    {
                        if (string.Equals(d?.Properties?.CorrelationId, correlationId, StringComparison.OrdinalIgnoreCase))
                        {
                            targetDeployment = d;
                            break;
                        }
                    }
                    if (targetDeployment != null || string.IsNullOrEmpty(page?.NextPageLink)) break;
                    page = await resourceClient.Deployments.ListByResourceGroupNextAsync(page.NextPageLink);
                }

                if (targetDeployment != null && !string.IsNullOrEmpty(targetDeployment.Name))
                {
                    // Collect only operations that touched the provided resourceId
                    return await CollectChangesForDeploymentAsync(resourceClient, resourceGroupName, targetDeployment.Name, resourceId);
                }

                // Fallback: scan recent deployments for changes to this resource
                return await GetResourceChangesFromRecentDeployments(resourceClient, resourceGroupName, resourceId);
            }
            catch
            {
                return new List<Dictionary<string, object>>();
            }
        }

        private async Task<List<Dictionary<string, object>>> GetResourceChangesFromRecentDeployments(
            ResourceManagementClient resourceClient,
            string resourceGroupName,
            string resourceId)
        {
            var allChanges = new List<Dictionary<string, object>>();
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24);

            IPage<DeploymentExtendedInner> page = await resourceClient.Deployments.ListByResourceGroupAsync(resourceGroupName);

            while (true)
            {
                foreach (var d in page)
                {
                    // Skip older deployments
                    var ts = d?.Properties?.Timestamp;
                    if (ts.HasValue && ts.Value < cutoff) continue;
                    if (string.IsNullOrEmpty(d?.Name)) continue;

                    var perDeployment = await CollectChangesForDeploymentAsync(resourceClient, resourceGroupName, d.Name, resourceId);
                    if (perDeployment.Count > 0)
                    {
                        allChanges.AddRange(perDeployment);
                    }
                }

                if (string.IsNullOrEmpty(page?.NextPageLink)) break;
                page = await resourceClient.Deployments.ListByResourceGroupNextAsync(page.NextPageLink);
            }

            // newest first
            allChanges = allChanges
                .OrderByDescending(c =>
                {
                    if (c.TryGetValue("timestamp", out var t) && DateTimeOffset.TryParse(t?.ToString(), out var dto)) return dto;
                    return DateTimeOffset.MinValue;
                })
                .ToList();

            return allChanges;
        }

        // small shared helper used by both methods
        private static async Task<List<Dictionary<string, object>>> CollectChangesForDeploymentAsync(
            ResourceManagementClient client, string resourceGroupName, string deploymentName, string resourceId)
        {
            var changes = new List<Dictionary<string, object>>();
            IPage<DeploymentOperationInner> ops = await client.DeploymentOperations.ListAsync(resourceGroupName, deploymentName);

            while (true)
            {
                foreach (var op in ops)
                {
                    var tr = op?.Properties?.TargetResource;
                    var trId = tr?.Id ?? string.Empty;

                    if (!string.IsNullOrEmpty(resourceId) &&
                        !string.Equals(trId, resourceId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var change = new Dictionary<string, object>
                    {
                        ["deploymentName"] = deploymentName,
                        ["operationId"] = op?.OperationId ?? string.Empty,
                        ["timestamp"] = op?.Properties?.Timestamp?.ToString("o") ?? string.Empty,
                        ["provisioningOperation"] = op?.Properties?.ProvisioningOperation?.ToString() ?? string.Empty,
                        ["provisioningState"] = op?.Properties?.ProvisioningState ?? string.Empty,
                        ["resourceId"] = trId,
                        ["resourceType"] = tr?.ResourceType ?? string.Empty,
                        ["resourceName"] = tr?.ResourceName ?? string.Empty
                    };

                    // Track1 gives StatusMessage as a JToken-like object
                    if (op?.Properties?.StatusMessage != null)
                    {
                        change["statusMessage"] = op.Properties.StatusMessage;
                    }

                    changes.Add(change);
                }

                if (string.IsNullOrEmpty(ops?.NextPageLink)) break;
                ops = await client.DeploymentOperations.ListNextAsync(ops.NextPageLink);
            }

            return changes;
        }

        private async Task<ResourceManagementClient> CreateResourceClientAsync(string subscriptionId)
        {
            // Get ARM token via your existing auth service
            var credential = await _authService.GetArmOperationCredential();
            var token = credential.GetToken(
                new TokenRequestContext(["https://management.azure.com/.default"]),
                CancellationToken.None).Token;

            var tokenCreds = new TokenCredentials(token);

            // Reuse the pattern you already use for MonitorManagementClient
            var azureCreds = new Microsoft.Azure.Management.ResourceManager.Fluent.Authentication.AzureCredentials(
                tokenCreds, tokenCreds, tenantId: null, environment: AzureEnvironment.AzureGlobalCloud);

            var restClient = RestClient
                .Configure()
                .WithBaseUri("https://management.azure.com")
                .WithCredentials(azureCreds)
                .Build();

            var rm = new ResourceManagementClient(restClient)
            {
                SubscriptionId = subscriptionId
            };
            return rm;
        }

        private async Task<string> AnalyzeChangeHistoryWithLLM(
            List<Dictionary<string, object>> changeHistory,
            Dictionary<string, object>? deploymentDetails,
            List<Dictionary<string, object>> resourceChanges,
            string correlationId,
            string resourceId)
        {
            try
            {
                var changeHistoryJson = JsonSerializer.Serialize(changeHistory, new JsonSerializerOptions { WriteIndented = true });
                var deploymentDetailsJson = deploymentDetails != null ?
                    JsonSerializer.Serialize(deploymentDetails, new JsonSerializerOptions { WriteIndented = true }) : "No deployment details available";
                var resourceChangesJson = JsonSerializer.Serialize(resourceChanges, new JsonSerializerOptions { WriteIndented = true });

                var prompt = @$"
You are an Azure change analysis expert. I will provide you with detailed change history for a specific operation identified by a correlation ID. Analyze this information and provide comprehensive insights about what changes were made.

Correlation ID: {correlationId}
Resource ID: {resourceId}

Provide a detailed analysis including:

1. **Change Summary**: What high-level changes were made and when?
2. **Operation Timeline**: Chronological sequence of all related operations
3. **Change Details**: Specific changes made including:
   - Configuration changes
   - Resource modifications
   - Parameter changes
   - Template updates
4. **Who Made Changes**: Identity of users/services that made changes
5. **Impact Analysis**: What resources were affected and how
6. **Change Validation**: Whether changes were successful and any issues
7. **Related Operations**: Other operations that happened as part of this change

**Change History Events:**
{changeHistoryJson}

**Deployment Details:**
{deploymentDetailsJson}

**Resource Changes:**
{resourceChangesJson}

Focus on extracting actionable information about what specifically changed. Look for patterns in the HTTP requests, operation names, and properties. Identify the business impact of these changes and any potential issues or rollback scenarios.";

                var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(prompt);
                return response.Text;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error analyzing change history with LLM");
                return $"Error analyzing change history: {ex.Message}";
            }
        }

        private async Task<List<ChangeRecord>> FetchDiffsFromARGByCorrelationIdAsync(
    string correlationId, string subscriptionId)
        {
            var armCred = await _authService.GetArmOperationCredential();
            var token = armCred.GetToken(
                new TokenRequestContext(["https://management.azure.com/.default"]),
                CancellationToken.None).Token;

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var query = $@"
resourcechanges
| extend corr=tostring(properties.changeAttributes.correlationId),
         changeTime=todatetime(properties.changeAttributes.timestamp),
         targetResourceId=tostring(properties.targetResourceId),
         changeType=tostring(properties.changeType),
         changedBy=tostring(properties.changeAttributes.changedBy),
         clientType=tostring(properties.changeAttributes.clientType),
         prevSnap=tostring(properties.changeAttributes.previousResourceSnapshotId),
         newSnap=tostring(properties.changeAttributes.newResourceSnapshotId)
| where corr =~ '{correlationId}'
| project changeTime, targetResourceId, changeType, changedBy, clientType,
          changes=properties.changes, prevSnap, newSnap
| order by changeTime asc";

            var payload = new
            {
                subscriptions = new[] { subscriptionId },
                query,
                options = new { resultFormat = "objectArray" }
            };

            var resp = await http.PostAsync(
                "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();

            using var s = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);

            var rows = doc.RootElement.GetProperty("data").EnumerateArray();
            var results = new List<ChangeRecord>();
            foreach (var row in rows)
            {
                results.Add(new ChangeRecord
                {
                    ChangeTime = row.GetProperty("changeTime").GetDateTime(),
                    TargetResourceId = row.GetProperty("targetResourceId").GetString() ?? "",
                    ChangeType = row.GetProperty("changeType").GetString() ?? "",
                    ChangedBy = row.GetProperty("changedBy").GetString() ?? "",
                    ClientType = row.GetProperty("clientType").GetString() ?? "",
                    ChangesJson = row.GetProperty("changes").GetRawText(),
                    PreviousSnapshotId = row.GetProperty("prevSnap").GetString(),
                    NewSnapshotId = row.GetProperty("newSnap").GetString()
                });
            }
            return results;
        }

        public record ChangeRecord
        {
            public DateTime ChangeTime { get; init; }
            public string TargetResourceId { get; init; } = "";
            public string ChangeType { get; init; } = "";
            public string ChangedBy { get; init; } = "";
            public string ClientType { get; init; } = "";
            public string ChangesJson { get; init; } = "[]";
            public string? PreviousSnapshotId { get; init; }
            public string? NewSnapshotId { get; init; }
        }

        public class ScreenshotResponse
        {
            [JsonPropertyName("screenshot")]
            public string? Screenshot { get; set; }
        }

        #endregion

        public async Task<string> GetResourcePropertiesRealTime(string resourceId)
        {
            try
            {
                // Validate the resource ID format
                if (!Azure.Core.ResourceIdentifier.TryParse(resourceId, out var parsedResourceId))
                {
                    throw new ArgumentException($"Invalid Azure resource ID format: {resourceId}");
                }

                // Extract subscription ID from the resource ID
                var subscriptionId = parsedResourceId!.SubscriptionId;
                if (string.IsNullOrEmpty(subscriptionId))
                {
                    throw new ArgumentException($"Could not extract subscription ID from resource ID: {resourceId}");
                }

                // Build KQL query to get the specific resource
                var query = $@"
                    Resources
                    | where id =~ '{resourceId}'
                    | project id, name, type, kind, location, resourceGroup, subscriptionId, properties, tags
                ";

                _logger.LogInternalInformation("Executing real-time Azure Resource Graph query for resource: {ResourceId}", resourceId);

                // Execute the query using Azure Resource Graph
                var result = await _azureResourceGraphClient.Query(new[] { subscriptionId }, query);

                // Convert result to JSON string
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var jsonResult = JsonSerializer.Serialize(result.Data, jsonOptions);

                _logger.LogInternalInformation("Successfully retrieved real-time properties for resource: {ResourceId}", resourceId);
                return jsonResult;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error retrieving real-time properties for resource: {ResourceId}", resourceId);
                throw new Exception($"Failed to retrieve real-time properties for resource {resourceId}: {ex.Message}", ex);
            }
        }
    }
}
