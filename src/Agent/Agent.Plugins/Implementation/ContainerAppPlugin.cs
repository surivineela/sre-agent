// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Network;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using static Agent.Core.Extensions.TaskExtensions;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Agent.Plugins.Implementation
{
    public class ContainerAppPlugin : IContainerAppPlugin
    {
        private readonly ArmHelper _armHelper;
        private readonly IGraphDatabaseClient _databaseClient;
        private readonly ILogger<ContainerAppPlugin> _logger;
        private readonly ArmClient _armClient;
        private readonly IAuthenticationService _authService;
        private readonly HttpClient _httpClient;
        private readonly ILogAnalyticsService _logAnalyticsService;
        private readonly IChatClient _chatClient;

        public ContainerAppPlugin(ArmHelper armHelper,
            IGraphDatabaseClient graphDbClient,
            ILogger<ContainerAppPlugin> logger,
            IArmClientFactory armClientFactory,
            IAuthenticationService authService,
            IHttpClientFactory httpClientFactory,
            ILogAnalyticsService logAnalyticsService,
            IChatClient chatClient)
        {
            _armClient = armClientFactory.GetArmClient();
            _databaseClient = graphDbClient;
            _armHelper = armHelper;
            _logger = logger;
            _authService = authService;
            _httpClient = httpClientFactory.CreateClient(nameof(ContainerAppPlugin));
            _logAnalyticsService = logAnalyticsService;
            _chatClient = chatClient;
        }

        public async Task<ContainerAppDescriptor> GetContainerAppInfoAsync(string resourceId)
        {
            _logger.LogInformation($"[get_container_app_info] Invoked with resourceId: {resourceId}");

            try
            {
                string cappResourceId = resourceId.ToLower().Replace("/", "_");

                string query = $@"
                    g.V().has('id', '{cappResourceId}')
                    .hasLabel('{Graph.Crawler.ARM.Constants.ContainerAppType.ToLower()}')
                    .project('properties')
                    .by(properties().group().by(key()).by(value()))
                    .select('properties')";

                var result = await _databaseClient.Query<Dictionary<string, object>>(query);

                if (result == null || !result.Any())
                {
                    _logger.LogWarning($"Container App with ID '{resourceId}' not found in graph database.");
                    return null;
                }

                return TranslateNodeToDescriptor(new ContainerAppNode(result.First()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetContainerAppInfoAsync with resourceId {resourceId}");
                return null;
            }
        }

        public async Task<IReadOnlyList<RevisionInfo>> ListContainerAppRevisionsAsync(string resourceId)
        {
            _logger.LogInformation($"[${nameof(ListContainerAppRevisionsAsync)}(resourceId: '{resourceId}')]");
            try
            {
                var cappResourceId = resourceId.ToLower().Replace("/", "_");

                var query = $@"
                    g.V().has('id', '{cappResourceId}')
                    .hasLabel('{Graph.Crawler.ARM.Constants.ContainerAppType.ToLower()}')
                    .outE('{Graph.Crawler.ARM.Constants.Relationships.RevisionOf}')
                    .inV()
                    .project('properties')
                    .by(properties().group().by(key()).by(value()))
                    .select('properties')";
                var result = await _databaseClient.Query<Dictionary<string, object>>(query);

                return result
                    .Select(r => new ContainerAppRevisionNode(r))
                    .Select(r => TranslateNodeToDescriptor(r))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in {nameof(ListContainerAppRevisionsAsync)}(resourceId: '{resourceId}'");
                return null;
            }
        }

        public async Task<RevisionInfo?> GetLatestRevisionAsync(string resourceId)
        {
            _logger.LogInformation($"[get_latest_revision] Invoked with resourceId: {resourceId}");

            try
            {
                var containerAppResource = _armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));

                var containerApp = await containerAppResource.GetAsync();

                string latestRevisionName = containerApp.Value.Data.LatestRevisionName;

                if (string.IsNullOrEmpty(latestRevisionName))
                {
                    _logger.LogWarning($"No latest revision name found for Container App {resourceId}");
                    return null;
                }

                string revisionName = latestRevisionName;
                if (latestRevisionName.Contains("--"))
                {
                    revisionName = latestRevisionName.Split("--").Last();
                }

                var revisions = containerAppResource.GetContainerAppRevisions();
                ContainerAppRevisionResource? latestRevision = null;

                await foreach (var revision in revisions.GetAllAsync())
                {
                    if (revision.Data.Name == latestRevisionName)
                    {
                        latestRevision = revision;
                        break;
                    }
                }

                if (latestRevision != null)
                {
                    int trafficWeight = latestRevision.Data.TrafficWeight ?? 0;
                    bool isActive = trafficWeight > 0;

                    return new RevisionInfo(
                        RevisionName: revisionName,
                        IsActive: isActive,
                        TrafficWeight: trafficWeight);
                }
                else
                {
                    return new RevisionInfo(
                        RevisionName: revisionName,
                        IsActive: true,
                        TrafficWeight: 100);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetLatestRevisionAsync with resourceId {resourceId}");
                return null;
            }
        }

        public async Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(Guid subscriptionId)
        {
            _logger.LogInformation($"[list_container_app_instances] Invoked with subscription {subscriptionId}");

            try
            {
                string query = $@"
                    g.V()
                    .has('subscriptionId', '{subscriptionId}')
                    .hasLabel('{Graph.Crawler.ARM.Constants.ContainerAppType.ToLower()}')
                    .project('properties')
                    .by(properties().group().by(key()).by(value()))
                    .select('properties')";

                var result = await _databaseClient.Query<Dictionary<string, object>>(query);

                if (result == null! || !result.Any())
                {
                    _logger.LogInformation(
                        "No container apps found for subscription {subscriptionId} in graph database.", subscriptionId);
                    return [];
                }

                return result
                    .Select(containerAppData => new ContainerAppNode(containerAppData))
                    .Select(a => TranslateNodeToDescriptor(a, limited: true))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ListContainerAppsAsync with subscription {subscriptionId}", subscriptionId);
                return [];
            }
        }
        public async Task<string> RestartContainerApp(
          [Description("The resource ID of the Container App.")]
            string appResourceId,
          [Description("Container App revision name to restart.")]
            string revisionName)
        {
            return await _armHelper.RestartContainerAppAsync(appResourceId, revisionName) ? "Restart succeeded" : "Restart failed";
        }

        public async Task<IReadOnlyList<RequestCountTimeSeriesData>> GetContainerAppRequestMetrics(string resourceId)
        {
            Console.WriteLine($"[get_containerapp_request_count_metrics] Invoked with resourceId: {resourceId}]");

            var metrics = new List<Metric>
            {
                new Metric { Name = "Requests", Unit = "Count", Aggregation = "Total" },
            };

            var metricsData = await _armHelper.FetchMetricsAsync(
                resourceId.ToString(),
                metrics);

            return metricsData
                 .Select(m => new RequestCountTimeSeriesData(
                     TimeStamp: m.Timestamp,
                     TotalRequestCount: m.Value))
                 .ToArray();
        }

        public async Task<IReadOnlyList<MemoryUsageTimeSeriesData>> GetContainerAppMemoryMetrics(string resourceId)
        {
            Console.WriteLine($"[get_containerapp_memory_metrics] Invoked with resourceId: {resourceId}]");

            var metrics = new List<Metric>
            {
                new Metric { Name = "MemoryPercentage", Unit = "Percentage", Aggregation = "Average" },
            };

            var metricsData = await _armHelper.FetchMetricsAsync(
                resourceId.ToString(),
                metrics);

            return metricsData
                .Select(m => new MemoryUsageTimeSeriesData(
                    TimeStamp: m.Timestamp,
                    Percent: m.Value))
                .ToArray();
        }

        public async Task<IReadOnlyList<CpuUsageTimeSeriesData>> GetContainerAppCpuMetrics(string resourceId)
        {
            Console.WriteLine($"[get_containerapp_cpu_metrics] Invoked with resourceId: {resourceId}]");

            var metrics = new List<Metric>
            {
                new Metric { Name = "CpuPercentage", Unit = "Percentage", Aggregation = "Average" },
            };

            var metricsData = await _armHelper.FetchMetricsAsync(
               resourceId.ToString(),
               metrics);

            return metricsData
                .Select(m => new CpuUsageTimeSeriesData(
                    TimeStamp: m.Timestamp,
                    Percent: m.Value))
                .ToArray();
        }

        public async Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetAllNSGRulesForContainerAppAsync(string resourceId)
        {
            _logger.LogInformation($"[get_containerapp_nsg_rules] Invoked with resourceId: {resourceId}");
            var result = new Dictionary<string, IReadOnlyList<SecurityRuleData>>();

            try
            {
                // Get the Container App to find its environment
                var containerAppResource = _armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                if (containerApp.Value.Data.ManagedEnvironmentId == null)
                {
                    _logger.LogWarning($"Container App {resourceId} does not have a managed environment ID");
                    return result;
                }

                // Get the Container App Environment
                var environment = _armClient.GetContainerAppManagedEnvironmentResource(containerApp.Value.Data.ManagedEnvironmentId);
                var environmentData = await environment.GetAsync();

                // Check if environment has VNet configuration with infrastructure subnet
                if (environmentData.Value.Data.VnetConfiguration == null ||
                    string.IsNullOrEmpty(environmentData.Value.Data.VnetConfiguration.InfrastructureSubnetId))
                {
                    _logger.LogWarning($"Container App Environment {environment.Id} does not have VNet configuration with infrastructure subnet");
                    return result;
                }

                // Get the infrastructure subnet
                string infrastructureSubnetId = environmentData.Value.Data.VnetConfiguration.InfrastructureSubnetId;
                var subnet = _armClient.GetSubnetResource(new ResourceIdentifier(infrastructureSubnetId));
                var subnetData = await subnet.GetAsync();

                // Check if subnet has NSG
                if (subnetData.Value.Data.NetworkSecurityGroup != null)
                {
                    string nsgId = subnetData.Value.Data.NetworkSecurityGroup.Id;
                    var nsg = _armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgId));
                    var nsgData = await nsg.GetAsync();

                    // Add this NSG's rules to the result dictionary
                    result[nsgId] = nsgData.Value.Data.SecurityRules.ToList();
                    _logger.LogInformation($"Found NSG {nsgId} with {nsgData.Value.Data.SecurityRules.Count} rules for infrastructure subnet");
                }
                else
                {
                    _logger.LogInformation($"No NSG found for infrastructure subnet {infrastructureSubnetId}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetAllNSGRulesForContainerAppAsync with resourceId {resourceId}");
                return result;
            }
        }

        public async Task<bool> ScaleContainerApp(string resourceId, string desiredMemory, int minReplicas, int maxReplicas)
        {
            _logger.LogInformation($"[scale_container_app] Invoked with resourceId: {resourceId}, memory: {desiredMemory}, minReplicas: {minReplicas}, maxReplicas: {maxReplicas}");

            try
            {
                // Dictionary of valid memory-to-CPU mappings
                var validCpuMemoryCombinations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    { "0.25Gi", 0.25 }, { "0.5Gi", 0.5 }, { "0.75Gi", 0.75 }, { "1Gi", 1.0 },
                    { "1.25Gi", 1.25 }, { "1.5Gi", 1.5 }, { "1.75Gi", 1.75 }, { "2Gi", 2.0 },
                    { "256Mi", 0.25 }, { "512Mi", 0.5 }, { "1024Mi", 1.0 }, { "2048Mi", 2.0 }
                };

                if (!validCpuMemoryCombinations.TryGetValue(desiredMemory, out double cpu))
                {
                    _logger.LogError($"Unsupported memory size: {desiredMemory}. Valid options include: 0.25Gi, 0.5Gi, 1Gi, 2Gi, 256Mi, 512Mi, etc.");
                    return false;
                }

                var credential = _authService.GetArmReadOperationCredential();
                var armClient = new ArmClient(credential);

                // Get the Container App
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerAppResponse = await containerAppResource.GetAsync();
                var containerApp = containerAppResponse.Value;

                if (containerApp.Data.Template?.Containers == null || containerApp.Data.Template.Containers.Count == 0)
                {
                    _logger.LogError($"No container definition found in the app {resourceId}");
                    return false;
                }

                // Patch request
                var containerAppUpdateData = containerApp.Data;

                // Update all containers' resources
                foreach (var container in containerAppUpdateData.Template.Containers)
                {
                    if (container.Resources == null)
                    {
                        _logger.LogInformation($"Creating new resources for container {container.Name}");
                        container.Resources = new AppContainerResources
                        {
                            Cpu = cpu,
                            Memory = desiredMemory
                        };
                    }
                    else
                    {
                        // Update existing resources
                        container.Resources.Cpu = cpu;
                        container.Resources.Memory = desiredMemory;
                    }
                }

                // Update scale settings
                if (containerAppUpdateData.Template.Scale == null)
                {
                    _logger.LogInformation("Creating new scale configuration");
                    containerAppUpdateData.Template.Scale = new ContainerAppScale
                    {
                        MinReplicas = minReplicas,
                        MaxReplicas = maxReplicas
                    };
                }
                else
                {
                    // Update existing scale settings
                    containerAppUpdateData.Template.Scale.MinReplicas = minReplicas;
                    containerAppUpdateData.Template.Scale.MaxReplicas = maxReplicas;
                }

                // Apply the update
                _logger.LogInformation("Applying container app scale update...");
                await containerAppResource.UpdateAsync(WaitUntil.Completed, containerAppUpdateData);

                _logger.LogInformation($"Successfully scaled container app {resourceId} to {cpu} vCPU / {desiredMemory} with min {minReplicas}, max {maxReplicas} replicas");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error scaling container app {resourceId}");
                return false;
            }
        }

        public async Task<string> GetContainerAppLogsAsync(string resourceId, string? revisionName = null)
        {
            _logger.LogInformation("GetRevisionLogsAsync(resourceId: {resourceId}, revisionName: {revisionName})", resourceId, revisionName);

            try
            {
                var containerApp = await _armClient.GetContainerAppResource(new ResourceIdentifier(resourceId)).GetAsync();
                if (!containerApp.HasValue)
                {
                    _logger.LogWarning("Container App with ID '{resourceId}' not found.", resourceId);
                    return string.Empty;
                }

                revisionName = NormalizeRevisionName(containerApp, revisionName);
                if (string.IsNullOrEmpty(revisionName))
                {
                    _logger.LogWarning("Revision name is null or empty.");
                    return string.Empty;
                }

                var streamToken = await containerApp.Value.GetAuthTokenAsync();
                if (!streamToken.HasValue)
                {
                    _logger.LogWarning("No auth token found for Container App {containerAppName}", containerApp.Value.Data.Name);
                    return string.Empty;
                }

                var logs = new
                {
                    system = (await new[]
                    {
                        GetStreamedSystemLogsAsync(containerApp, streamToken),
                        GetHistoricalLogsAsync(containerApp, revisionName, LogType.System)
                    }.IgnoreAndFilterFailures(_logger)).SelectMany(i => i),
                    console = (await new[]
                    {
                        GetStreamedConsoleLogsAsync(containerApp, streamToken, revisionName),
                        GetHistoricalLogsAsync(containerApp, revisionName, LogType.Application)
                    }.IgnoreAndFilterFailures(_logger)).SelectMany(i => i)
                };

                return await SummarizeLogs(JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetRevisionLogsAsync with resourceId {resourceId}, revisionName {revisionName}");
                return null;
            }
        }

        private async Task<string> SummarizeLogs(string logs)
        {
            _logger.LogInformation("Summarizing logs");
            const string prompt = $"Please summarize these application logs. " +
                                  $"This summary will be used to determine if there any potential issues with the application. " +
                                  $"Make sure it's complete, detailed, and references any particular numbers, error messages, error codes verbatim in case they are relevant for debugging" +
                                  $"Some Logs insights: \n" +
                                  $"A startup probe is just a check that the application is able to start successfully. Liveliness and readiness probes are checks that the application is running and able to serve traffic. " +
                                  $"Sometimes probes are misconfigured, but usually a probe failing means look elsewhere for the problem. " +
                                  $"Some problems include: Image pull errors, port mismatch, application startup errors/exceptions, timeouts, etc.";

            var messages = new []
            {
                new ChatMessage(ChatRole.System, prompt),
                new ChatMessage(ChatRole.User, logs)
            };

            var options = new ChatOptions
            {
                Temperature = (float)0.2,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };

            var response = await _chatClient.GetResponseAsync(messages, options);
            return response.Text;
        }

        public async Task<bool> UpdateTargetPort(string resourceId, int targetPort)
        {
            _logger.LogInformation("[UpdateTargetPort] Invoked with resourceId: {resourceId}, targetPort: {targetPort}", resourceId, targetPort);

            try
            {
                var containerAppResource = _armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                if (containerApp.Value.Data.Configuration?.Ingress == null)
                {
                    _logger.LogError($"No ingress configuration found in the app {resourceId}");
                    return false;
                }

                // Update the target port
                containerApp.Value.Data.Configuration.Ingress.TargetPort = targetPort;

                // Apply the update
                await containerAppResource.UpdateAsync(WaitUntil.Completed, containerApp.Value.Data);

                _logger.LogInformation($"Successfully updated target port of container app {resourceId} to {targetPort}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating target port for container app {resourceId}");
                return false;
            }
        }

        public IReadOnlyList<string> ListAvailableScalers()
        {
            var docsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "ContainerAppsAgent", "Docs", "scalers");
            return Directory
                .GetFiles(docsPath)
                .Where(f => f.EndsWith(".md"))
                .Select(Path.GetFileNameWithoutExtension)
                .Concat([
                    "http",
                    "tcp"
                ])
                .ToList()!;
        }

        public async Task<string> GetScalerDetails(string scalerName)
        {
            if (string.IsNullOrEmpty(scalerName))
            {
                return "Scaler name cannot be null or empty.";
            }

            if (string.Equals(scalerName, "http", StringComparison.OrdinalIgnoreCase))
            {
                return @"""
                    HTTP scaler is used to scale container apps based on HTTP request metrics. It allows you to define scaling rules based on the number of incoming requests.
                    The scale rule looks like
                    scale: {
                       minReplicas: 1
                       maxReplicas: 10
                       rules: [
                         {
                           http: {
                             name: my-http-rule
                             metadata: {
                               concurrentRequests: '10'
                             },
                           }
                         }
                       ]
                     }
                        """;
            }
            else if (string.Equals(scalerName, "tcp", StringComparison.OrdinalIgnoreCase))
            {
                return @"""
                    TCP scaler is used to scale container apps based on TCP request metrics. It allows you to define scaling rules based on the number of connections.
                    The scale rule looks like
                    scale: {
                       minReplicas: 1
                       maxReplicas: 10
                       rules: [
                         {
                           tcp: {
                             name: my-tcp-rule
                             metadata: {
                               concurrentConnections: '10'
                             },
                           }
                         }
                       ]
                     }
                        """;
            }

            var docsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "ContainerAppsAgent", "Docs", "scalers");
            var filePath = Path.Combine(docsPath, $"{scalerName}.md");

            if (!File.Exists(filePath))
            {
                return $"Scaler '{scalerName}' not found.";
            }

            return await File.ReadAllTextAsync(filePath);
        }

        public async Task<string> GetImageReferenceFromResourceId(string resourceId)
        {
            _logger.LogInformation($"Getting image reference for resource: {resourceId}");
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            try
            {
                return await GetContainerAppImageReference(resourceIdentifier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting image reference for resource {resourceId}");
                return null;
            }
        }

        public async Task<bool> VerifyExternalRegistryAsync(string resourceId, string imageReference)
        {
            _logger.LogInformation($"Verifying external registry connectivity for {resourceId} and image {imageReference}");

            try
            {
                if (imageReference.Contains(".azurecr.io", StringComparison.OrdinalIgnoreCase) ||
                    imageReference.Contains(".acr.io", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var registryType = DetermineRegistryType(imageReference);

                var registryHostname = ExtractRegistryHostname(imageReference);
                if (string.IsNullOrEmpty(registryHostname))
                {
                    return false;
                }

                // Check basic connectivity first
                var connectivityResult = await TestExternalRegistryConnectivity(registryHostname);
                if (!connectivityResult)
                {
                    _logger.LogWarning($"Basic connectivity test to registry {registryHostname} failed");
                    return false;
                }

                // Check for registry-specific issues
                switch (registryType)
                {
                    case RegistryType.DockerHub:
                        return await VerifyDockerHubRegistry(imageReference, resourceId);

                    case RegistryType.MicrosoftContainerRegistry:
                        return await VerifyMicrosoftContainerRegistry(imageReference, resourceId);

                    case RegistryType.GoogleContainerRegistry:
                        return await VerifyGoogleContainerRegistry(imageReference, resourceId);

                    case RegistryType.KubernetesRegistry:
                        // Kubernetes registry is generally public and doesn't require authentication
                        return true;

                    case RegistryType.Other:
                        return await VerifyOtherRegistry(imageReference, resourceId);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying external registry for image {imageReference}");
                return false;
            }
        }

        public async Task<bool> RollbackToLastWorkingImage(string resourceId)
        {
            _logger.LogInformation($"Rolling back to last known working image for resource: {resourceId}");

            try
            {
                return await RollbackContainerApp(resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rolling back resource {resourceId} to last working image");
                return false;
            }
        }

        public async Task<bool> UpdateContainerImage(string resourceId, string newImageReference, string containerName = null)
        {
            _logger.LogInformation($"Updating container image for resource: {resourceId} to {newImageReference}");

            try
            {
                return await UpdateContainerAppImage(resourceId, newImageReference, containerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating container image for resource {resourceId}");
                return false;
            }
        }

        private async Task<bool> UpdateContainerAppImage(string resourceId, string newImageReference, string containerName = null)
        {
            try
            {
                // Get the Container App resource
                var containerAppResource = _armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                // Create a data object for the update
                ContainerAppData updateData = new ContainerAppData(containerApp.Value.Data.Location)
                {
                    Template = containerApp.Value.Data.Template
                };

                // Check if we have containers in the template
                if (updateData.Template?.Containers == null || updateData.Template.Containers.Count == 0)
                {
                    return false;
                }

                // Update specific container by name if provided, otherwise update the first container
                var containerToUpdate = string.IsNullOrEmpty(containerName)
                    ? updateData.Template.Containers[0]
                    : updateData.Template.Containers.FirstOrDefault(c => c.Name == containerName);

                if (containerToUpdate == null)
                {
                    return false;
                }

                // Update the image reference
                containerToUpdate.Image = newImageReference;

                // Update the Container App with the new template
                _logger.LogInformation($"Updating Container App {resourceId} with new image: {newImageReference}");
                var updateOperation = await containerAppResource.UpdateAsync(
                    WaitUntil.Completed, // Specify the wait behavior (e.g., WaitUntil.Completed or WaitUntil.Started)
                    updateData,          // The ContainerAppData object to update
                    CancellationToken.None // Provide a CancellationToken (use CancellationToken.None if no cancellation is needed)
                );
                var updatedApp = updateOperation.Value;

                _logger.LogInformation($"Successfully updated Container App {resourceId} to image: {newImageReference}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Container App {resourceId}");
                return false;
            }
        }

        private async Task<bool> RollbackContainerApp(string resourceId)
        {
            try
            {
                // Get the Container App resource
                var containerAppResource = _armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                // Get all revisions for this Container App
                var revisions = await containerAppResource.GetContainerAppRevisions().ToListAsync();

                // Sort revisions by created time in descending order (newest first)
                revisions = revisions
                    .OrderByDescending(r => r.Data.CreatedOn)
                    .ToList();

                // We need at least 2 revisions to perform a rollback
                if (revisions.Count < 2)
                {
                    return false;
                }

                // Get current active revision name
                string currentRevisionName = containerApp.Value.Data.LatestRevisionName;

                // Find the most recent inactive revision that is not the current one and is in a "Ready" state
                var targetRevision = revisions
                    .Where(r => r.Data.Name != currentRevisionName)
                    .Where(r => r.Data.ProvisioningState == ContainerAppRevisionProvisioningState.Provisioned)
                    .FirstOrDefault();

                if (targetRevision == null)
                {
                    return false;
                }

                // Find the image reference in the target revision
                string? targetImageReference = null;
                if (targetRevision.Data.Template?.Containers != null && targetRevision.Data.Template.Containers.Count > 0)
                {
                    targetImageReference = targetRevision.Data.Template.Containers[0].Image;
                }

                if (string.IsNullOrEmpty(targetImageReference))
                {
                    return false;
                }

                // Create a data object for the update
                ContainerAppData updateData = new ContainerAppData(containerApp.Value.Data.Location)
                {
                    Template = containerApp.Value.Data.Template
                };

                // Update image in the template containers
                if (updateData.Template?.Containers != null && updateData.Template.Containers.Count > 0)
                {
                    updateData.Template.Containers[0].Image = targetImageReference;
                }
                else
                {
                    return false;
                }

                // Update the Container App with the new template
                _logger.LogInformation($"Updating Container App {resourceId} with previous working image: {targetImageReference}");
                var updateOperation = await containerAppResource.UpdateAsync(
                   WaitUntil.Completed, // Specify the wait behavior (e.g., WaitUntil.Completed or WaitUntil.Started)
                   updateData,          // The ContainerAppData object to update
                   CancellationToken.None // Provide a CancellationToken (use CancellationToken.None if no cancellation is needed)
                );
                var updatedApp = updateOperation.Value;

                _logger.LogInformation($"Successfully rolled back Container App {resourceId} to image: {targetImageReference}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rolling back Container App {resourceId}");
                return false;
            }
        }

        private RegistryType DetermineRegistryType(string imageReference)
        {
            if (string.IsNullOrEmpty(imageReference))
            {
                throw new ArgumentException("Image reference cannot be null or empty.", nameof(imageReference));
            }

            if (imageReference.Contains("docker.io", StringComparison.OrdinalIgnoreCase))
            {
                return RegistryType.DockerHub;
            }
            else if (imageReference.Contains("gcr.io", StringComparison.OrdinalIgnoreCase))
            {
                return RegistryType.GoogleContainerRegistry;
            }
            else if (imageReference.Contains("mcr.microsoft.com", StringComparison.OrdinalIgnoreCase))
            {
                return RegistryType.MicrosoftContainerRegistry;
            }
            else if (imageReference.Contains("k8s.gcr.io", StringComparison.OrdinalIgnoreCase))
            {
                return RegistryType.KubernetesRegistry;
            }
            else
            {
                return RegistryType.Other;
            }
        }

        private async Task<bool> VerifyDockerHubRegistry(string imageReference, string resourceId)
        {
            try
            {
                // Verify the image exists
                var (repo, tag) = ExtractDockerHubRepositoryAndTag(imageReference);
                if (string.IsNullOrEmpty(repo))
                {
                    return false;
                }

                // Check if we can access the image manifest
                var token = await GetDockerOAuthTokenAsync(repo);
                var manifestUrl = $"https://registry-1.docker.io/v2/{repo}/manifests/{tag}";
                var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");

                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (response.Headers.Contains("X-RateLimit-Remaining"))
                    {
                        var rateLimitRemaining = response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
                        var rateLimitLimit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
                        var rateLimitReset = response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault();

                        _logger.LogInformation($"Rate Limit Remaining: {rateLimitRemaining}/{rateLimitLimit}, Reset Time: {rateLimitReset}");

                        // If remaining requests are 0, handle rate limiting
                        if (int.TryParse(rateLimitRemaining, out int remaining) && remaining == 0)
                        {
                            _logger.LogWarning("Rate limit exceeded. Please wait until the limit resets.");
                            return false;
                        }
                    }

                    // Fallback to Retry-After logic if needed
                    if (response.Headers.TryGetValues("Retry-After", out var values))
                    {
                        var retryAfter = values.FirstOrDefault();
                        if (retryAfter != null && int.TryParse(retryAfter, out int seconds))
                        {
                            _logger.LogWarning($"Rate limit exceeded. Retry after {seconds} seconds.");
                            return false;
                        }
                    }
                }

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return true;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying Docker Hub registry for {imageReference}");
                return false;
            }
        }

        private (string Repository, string Tag) ExtractDockerHubRepositoryAndTag(string imageReference)
        {
            if (string.IsNullOrWhiteSpace(imageReference))
            {
                throw new ArgumentException("Image reference cannot be null or empty.", nameof(imageReference));
            }

            // Normalize the input by removing any double slashes
            imageReference = imageReference.Replace("//", "/");

            var dockerImageRegex = new Regex(
                @"^(?:(?<registry>[^/]+(?:\.[^/]+)+(?:[:]\d+)?)/)?(?<repository>[^:]+)(?::(?<tag>[\w.-]+))?$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

            var match = dockerImageRegex.Match(imageReference);

            if (!match.Success)
            {
                throw new ArgumentException("Invalid Docker image reference format.", nameof(imageReference));
            }

            string repository = match.Groups["repository"].Value;
            string tag = match.Groups["tag"].Success ? match.Groups["tag"].Value : "latest"; // Default tag is 'latest'

            return (repository, tag);
        }

        private async Task<bool> VerifyMicrosoftContainerRegistry(string imageReference, string resourceId)
        {
            try
            {
                var (repo, tag) = ExtractRepositoryAndTag(imageReference);
                if (string.IsNullOrEmpty(repo))
                {
                    return false;
                }

                var manifestUrl = $"https://mcr.microsoft.com/v2/{repo}/manifests/{tag}";
                var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogInformation($"Successfully verified MCR image: {imageReference}");
                    return true;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Image {imageReference} not found in Microsoft Container Registry");
                    return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying Microsoft Container Registry for {imageReference}");
                return false;
            }
        }

        private async Task<bool> VerifyGoogleContainerRegistry(string imageReference, string resourceId)
        {
            try
            {
                var (repo, tag) = ExtractRepositoryAndTag(imageReference);
                if (string.IsNullOrEmpty(repo))
                {
                    return false;
                }

                if (repo.StartsWith("gcr.io/", StringComparison.OrdinalIgnoreCase))
                {
                    repo = repo.Substring("gcr.io/".Length);
                }

                var manifestUrl = $"https://gcr.io/v2/{repo}/manifests/{tag}";
                var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                request.Headers.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogInformation($"Successfully verified GCR image: {imageReference}");
                    return true;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Image {imageReference} not found in Google Container Registry");
                    return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying Google Container Registry for {imageReference}");
                return false;
            }
        }

        private async Task<bool> VerifyOtherRegistry(string imageReference, string resourceId)
        {
            try
            {
                var registryHostname = ExtractRegistryHostname(imageReference);
                if (string.IsNullOrEmpty(registryHostname))
                {
                    _logger.LogWarning($"Could not extract registry hostname from {imageReference}");
                    return false;
                }

                var containerAppResource = _armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                bool hasRegistryCredentials = false;
                if (containerApp.Value.Data.Configuration?.Registries != null)
                {
                    hasRegistryCredentials = containerApp.Value.Data.Configuration.Registries
                        .Any(r => !string.IsNullOrEmpty(r.Server) &&
                                 r.Server.Equals(registryHostname, StringComparison.OrdinalIgnoreCase));
                }

                try
                {
                    var manifestUrl = $"https://{registryHostname}/v2/";
                    var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);

                    var response = await _httpClient.SendAsync(request);

                    // 200 OK means the registry is accessible and doesn't require auth for basic API access
                    // 401 Unauthorized is also acceptable as it confirms the registry exists but needs auth
                    if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogInformation($"Successfully verified registry API accessibility for: {registryHostname}");
                        return true;
                    }

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning($"Registry API endpoint not found at {manifestUrl}. The registry may not implement the Docker Registry HTTP API V2.");
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, $"Error connecting to registry API for {registryHostname}. This may be due to network restrictions or registry configuration.");
                }

                // If container app has registry credentials, assume the registry is accessible
                // Even if the API check failed, the credentials configuration suggests intentional use
                return hasRegistryCredentials;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying private registry for {imageReference}");
                return false;
            }
        }

        private async Task<string> GetDockerOAuthTokenAsync(string repo)
        {
            var authUrl = "https://auth.docker.io/token";
            var authRequest = new HttpRequestMessage(HttpMethod.Get, $"{authUrl}?service=registry.docker.io&scope=repository:{repo}:pull");

            var authResponse = await _httpClient.SendAsync(authRequest);

            if (authResponse.IsSuccessStatusCode)
            {
                var authResponseBody = await authResponse.Content.ReadAsStringAsync();
                var token = JsonSerializer.Deserialize<JsonElement>(authResponseBody).GetProperty("token").GetString();
                return token;
            }

            return null;
        }


        private (string Repository, string Tag) ExtractRepositoryAndTag(string imageReference)
        {
            if (string.IsNullOrWhiteSpace(imageReference))
            {
                throw new ArgumentException("Image reference cannot be null or empty.", nameof(imageReference));
            }

            // Normalize the input by removing any double slashes
            imageReference = imageReference.Replace("//", "/");

            var dockerImageRegex = new Regex(
                @"^(?:(?<registry>[^/]+(?:\.[^/]+)+(?:[:]\d+)?)/)?(?<repository>[^:]+)(?::(?<tag>[\w.-]+))?$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

            var match = dockerImageRegex.Match(imageReference);

            if (!match.Success)
            {
                throw new ArgumentException("Invalid Docker image reference format.", nameof(imageReference));
            }

            string repository = match.Groups["repository"].Value;
            string tag = match.Groups["tag"].Success ? match.Groups["tag"].Value : "latest"; // Default tag is 'latest'

            return (repository, tag);
        }

        enum LogType
        {
            System,
            Application,
        }

        private async Task<IEnumerable<string>> GetStreamedSystemLogsAsync(
            ContainerAppResource containerApp,
            Response<ContainerAppAuthToken> streamToken)
        {
            var eventsStreamUrl = containerApp.Data.EventStreamEndpoint;
            try
            {
                return await this.GetLogsAsync(eventsStreamUrl.ToString(), streamToken, StreamEndpointType.EventStream);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get logs from {eventsStreamUrl}.", eventsStreamUrl);
                return [];
            }
        }

        private async Task<IEnumerable<string>> GetStreamedConsoleLogsAsync(
            ContainerAppResource containerApp,
            ContainerAppAuthToken streamToken,
            string revisionName)
        {
            // Get container App revision
            var revision = await containerApp.GetContainerAppRevisionAsync(revisionName);
            if (!revision.HasValue)
            {
                _logger.LogWarning("Revision {revisionName} not found for Container App {containerAppName}", revisionName, containerApp.Data.Name);
                return [];
            }

            // Get revision instances
            var replicas = await _armHelper.GetRevisionReplicas(revision.Value.Id.ToString());

            var logs = await replicas
                .Select(r => r?.Properties?.InitContainers?.Concat(r?.Properties?.Containers ?? []) ?? [])
                .SelectMany(c => c)
                .Select(c => this.GetLogsAsync(c.LogStreamEndpoint, streamToken, StreamEndpointType.LogStream))
                .IgnoreAndFilterFailures(_logger) ?? [];

            return logs.SelectMany(l => l).ToList();
        }

        private enum StreamEndpointType
        {
            LogStream,
            EventStream
        }

        private async Task<IEnumerable<string>> GetLogsAsync(string? argLogStreamEndpoint, ContainerAppAuthToken streamToken, StreamEndpointType streamType = StreamEndpointType.LogStream)
        {
            if (string.IsNullOrEmpty(argLogStreamEndpoint))
            {
                return [];
            }

            var logStreamEndpoint = streamType == StreamEndpointType.EventStream
                    ? new Uri($"{argLogStreamEndpoint}?follow=false&output=json&tailLines=50")
                    : new Uri($"{argLogStreamEndpoint}?follow=false&output=text&tailLines=50");

            var request = new HttpRequestMessage(HttpMethod.Get, logStreamEndpoint);
            request.Headers.Add("Authorization", $"Bearer {streamToken.Token}");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                if (streamType == StreamEndpointType.EventStream)
                {
                    try
                    {
                        await using var stream = await response.Content.ReadAsStreamAsync();
                        var logNodes =
                            JsonSerializer.DeserializeAsyncEnumerable<JsonNode>(stream, topLevelValues: true);

                        return await logNodes
                            .Select(n => n?["Msg"]?.ToString() ?? "")
                            .Where(m => !string.IsNullOrEmpty(m))
                            .Distinct()
                            .ToListAsync();
                    }
                    catch (JsonException e)
                    {
                        _logger.LogError(e, "Failed to deserialize JSON from {logStreamEndpoint}", logStreamEndpoint);
                        return [];
                    }
                }

                var content = await response.Content.ReadAsStringAsync();
                return content.Split("\n");
            }
            else
            {
                _logger.LogError("Failed to get logs from {logStreamEndpoint}. Status code: {StatusCode}", logStreamEndpoint, response.StatusCode);
                return [];
            }
        }

        private async Task<IEnumerable<string>> GetHistoricalLogsAsync(
            ContainerAppResource containerApp,
            string revisionName,
            LogType logType)
        {
            var workspaceId = await GetContainerAppWorkspaceIdAsync(containerApp);
            if (string.IsNullOrEmpty(workspaceId))
            {
                _logger.LogWarning("No workspace ID found for Container App {containerAppName}", containerApp.Data.Name);
                return ["ContainerApp is missing a Log Analytics workspace ID for historical logs"];
            }

            var startTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(2));
            var endTime = DateTimeOffset.UtcNow;
            var logAnalyticsLogs = logType switch
            {
                LogType.System => await _logAnalyticsService.GetContainerAppSystemLogsAsync(
                    workspaceId,
                    containerAppName: containerApp.Data.Name,
                    startTime,
                    endTime,
                    revisionName),
                LogType.Application => await _logAnalyticsService.GetContainerAppApplicationLogsAsync(
                    workspaceId,
                    containerAppName: containerApp.Data.Name,
                    startTime,
                    endTime,
                    revisionName),
                _ => throw new ArgumentOutOfRangeException(nameof(logType), logType, null)
            };

            if (logAnalyticsLogs == null! || logAnalyticsLogs.Count == 0)
            {
                _logger.LogWarning("No logs found for Container App {containerAppName} in the last 2 hours", containerApp.Data.Name);
                return [];
            }

            return logAnalyticsLogs
                .Select(log => $"[{log.TimeGenerated}] {log.Log}")
                .ToList();
        }

        private async Task<string> GetContainerAppWorkspaceIdAsync(ContainerAppResource containerApp)
        {
            var environment = await _armClient.GetContainerAppManagedEnvironmentResource(containerApp.Data.EnvironmentId).GetAsync();
            if (!environment.HasValue)
            {
                return string.Empty;
            }

            var workspaceId = environment.Value.Data.AppLogsConfiguration?.LogAnalyticsConfiguration?.CustomerId;
            return workspaceId ?? string.Empty;
        }

        private static string? NormalizeRevisionName(ContainerAppResource containerApp, string? revisionName)
        {
            var result = revisionName;
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }

            result = containerApp.Data.LatestRevisionName;

            return result;
        }

        private static RevisionInfo TranslateNodeToDescriptor(ContainerAppRevisionNode revisionNode, bool limited = false)
        {
            var revisionInfo = new RevisionInfo(
                IsActive: revisionNode.IsActive ?? false,
                RevisionName: revisionNode.Name ?? string.Empty,
                TrafficWeight: revisionNode.TrafficWeight ?? 0);

            if (!limited)
            {
                revisionInfo = revisionInfo with
                {
                    CreatedOn = revisionNode.CreatedOn ?? string.Empty,
                    LastActiveOn = revisionNode.LastActiveOn ?? string.Empty,
                    Fqdn = revisionNode.Fqdn ?? string.Empty,
                    Labels = string.Join(",", revisionNode.Labels),
                    ProvisioningError = revisionNode.ProvisioningError ?? string.Empty,
                    HealthState = revisionNode.HealthState ?? string.Empty,
                    ProvisioningState = revisionNode.ProvisioningState ?? string.Empty,
                    RunningState = revisionNode.RunningState ?? string.Empty,
                };
            }

            return revisionInfo;
        }

        private static ContainerAppDescriptor TranslateNodeToDescriptor(ContainerAppNode containerApp, bool limited = false)
        {
            var result = new ContainerAppDescriptor(
                ResourceId: containerApp.ResourceId,
                Name: containerApp.ResourceName,
                Location: containerApp.Location,
                WorkloadProfile: containerApp.WorkloadProfileName ?? string.Empty,
                State: containerApp.ProvisioningState ?? string.Empty,
                ResourceGroup: containerApp.ResourceGroupName,
                EnvironmentId: containerApp.EnvironmentId ?? string.Empty,
                Configurations: null,
                Containers: [],
                InitContainers: [],
                Revisions: null); // Not including revisions for now

            if (!limited)
            {
                result = result with
                {
                    Configurations = new ContainerAppConfigurations(
                        RevisionMode: containerApp.ActiveRevisionMode ?? string.Empty,
                        Ingress: new IngressConfiguration(
                            IsExternal: containerApp.External ?? false,
                            TargetPort: containerApp.TargetPort ?? 80,
                            Transport: containerApp.Transport ?? string.Empty,
                            Hostnames: containerApp.HostNames.ToArray(),
                            Traffic: containerApp.Traffic?
                                .Select(t => new TrafficConfiguration(
                                    RevisionName: t.RevisionName ?? string.Empty,
                                    Weight: t.Weight,
                                    Label: t.Label ?? string.Empty,
                                    LatestRevision: t.LatestRevision))
                                .ToArray() ?? []),
                        Registries: containerApp.Registries
                            .Select(r => new Registry(
                                Server: r.Server ?? string.Empty,
                                Username: r.Username ?? string.Empty,
                                PasswordSecretRef: r.PasswordSecretRef ?? string.Empty,
                                Identity: r.Identity ?? string.Empty))
                            .ToArray()),
                    Containers = containerApp.Containers
                        .Select(c => new Models.Container(
                            Name: c.Name,
                            Image: c.Image ?? string.Empty,
                            Cpu: c.Cpu ?? string.Empty,
                            Memory: c.Memory ?? string.Empty))
                        .ToArray(),
                    InitContainers = containerApp.InitContainers
                        .Select(c => new Models.Container(
                            Name: c.Name,
                            Image: c.Image ?? string.Empty,
                            Cpu: c.Cpu ?? string.Empty,
                            Memory: c.Memory ?? string.Empty))
                        .ToArray(),
                    AppHealthInfo = containerApp.AppHealthInfo,
                };
            }
            else
            {
                result = result with
                {
                    Containers = containerApp.Containers
                        .Select(c => new Models.Container(
                            Name: c.Name,
                            Image: c.Image ?? string.Empty,
                            Cpu: c.Cpu ?? string.Empty,
                            Memory: c.Memory ?? string.Empty))
                        .ToArray(),
                };
            }

            return result;
        }


        private string ExtractRegistryHostname(string imageReference)
        {
            if (string.IsNullOrEmpty(imageReference))
                return string.Empty;

            try
            {
                // Split on first slash to get potential hostname
                var slashIndex = imageReference.IndexOf('/');
                if (slashIndex > 0)
                {
                    var possibleHostname = imageReference.Substring(0, slashIndex);

                    // If it contains a dot, it's likely a hostname
                    if (possibleHostname.Contains('.'))
                    {
                        return possibleHostname;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting registry hostname from {imageReference}");
                return string.Empty;
            }
        }

        private async Task<bool> TestExternalRegistryConnectivity(string hostname)
        {
            try
            {
                // Try HTTPS first
                var httpsUrl = $"https://{hostname}/v2/";
                var request = new HttpRequestMessage(HttpMethod.Head, httpsUrl);

                try
                {
                    var httpResponse = await _httpClient.SendAsync(request);
                    if (httpResponse.IsSuccessStatusCode || httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogInformation($"Successfully connected to registry {hostname} via HTTPS");
                        return true;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, $"HTTPS connection failed to {hostname}");
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error testing connectivity to external registry: {hostname}");
                return false;
            }
        }

        private async Task<string> GetContainerAppImageReference(ResourceIdentifier resourceId)
        {
            var containerAppResource = _armClient.GetContainerAppResource(resourceId);
            var containerApp = await containerAppResource.GetAsync();
            string latestRevisionName = containerApp.Value.Data.LatestRevisionName;

            // If we have a latest revision name, get that revision specifically
            if (!string.IsNullOrEmpty(latestRevisionName))
            {
                string revisionResourceId = $"{resourceId}/revisions/{latestRevisionName}";
                try
                {
                    var revisionResource = _armClient.GetContainerAppRevisionResource(new ResourceIdentifier(revisionResourceId));
                    var revision = await revisionResource.GetAsync();

                    // Get the container image from the revision
                    if (revision.Value.Data.Template?.Containers != null &&
                        revision.Value.Data.Template.Containers.Count > 0)
                    {
                        return revision.Value.Data.Template.Containers[0].Image;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Could not retrieve latest revision {latestRevisionName} for app {resourceId}, falling back to template");
                }
            }
            // Fall back to the template if available
            if (containerApp.Value.Data.Template?.Containers != null &&
                        containerApp.Value.Data.Template.Containers.Count > 0)
            {
                return containerApp.Value.Data.Template.Containers[0].Image;
            }

            return null;
        }
    }
}
