// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Schema;
using Azure.Core;
using Gremlin.Net.Driver;
using Microsoft.Azure.Management.Monitor.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Rest.Azure;
using Microsoft.Rest.Azure.OData;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class GraphDBPlugin : IGraphDBPlugin
    {
        public IGraphDatabaseClient GraphDbClient { get; }
        public IChatClient ChatClient { get; }

        // Using ThreadContext from your original code. When a thread ID is needed, we pull it from here.
        public Guid? ThreadId { get; set; }

        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
        public ILogger<GraphDBPlugin> _logger { get; }

        // Additional fields for health dashboard screenshot and LLM summarization
        private readonly string _grafanaUrl;
        private readonly string _grafanaToken;
        private readonly string _puppeteerScreenshotApiUrl;
        private readonly HttpClient _httpClient;
        private readonly DashboardSettings _dashboardSettings;
        private readonly IAuthenticationService _authService;

        private const string MermaidServiceAPI = "https://mermaid-renderer.salmonhill-ad96bd78.eastus2.azurecontainerapps.io/render";

        private readonly Dictionary<string, string> _dashboardsToProcessByResourceType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
            IChatClient chatClient,
            DashboardSettings dashboardSettings,
            IAgentOutboundCommunicationService agentOutboundCommunicationService,
            ILogger<GraphDBPlugin> logger,
            IAuthenticationService authService)
        {
            GraphDbClient = graphDbClient;
            ChatClient = chatClient;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
            _logger = logger;
            _dashboardSettings = dashboardSettings;

            _grafanaUrl = dashboardSettings.GrafanaUrl.TrimEnd('/');
            _grafanaToken = dashboardSettings.GrafanaApiKey;
            _puppeteerScreenshotApiUrl = "https://test-capp.ambitiouspond-10f27fe1.canadaeast.azurecontainerapps.io";
            _httpClient = new HttpClient();
            _authService = authService;
        }

        /// <summary>
        /// When implementing this in prod, we need to give this agent a read-only user
        /// </summary>
        [KernelFunction("query")]
        [Description("Run a generic query against the graph database. Do NOT perform any write operations.")]
        public async Task<ResultSet<dynamic>> Query(string query)
        {
            return await GraphDbClient.Query(query);
        }

        public async Task<string> FindAllNetworkConnectedResources(string resourceId = "")
        {
            try
            {
                string vertexFilter = string.IsNullOrEmpty(resourceId)
                    ? "hasLabel('microsoft.app/containerapps')"
                    : $"hasId('{resourceId.ToLower().Replace("/", "_")}')"; // Replacing "/" with "_" as graph IDs use underscores

                string query = $@"
    g.V().{vertexFilter}
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

                var results = await GraphDbClient.Query(query);
                return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding network connected resources");
                return $"Error finding network connected resources: {ex.Message}";
            }
        }

        public async Task<List<Node>> GetApplicationComponentsSummary(string resourceId, int hops = 3)
        {
            _logger.LogInformation($"[GetApplicationComponentsSummary] Invoked with resourceId: {resourceId}");

            var result = await GetApplicationComponentsRaw(resourceId, hops);
            return ConvertResultToNodes(result);
        }

        public async Task<string> VisualizeAKSMicroserviceTopology(
            string AKSClusterResourceId,
            string _namespace,
            string deploymentName,
            Guid? threadId = null)
        {
            _logger.LogInformation($"[VisualizeAKSMicroserviceTopology] Invoked with resourceId: {AKSClusterResourceId}");

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
                    _logger.LogWarning("[VisualizeAKSMicroserviceTopology] ThreadId is null. Cannot append image to message.");
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
                        _logger.LogWarning($"No components found to visualize, cluster {AKSClusterResourceId}, namespace {_namespace}, deployment {deploymentName}");
                        return "Error: No components found to visualize";
                    }

                    // First, deserialize the result to a more strongly-typed structure we can work with
                    string tempJson = JsonSerializer.Serialize(result);
                    var typedResult = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(tempJson);

                    // Filter out pods, services, and nodes from each item
                    foreach (var item in typedResult)
                    {
                        if (item.ContainsKey("objects"))
                        {
                            // Get the objects array
                            var objectsJson = JsonSerializer.Serialize(item["objects"]);
                            var objects = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(objectsJson);

                            // Filter the objects
                            var filteredObjects = objects.Where(obj =>
                            {
                                if (obj.ContainsKey("resourceType"))
                                {
                                    var resourceTypeJson = JsonSerializer.Serialize(obj["resourceType"]);
                                    var resourceTypes = JsonSerializer.Deserialize<List<string>>(resourceTypeJson);

                                    // Check if the first resource type ends with /services, /pods or /nodes
                                    if (resourceTypes.Count > 0)
                                    {
                                        string resourceType = resourceTypes[0];
                                        return !resourceType.EndsWith("/services") && !resourceType.EndsWith("/pods") && !resourceType.EndsWith("/nodes");
                                    }
                                }
                                return true; // Keep objects without resourceType
                            }).ToList();

                            // Replace the objects array with the filtered one
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
                            _logger.LogWarning($"[VisualizeAKSMicroserviceTopology] Could not serialize filtered item for deduplication. Skipping item. Error: {ex.Message}");
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
                    var response = await ChatClient.GetResponseAsync(prompt);
                    var mermaidSpec = response.Text;
                    _logger.LogInformation($"Generated Mermaid specification successfully: {mermaidSpec}");

                    var base64EncodedGraph = await GenerateMermaidGraph(mermaidSpec);
                    _logger.LogInformation($"base64 encoded image: {base64EncodedGraph}");
                    await _agentOutboundCommunicationService.AppendAgentImageMessage(threadId.Value, $"![Microservice Topology](data:image/png;base64,{base64EncodedGraph})\r\n");

                    // Construct the final response string for the LLM, this helps the LLM to further answer questions regarding the topology
                    string llmResponse = $@"I have analyzed the microservice topology starting from '{deploymentName}' in the '{_namespace}' namespace within the cluster '{AKSClusterResourceId}'.

A visual diagram representing these relationships has been generated and added to our chat.

For reference, here is the raw Mermaid specification used to create the diagram:
```mermaid
{mermaidSpec}
```";

                    _logger.LogInformation("[VisualizeAKSMicroserviceTopology] Successfully generated visualization and response text.");
                    return llmResponse;
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning($"[VisualizeAKSMicroserviceTopology] Attempt {attempt} failed with error: {ex.Message}. Retrying in {retryDelayMilliseconds}ms...");
                        await Task.Delay(retryDelayMilliseconds);
                    }
                    else
                    {
                        _logger.LogError($"[VisualizeAKSMicroserviceTopology] All {maxRetries} attempts to render the image failed. Last error: {ex.Message}");
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
            _logger.LogInformation($"[VisualizeApplicationComponents] Invoked with resourceId: {resourceId}");

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
            if (threadId == null)
            {
                if (ThreadId != null)
                {
                    threadId = ThreadId;
                }
                else
                {
                    _logger.LogWarning("[VisualizeApplicationComponents] ThreadId is null. Cannot append image to message.");
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
                        _logger.LogInformation($"No components found for resourceId: {resourceId}");
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
                    var response = await ChatClient.GetResponseAsync(prompt);
                    var mermaidSpec = response.Text;
                    _logger.LogInformation("Generated Mermaid specification successfully");

                    var base64EncodedGraph = await GenerateMermaidGraph(mermaidSpec);
                    _logger.LogInformation($"base64 encoded image: {base64EncodedGraph}");
                    // TODO: read threadcontext from cosmos db
                    await _agentOutboundCommunicationService.AppendAgentImageMessage(threadId.Value, $"![Application Group](data:image/png;base64,{base64EncodedGraph})\r\n");

                    return "Visualization Rendered!";
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning($"[VisualizeApplicationComponents] Attempt {attempt} failed with error: {ex.Message}. Retrying in {retryDelayMilliseconds}ms...");
                        await Task.Delay(retryDelayMilliseconds);
                    }
                    else
                    {
                        _logger.LogError($"[VisualizeApplicationComponents] All {maxRetries} attempts to render the image failed. Last error: {ex.Message}");
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
                _logger.LogInformation("Calling Mermaid rendering service to generate graph visualization");

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

                            _logger.LogInformation("Successfully generated graph visualization");
                            return base64Image;
                        }
                        else
                        {
                            var errorMessage = await response.Content.ReadAsStringAsync();
                            _logger.LogError($"Error calling Mermaid rendering service. Status: {response.StatusCode}, Message: {errorMessage}");
                            return $"Error generating visualization: {response.StatusCode}. The rendering service returned an error. Please try a smaller graph or different visualization settings.";
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogWarning("The visualization request timed out after {Timeout} seconds", httpClient.Timeout.TotalSeconds);
                        return $"The visualization request timed out after {httpClient.Timeout.TotalSeconds} seconds. The graph may be too complex to render. Here's the raw Mermaid specification that you can paste into a Mermaid editor:\n\n```mermaid\n{mermaidSpec}\n```";
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex, "HTTP request error while calling Mermaid rendering service");
                        return $"Error connecting to the visualization service: {ex.Message}. Please check network connectivity or try again later.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while calling Mermaid rendering service");
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
g.V().has('id', '{deploymentResourceId}')
.repeat(out('LINKED', 'CONNECTED', 'OWNED_BY', 'HOSTED_ON', 'SQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS', 'BACKED_BY'))
.emit()
.path().by(valueMap('name', 'resourceType'))";

                _logger.LogInformation($"Executing AKS microservice topology query for resource: {resourceId}, namespace: {namespaceName}, deployment: {deploymentName ?? "all"}");
                return await GraphDbClient.Query(query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing AKS microservice topology Gremlin query");
                throw;
            }
        }

        private async Task<ResultSet<dynamic>> GetApplicationComponentsRaw(string resourceId, int hops = 3)
        {
            _logger.LogInformation($"[GetApplicationComponentsRaw] Invoked with resourceId: {resourceId}");

            try
            {
                string query = $@"g.V().has('id', '{resourceId.ToLower().Replace("/", "_")}')
                    .union(
                        identity(),
                        repeat(
                            union(
                                outE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS').inV(),
                                inE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS').outV()
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
                _logger.LogError(ex, "Error finding application components");
                throw;
            }
        }

        public async Task<List<ApplicationGraph>> DiscoverApplications(string subscriptionId)
        {
            _logger.LogInformation($"[DiscoverApplications] Invoked with subscription {subscriptionId}");

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
                    var components = await GetApplicationComponentsSummary(resourceId, 3);

                    if (components.Count == 0)
                    {
                        _logger.LogWarning($"No components found for application: {entryPoint.Name}");
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
                _logger.LogError(ex, "Error discovering applications");
                return new List<ApplicationGraph>();
            }
        }

        private string BuildDiscoverApplicationsQuery(string subscriptionId)
        {
            return $@"g.V().has('subscriptionId', '{subscriptionId.ToLower()}')
                .out('{Constants.Relationships.Contains}')
                .out('{Constants.Relationships.Contains}')
                .hasLabel(within(
                    '{Constants.ContainerAppType.ToLower()}',
                    '{Constants.AppServiceType.ToLower()}',
                    '{Constants.AzureKubernetesServiceType.ToLower()}'
                ))
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

        public async Task AddSourceCodeNodeToContainerAppNodeAsync(string resourceId, string repoUrl)
        {
            try
            {
                var containerAppNodeId = resourceId.ToLower().Replace("/", "_");
                string vertexFilter = $"hasId('{containerAppNodeId}')";
                string query = $@"g.V().{vertexFilter}";
                var containerAppNodeResults = await GraphDbClient.Query(query);
                if (!containerAppNodeResults.Any())
                {
                    return;
                }

                string sourceCodeNodeId = repoUrl.ToLower().Replace("/", "_");
                string checkSourceCodeNodeQuery = $"g.V('{sourceCodeNodeId}').hasLabel('microsoft.source/repository')";
                var sourceCodeNodeResults = await GraphDbClient.Query(checkSourceCodeNodeQuery);

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

                    await GraphDbClient.AddOrUpdateNodeAsync("microsoft.source/repository", sourceCodeNodeId, "microsoft.source/repository", properties);
                }

                await GraphDbClient.AddOrUpdateEdgeAsync(containerAppNodeId, sourceCodeNodeId, Constants.Relationships.ServesCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding network connected resources");
            }
        }

        public async Task<List<string>> GetContainerAppsWithNodesWithoutSourceCodeNodesAsync()
        {
            var queryResults = await GraphDbClient.Query(@"
                g.V().has('resourceType', 'microsoft.app/containerapps')
                .not(outE().hasLabel('SERVES_CODE').inV().has('resourceType', 'microsoft.source/repository'))
                .values('resourceId')");

            var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();

            return resources;
        }

        public async Task UpdateRepoNodeWithLastScanTime(string repoUrl)
        {
            var queryResults = await GraphDbClient.Query($@"
                g.V().has('resourceId', '{repoUrl}')
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

            await GraphDbClient.AddOrUpdateNodeAsync(label, id, resourceType, properties);
        }

        public async Task<Dictionary<string, object>> GetResourceBasicProperties(string resourceId)
        {
            var query = $@"
                g.V({CrawlerExtensions.GetSanitizedCosmosDBId(resourceId)})
                .project('subscriptionId', 'resourceGroupName', 'resourceType', 'resourceName', 'location')
                .by(coalesce(values('subscriptionId'), constant('unknown')))
                .by(coalesce(values('resourceGroupName'), constant('unknown')))
                .by(coalesce(values('resourceType'), constant('unknown')))
                .by(coalesce(values('resourceName'), constant('unknown')))
                .by(coalesce(values('location'), constant('unknown')))
                ";

            var results = await GraphDbClient.Query<Dictionary<string, object>>(query);

            return results.FirstOrDefault(new Dictionary<string, object>());
        }

        public async Task<Dictionary<string, object>> GetResourceDetailedProperties(string resourceId)
        {
            if (!ResourceIdentifier.TryParse(resourceId, out _))
            {
                throw new Exception("Invalid Azure resource Id, should be of form /subscriptions/<>/resourceGroups/<>/providers/<providerName>/<resourceType>/<resourceName>");
            }
            var query = $@"g.V('{CrawlerExtensions.GetSanitizedCosmosDBId(resourceId.ToLower())}').properties().as('p').group().by(select('p').key()).by(select('p').value())";

            var results = await GraphDbClient.Query<Dictionary<string, object>>(query);
            return results.FirstOrDefault(new Dictionary<string, object>());
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
                    _logger.LogWarning("Resource name or type cannot be empty");
                    return "Failed to generate health summary: Resource name or type not provided.";
                }

                // Normalize resource type to case-insensitive comparison
                resourceType = resourceType.ToLowerInvariant();

                // Check if the resource type is supported
                if (!_dashboardsToProcessByResourceType.TryGetValue(resourceType, out var dashboardType))
                {
                    _logger.LogWarning("Unsupported resource type for dashboard: {ResourceType}", resourceType);
                    return $"Failed to generate health summary: Dashboard not available for resource type '{resourceType}'.";
                }

                // Look up the resource in our database/graph
                var resourceNodes = await SearchResourceAsync(resourceName, resourceType);
                if (resourceNodes == null || !resourceNodes.Any())
                {
                    _logger.LogWarning("Resource not found: {ResourceName} of type {ResourceType}", resourceName, resourceType);
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
                    { "var-sub", resource.SubscriptionId },
                    { "var-rg", resource.ResourceGroupName.ToLowerInvariant() },
                    { "var-resource", resource.ResourceName.ToLowerInvariant() }
                };

                switch (dashboardType)
                {
                    case "azure-container-apps-container-app-view":
                        queryParams["var-containerapp"] = resource.ResourceName.ToLowerInvariant();
                        break;
                    case "azure-redis":
                        queryParams["var-name"] = resource.ResourceName.ToLowerInvariant();
                        break;
                    case "azure-app-service-monitoring":
                        queryParams["var-name"] = resource.ResourceName.ToLowerInvariant();
                        break;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_dashboardSettings.GrafanaApiKey}");
                var dashboardResponse = await _httpClient.GetAsync($"{_grafanaUrl}/api/search?type=dash-db");
                dashboardResponse.EnsureSuccessStatusCode();
                var dashboardsContent = await dashboardResponse.Content.ReadAsStringAsync();
                var dashboards = JsonSerializer.Deserialize<JsonElement>(dashboardsContent);
                string dashboardUrl = null;
                foreach (var dashboard in dashboards.EnumerateArray())
                {
                    if (dashboard.TryGetProperty("url", out var urlElement) &&
                        urlElement.GetString().Contains(dashboardType, StringComparison.OrdinalIgnoreCase))
                    {
                        dashboardUrl = $"{urlElement.GetString()}";
                        break;
                    }
                }

                if (string.IsNullOrEmpty(dashboardUrl))
                {
                    dashboardUrl = baseUrl;
                }

                // Build the final dashboard URL
                dashboardUrl = AddQueryParameters(dashboardUrl, queryParams);

                _logger.LogInformation("Generated dashboard URL: {DashboardUrl} for resource {ResourceName}", dashboardUrl, resourceName);

                // Capture screenshot for the dashboard
                var screenshotResponse = await CaptureDashboardScreenshotAsync(dashboardUrl);
                if (string.IsNullOrEmpty(screenshotResponse?.Screenshot))
                {
                    _logger.LogWarning("No screenshot captured for resource {ResourceName}", resourceName);
                    return $"Failed to capture dashboard screenshot for resource '{resourceName}'. Dashboard URL: {dashboardUrl}";
                }

                if (ThreadId != null)
                {
                    // TODO: read threadcontext from cosmos db
                    await _agentOutboundCommunicationService.AppendAgentImageMessage(ThreadId.Value, $"![DailyReport Dashboard](data:image/png;base64,{screenshotResponse.Screenshot})\r\n");
                }

                string healthSummary = await SummarizeDashboardScreenshotAsync(screenshotResponse.Screenshot, dashboardUrl);
                _logger.LogInformation("General health summary generated successfully for resource {ResourceName}.  Dashboard URL: {dashboardUrl}", resourceName);
                return $"Summary--------{healthSummary}------\nDashboard URL from where the summary was retrieved: {_grafanaUrl}{dashboardUrl}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating general health summary for resource {ResourceName} of type {ResourceType}", resourceName, resourceType);
                return $"Error generating health summary: {ex.Message}";
            }
        }

        public string GetKnowledgeGraphResourceUsageDashboard()
        {
            if (string.IsNullOrEmpty(_dashboardSettings?.PrometheusUrl))
            {
                return "Dashboard is not configured for this agent. Must use Knowledge graph queries. If user wants agent to deploy a dashboard they must configure Dashboard Settings.";
            }

            return $"Dashboard URL: {_dashboardSettings.GrafanaUrl}/d/azure-sre-resources/sre-azure-resource-overview?orgId=1&refresh=1m";
        }

        public async Task<List<Dictionary<string, object>>> ListResourcesByTypeAsync(string resourceType, string propertyName, string propertyValue)
        {
            try
            {
                // only return basic properties
                string query = $@"g.V().hasLabel('{resourceType.ToLower()}')";

                if (!string.IsNullOrEmpty(propertyName) && !string.IsNullOrEmpty(propertyValue))
                {
                    query += $".has('{propertyName}', '{propertyValue}')";
                }
                query += @".project('subscriptionId', 'resourceGroupName', 'resourceName', 'resourceType')
                    .by(coalesce(values('subscriptionId'), constant('')))
                    .by(coalesce(values('resourceGroupName'), constant('')))
                    .by(coalesce(values('resourceName'), constant('')))
                    .by(coalesce(values('resourceType'), constant('')))";

                var result = await GraphDbClient.Query(query);
                var resources = new List<Dictionary<string, object>>();

                foreach (var item in result)
                {
                    // Create a new dictionary for each resource
                    var propertyBag = new Dictionary<string, object>();

                    // Add label
                    propertyBag["subscriptionId"] = item["subscriptionId"]?.ToString();
                    propertyBag["resourceGroupName"] = item["resourceGroupName"]?.ToString();
                    propertyBag["resourceName"] = item["resourceName"]?.ToString();
                    propertyBag["resourceType"] = item["resourceType"]?.ToString();

                    resources.Add(propertyBag);
                }

                _logger.LogInformation("Found {Count} resources of type '{ResourceType}'", resources.Count, resourceType);
                return resources;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing resources of type '{Type}'", resourceType);
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
                g.V().hasLabel('{resourceType.ToLower()}')
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

                var result = await GraphDbClient.Query(query);
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
                _logger.LogError(ex, "Error searching for resource with name '{Name}' and type '{Type}'", resourceName, resourceType);
                throw;
            }
        }

        /// <summary>
        /// Searches for resources by a partial resource name.
        /// </summary>
        public async Task<List<ArmResourceNode>> SearchResourceByNameAsync(string resourceName)
        {
            try
            {
                string modifiedResourceName = SanitizeInputForQuery(resourceName);
                string query = $@"
                g.V()
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

                var result = await GraphDbClient.Query(query);
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
                _logger.LogError(ex, "Error searching for resource with name '{Name}'", resourceName);
                throw;
            }
        }

        public async Task<dynamic> GetResourceCountAsync(string resourceType, string groupBy = "")
        {
            try
            {
                string query;

                if (string.IsNullOrWhiteSpace(groupBy))
                {
                    // Simple count query when no groupBy is specified
                    query = $@"
                g.V().hasLabel('{resourceType.ToLower()}')
                .count()
            ";

                    var result = await GraphDbClient.Query(query);

                    long count = 0;
                    if (result != null && result.Count > 0)
                    {
                        // Convert the first result to long
                        count = Convert.ToInt64(result.First());
                        _logger.LogInformation("Found {Count} resources of type '{Type}'", count, resourceType);
                    }

                    return new { ResourceType = resourceType, Count = count };
                }
                else
                {
                    query = $@"
    g.V().hasLabel('{resourceType.ToLower()}')
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

                    var result = await GraphDbClient.Query(query);
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
                            _logger.LogWarning(ex, "Error processing result item");
                        }
                    }

                    _logger.LogInformation("Found resource counts grouped by '{GroupBy}' for type '{Type}'", groupBy, resourceType);
                    return new { ResourceType = resourceType, GroupBy = groupBy, Counts = groupedResults };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting count for resource type '{Type}' grouped by '{GroupBy}'", resourceType, groupBy);
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
                _logger.LogError(ex, "Error getting managed resources information");
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
                string query = $@"g.V().has('resourceType', '{SubscriptionNode.Type}')
                          .project('name', 'id')
                          .by('subscriptionName')
                          .by('subscriptionId')";

                var result = await GraphDbClient.Query(query);

                _logger.LogInformation("Found {Count} subscriptions", result.Count);

                // Return the list of subscription objects with name and id properties intact
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing subscriptions");
                throw;
            }
        }

        /// <summary>
        /// Retrieves and summarizes activity logs for a container app and its dependent resources.
        /// </summary>
        [KernelFunction("GetActivityLogsSummary")]
        [Description("Retrieves and summarizes activity logs for a container app and its dependent resources")]
        public async Task<string> FetchAndSummarizeActivityLogs(
            string resourceId,
            int daysBack = 1,
            Guid? threadId = null)
        {
            _logger.LogInformation($"[FetchAndSummarizeActivityLogs] Invoked with resourceId: {resourceId}");

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
                        _logger.LogWarning($"No components found for resourceId: {resourceId}");
                        return $"No components found for resourceId: {resourceId}. Was the correct resource ID provided? Alternatively, the Knowledge Graph may not have been built for this component.";
                    }

                    var resourceIds = components.Select(c => c.Id).ToList();
                    var rgs = ExtractUniqueResourceGroups(resourceIds);
                    _logger.LogInformation($"Found {resourceIds.Count} related resources for activity log analysis");

                    var allActivityLogs = new List<Dictionary<string, object>>();

                    foreach (var id in rgs)
                    {
                        var logs = await FetchActivityLogsForResource(id.Replace("_", "/"), daysBack);
                        if (logs.Count > 0)
                        {
                            allActivityLogs.AddRange(logs);
                        }
                        _logger.LogInformation($"Fetched {logs.Count} activity logs for resource {id}");
                    }

                    if (allActivityLogs.Count == 0)
                    {
                        return "No activity logs found for the specified resource and its dependencies in the last " + daysBack + " days.";
                    }

                    allActivityLogs = allActivityLogs
                        .OrderByDescending(log =>
                            log.ContainsKey("eventTimestamp") ?
                            DateTimeOffset.Parse(log["eventTimestamp"].ToString()) :
                            DateTimeOffset.MinValue)
                        .ToList();

                    _logger.LogInformation($"Total activity logs collected: {allActivityLogs.Count}");

                    var logsJson = JsonSerializer.Serialize(allActivityLogs, new JsonSerializerOptions { WriteIndented = true });
                    var summary = await SummarizeLogsWithLLM(logsJson, components?.ToString());

                    return summary;
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning($"[FetchAndSummarizeActivityLogs] Attempt {attempt} failed with error: {ex.Message}. Retrying in {retryDelayMilliseconds}ms...");
                        await Task.Delay(retryDelayMilliseconds);
                    }
                    else
                    {
                        _logger.LogError($"[FetchAndSummarizeActivityLogs] All {maxRetries} attempts failed. Last error: {ex.Message}");
                        throw;
                    }
                }
            }

            return "Error: Unexpected execution path in fetching and summarizing activity logs";
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

        private async Task<List<Dictionary<string, object>>> FetchActivityLogsForResource(string resourceId, int daysBack)
        {
            _logger.LogInformation($"[FetchActivityLogsForResource] Fetching activity logs for: {resourceId}");

            try
            {
                var resourceIdentifier = new ResourceIdentifier(resourceId);
                var subscriptionId = resourceIdentifier.SubscriptionId;
                var resourceGroupName = resourceIdentifier.ResourceGroupName;

                var credential = _authService.GetArmOperationCredential();
                var defaultToken = credential.GetToken(new TokenRequestContext(new[] { "https://management.azure.com/.default" }), CancellationToken.None).Token;
                var defaultTokenCredentials = new Microsoft.Rest.TokenCredentials(defaultToken);
                var azureCredentials = new Microsoft.Azure.Management.ResourceManager.Fluent.Authentication.AzureCredentials(defaultTokenCredentials, defaultTokenCredentials, null, AzureEnvironment.AzureGlobalCloud);

                var restClient = RestClient.Configure()
                    .WithBaseUri("https://management.azure.com")
                    .WithCredentials(azureCredentials)
                    .Build();

                var monitorClient = new MonitorManagementClient(restClient)
                {
                    SubscriptionId = subscriptionId
                };

                var startTime = DateTime.UtcNow.AddDays(-daysBack);
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
                        var log = new Dictionary<string, object>
                        {
                            ["resourceId"] = resourceId,
                            ["resourceName"] = resourceIdentifier.Name,
                            ["resourceType"] = resourceIdentifier.ResourceType.ToString(),
                            ["eventTimestamp"] = eventData.EventTimestamp?.ToString("o"),
                            ["operationName"] = eventData.OperationName?.Value,
                            ["caller"] = eventData.Caller,
                            ["status"] = eventData.Status?.Value,
                            ["correlationId"] = eventData.CorrelationId,
                            ["category"] = eventData.Category?.Value,
                            ["level"] = eventData.Level?.ToString()
                        };

                        // Add caller IP if available
                        if (eventData.HttpRequest != null)
                        {
                            log["callerIpAddress"] = eventData.HttpRequest.ClientIpAddress;
                        }

                        // Add authorization details if available
                        if (eventData.Authorization != null)
                        {
                            log["authorizationAction"] = eventData.Authorization.Action;
                            log["authorizationScope"] = eventData.Authorization.Scope;
                        }

                        // Add properties
                        if (eventData.Properties != null)
                        {
                            log["properties"] = JsonSerializer.Serialize(eventData.Properties);
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
                _logger.LogError(ex, $"Error fetching activity logs for {resourceId}");
                return new List<Dictionary<string, object>>();
            }
        }

        private string SanitizeInputForQuery(string input)
        {
            // Sanitize the input to prevent injection attacks
            return input.Replace("'", "''").Replace(".", "").Replace("//", "");
        }


        private async Task<string> SummarizeLogsWithLLM(string logsJson, string gremlinOutput)
        {
            try
            {
                var prompt = @$"
You are a cloud operations analyst. I will provide you with Azure activity logs for a resource group of an app facing an issue. This might contain some noise since the resource group may have other resources. Provide a concise summary following instructions below:

Here's a gremlin output of resources we care about:

{gremlinOutput}

Please analyze these logs and provide a summary that includes:

1. A high-level overview of the activity
2. Key changes made to the resources (when and by whom), collapse same operation (e.g. role assignments) into same point
3. Patterns of activity (e.g., regular deployments, configuration changes)
4. Any potential issues or concerns
5. Recommendations based on the activity patterns

Each log entry contains:
- resourceId: The Azure resource identifier
- resourceName: The name of the resource
- resourceType: The type of Azure resource
- eventTimestamp: When the activity occurred
- operationName: What action was performed
- caller: The user or service principal that performed the action
- callerIpAddress: The IP address of the caller
- status: Success or failure of the operation
- correlationId: Unique identifier to track related activities
- category: Type of activity (Administrative, Security, etc.)
- properties: Additional details about the activity

Here are the logs in JSON format:

{logsJson}

Please provide a highly concise summary with sections for each of the above points. Focus on who made changes (mention the name), when they were made, and what kinds of changes were made. Identify patterns and potential issues. Remember the summary should be very concise and to the point. Respond in a **minimalist, structured format** with no fluff. Using fewer words per point while preserving clarity.";

                var response = await ChatClient.GetResponseAsync(prompt);
                return response.Text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error summarizing logs with LLM");
                return $"Error summarizing logs: {ex.Message}";
            }
        }

        #endregion

        #region Dummy Screenshot & LLM Summary Methods

        private async Task<ScreenshotResponse> CaptureDashboardScreenshotAsync(string dashboardUrl)
        {
            try
            {
                var payload = new
                {
                    grafanaEndpoint = _grafanaUrl,
                    grafanaToken = _grafanaToken,
                    dashboardUrl
                };

                string requestJson = JsonSerializer.Serialize(payload);
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                _logger.LogInformation("Requesting screenshot for dashboard: {DashboardUrl}", dashboardUrl);

                var response = await _httpClient.PostAsync($"{_puppeteerScreenshotApiUrl}/screenshot", content);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                var screenshotResponse = JsonSerializer.Deserialize<ScreenshotResponse>(responseJson);

                _logger.LogInformation("Screenshot captured successfully for dashboard: {DashboardUrl}", dashboardUrl);
                return screenshotResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing dashboard screenshot for {DashboardUrl}", dashboardUrl);
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
                var messages = new List<Microsoft.Extensions.AI.ChatMessage>();
                var prompt = $"Please analyze the dashboard screenshot for the dashboard at {dashboardUrl}. " +
                             $"Based on the visual data, provide a concise summary of the resource's health and any notable observations.";

                _logger.LogInformation("Sending screenshot data to LLM for summarization for dashboard: {DashboardUrl}", dashboardUrl);
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, prompt));
                var screenshot = Convert.FromBase64String(base64Screenshot);
                var content = new List<AIContent>
                    {
                        new Microsoft.Extensions.AI.TextContent($"This is a screenshot of the dashboard: {dashboardUrl}"),
                        new DataContent(screenshot, "image/png")
                    };
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, content));

                var options = new ChatOptions
                {
                    Temperature = (float)0.2,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["response_format"] = "text"
                    }
                };

                var response = await ChatClient.GetResponseAsync(messages, options);
                string summary = response.Text;

                _logger.LogInformation("Dashboard summary generated successfully for {DashboardUrl}", dashboardUrl);
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error summarizing dashboard screenshot for {DashboardUrl}", dashboardUrl);
                return $"Error summarizing dashboard screenshot: {ex.Message}";
            }
        }

        public class ScreenshotResponse
        {
            [JsonPropertyName("screenshot")]
            public string Screenshot { get; set; }
        }

        #endregion
    }
}
