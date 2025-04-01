// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Schema;
using Azure.Core;
using Gremlin.Net.Driver;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class GraphDBPlugin : IGraphDBPlugin
    {
        public IGraphDatabaseClient GraphDbClient { get; }

        public IChatClient ChatClient { get; }

        public ThreadContext? Context { get; set; }

        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

        private const string MermaidServiceAPI = "https://mermaid-renderer.salmonhill-ad96bd78.eastus2.azurecontainerapps.io/render";
        public ILogger<GraphDBPlugin> _logger { get; }
        public GraphDBPlugin(IGraphDatabaseClient graphDbClient, IChatClient chatClient, IAgentOutboundCommunicationService agentInboundCommunicationService, ILogger<GraphDBPlugin> logger)
        {
            GraphDbClient = graphDbClient;
            ChatClient = chatClient;
            _agentOutboundCommunicationService = agentInboundCommunicationService;
            _logger = logger;
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
                if (Context != null)
                {
                    threadId = Context.ThreadId;
                }
                else
                {
                    _logger.LogWarning("[VisualizeApplicationComponents] ThreadId is null. Cannot append image to message.");
                    return "Error: ThreadId is null. Cannot generate visualization without a valid thread ID.";
                }
            }

            // Retry policy configuration
            int maxRetries = 3;
            int retryDelayMilliseconds = 1000; // Fixed delay of 1 second

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Main execution logic
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

                    // Generate and return the base64-encoded graph image
                    var base64EncodedGraph = await GenerateMermaidGraph(mermaidSpec);
                    _logger.LogInformation($"base64 encoded image: {base64EncodedGraph}");
                    await _agentOutboundCommunicationService.AppendAgentImageMessage(threadId.Value, $"![DailyReport Dashboard](data:image/png;base64,{base64EncodedGraph})\r\n");

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
                    // Set a timeout for the HTTP client
                    httpClient.Timeout = TimeSpan.FromSeconds(30); // Reduced timeout to detect issues faster

                    // Prepare the API request as JSON with "spec" property
                    var jsonPayload = new { spec = mermaidSpec };
                    var requestContent = new StringContent(
                        JsonSerializer.Serialize(jsonPayload),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    try
                    {
                        // Make the request to the container app's render endpoint
                        var response = await httpClient.PostAsync(MermaidServiceAPI, requestContent);

                        // Check if the request was successful
                        if (response.IsSuccessStatusCode)
                        {
                            // Read the JSON response containing base64-encoded image data
                            var jsonResponse = await response.Content.ReadAsStringAsync();
                            var responseObject = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                            var base64Image = responseObject.GetProperty("image_base64").GetString();

                            _logger.LogInformation("Successfully generated graph visualization");

                            // Return the base64-encoded image
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

                        // Return a user-friendly message with the raw Mermaid spec as a fallback
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

        private async Task<ResultSet<dynamic>> GetApplicationComponentsRaw(string resourceId, int hops = 3)
        {
            _logger.LogInformation($"[GetApplicationComponentsRaw] Invoked with resourceId: {resourceId}");

            try
            {
                // Create a unified query that works for all resource types
                string query = $@"g.V().has('id', '{resourceId.ToLower().Replace("/", "_")}')
                    .repeat(
                        union(
                            outE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS').inV(),
                            inE('LINKED', 'CONNECTED', 'CONTAINS', 'HOSTED_ON', 'SQL_CONNECTED', 'REDIS_CONNECTED', 'USES_REDIS').outV()
                        )
                        .not(has('resourceType', within('resourcegroup', 'subscription')))
                        .simplePath()
                    )
                    .times({hops})
                    .emit()
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
                throw; // Let the calling methods handle the error according to their needs
            }
        }

        public async Task<List<ApplicationGraph>> DiscoverApplications(string subscriptionId)
        {
            _logger.LogInformation($"[DiscoverApplications] Invoked with subscription {subscriptionId}");

            try
            {
                // Identify all potential application entry points
                string entryPointQuery = BuildDiscoverApplicationsQuery(subscriptionId);
                var entryPointResult = await Query(entryPointQuery);
                var entryPoints = ConvertResultToNodes(entryPointResult);

                var applications = new List<ApplicationGraph>();

                // Process each entry point
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
            // Find resources that are typically application entry points
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
                var properties = new Dictionary<string, object>();
                foreach (var prop in item["properties"])
                {
                    properties.Add(prop.Key, prop.Value);
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

        public async Task AddSourceCodeNodeToContainerAppNodeAsync(string resourceId, string repoUrl)
        {
            try
            {
                var containerAppNodeId = resourceId.ToLower().Replace("/", "_");
                string vertexFilter = $"hasId('{containerAppNodeId}')"; // Replacing "/" with "_" as graph IDs use underscores

                string query = $@"
                    g.V().{vertexFilter}";

                var containerAppNodeResults = await GraphDbClient.Query(query);
                if (!containerAppNodeResults.Any())
                {
                    return;
                }

                // Check if the source code node exists, create it if it doesn't
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
    }
}
