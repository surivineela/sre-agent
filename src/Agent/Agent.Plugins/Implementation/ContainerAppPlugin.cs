// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.JsonConverters;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
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
        private readonly IGraphDBPlugin _graphDbPlugin;
        private readonly ILogger<ContainerAppPlugin> _logger;
        private readonly IAuthenticationService _authService;
        private readonly ILogAnalyticsService _logAnalyticsService;
        private readonly IChatClient _chatClient;
        private readonly IArmClientFactory _armClientFactory;
        private readonly IHttpClientFactory _httpClientFactory;

        // ContainerAppData from Azure SDK can't be serialized directly to JSON because it contains
        // IPAddress and IPEndPoint properties, which throw ISocketException when serialized.
        // So we need to create a custom serialization options with custom converters for these types.
        private static readonly JsonSerializerOptions _containerAppSerializationOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new IPEndPointConverter(),
                new IPAddressConverter(),
            }
        };

        public ContainerAppPlugin(ArmHelper armHelper,
            IGraphDatabaseClient graphDbClient,
            IGraphDBPlugin graphDBPlugin,
            ILogger<ContainerAppPlugin> logger,
            IArmClientFactory armClientFactory,
            IAuthenticationService authService,
            IHttpClientFactory httpClientFactory,
            ILogAnalyticsService logAnalyticsService,
            IChatClient chatClient)
        {
            _databaseClient = graphDbClient;
            _graphDbPlugin = graphDBPlugin;
            _armHelper = armHelper;
            _logger = logger;
            _authService = authService;
            _logAnalyticsService = logAnalyticsService;
            _chatClient = chatClient;
            _armClientFactory = armClientFactory;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> GetContainerAppInfoAsync(string resourceId)
        {
            _logger.LogInternalInformation($"[get_container_app_info] Invoked with resourceId: {resourceId}");
            var getDeploymentTimes = await GetDeploymentTimes(resourceId);

            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));

                var containerApp = await containerAppResource.GetAsync();
                return JsonSerializer.Serialize(containerApp.Value.Data, _containerAppSerializationOptions);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetContainerAppInfoAsync with resourceId {resourceId}");
                return string.Empty;
            }
        }

        public async Task<IReadOnlyList<RevisionInfo>> ListContainerAppRevisionsAsync(string resourceId)
        {
            _logger.LogInternalInformation($"[${nameof(ListContainerAppRevisionsAsync)}(resourceId: '{resourceId}')]");
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
                _logger.LogInternalError(ex, $"Error in {nameof(ListContainerAppRevisionsAsync)}(resourceId: '{resourceId}'");
                return null;
            }
        }

        public async Task<RevisionInfo?> GetLatestRevisionAsync(string resourceId)
        {
            _logger.LogInternalInformation($"[get_latest_revision] Invoked with resourceId: {resourceId}");

            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));

                var containerApp = await containerAppResource.GetAsync();

                string latestRevisionName = containerApp.Value.Data.LatestRevisionName;

                if (string.IsNullOrEmpty(latestRevisionName))
                {
                    _logger.LogInternalWarning($"No latest revision name found for Container App {resourceId}");
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
                _logger.LogInternalError(ex, $"Error in GetLatestRevisionAsync with resourceId {resourceId}");
                return null;
            }
        }

        public async Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(Guid subscriptionId)
        {
            _logger.LogInternalInformation($"[list_container_app_instances] Invoked with subscription {subscriptionId}");

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
                    _logger.LogInternalInformation(
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
                _logger.LogInternalError(ex, "Error in ListContainerAppsAsync with subscription {subscriptionId}", subscriptionId);
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
            _logger.LogInternalInformation($"[get_containerapp_memory_metrics] Invoked with resourceId: {resourceId}]");

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
            _logger.LogInternalInformation($"[get_containerapp_nsg_rules] Invoked with resourceId: {resourceId}");
            var result = new Dictionary<string, IReadOnlyList<SecurityRuleData>>();

            try
            {
                // Get the Container App to find its environment
                var armClient = await _armClientFactory.GetArmOperationClient();
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                if (containerApp.Value.Data.ManagedEnvironmentId == null)
                {
                    _logger.LogInternalWarning($"Container App {resourceId} does not have a managed environment ID");
                    return result;
                }

                // Get the Container App Environment
                var environment = armClient.GetContainerAppManagedEnvironmentResource(containerApp.Value.Data.ManagedEnvironmentId);
                var environmentData = await environment.GetAsync();

                // Check if environment has VNet configuration with infrastructure subnet
                if (environmentData.Value.Data.VnetConfiguration == null ||
                    string.IsNullOrEmpty(environmentData.Value.Data.VnetConfiguration.InfrastructureSubnetId))
                {
                    _logger.LogInternalWarning($"Container App Environment {environment.Id} does not have VNet configuration with infrastructure subnet");
                    return result;
                }

                // Get the infrastructure subnet
                string infrastructureSubnetId = environmentData.Value.Data.VnetConfiguration.InfrastructureSubnetId;
                var subnet = armClient.GetSubnetResource(new ResourceIdentifier(infrastructureSubnetId));
                var subnetData = await subnet.GetAsync();

                // Check if subnet has NSG
                if (subnetData.Value.Data.NetworkSecurityGroup != null)
                {
                    string nsgId = subnetData.Value.Data.NetworkSecurityGroup.Id;
                    var nsg = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgId));
                    var nsgData = await nsg.GetAsync();

                    // Add this NSG's rules to the result dictionary
                    result[nsgId] = nsgData.Value.Data.SecurityRules.ToList();
                    _logger.LogInternalInformation($"Found NSG {nsgId} with {nsgData.Value.Data.SecurityRules.Count} rules for infrastructure subnet");
                }
                else
                {
                    _logger.LogInternalInformation($"No NSG found for infrastructure subnet {infrastructureSubnetId}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetAllNSGRulesForContainerAppAsync with resourceId {resourceId}");
                return result;
            }
        }

        public async Task<bool> ScaleContainerApp(string resourceId, string desiredMemory, int minReplicas, int maxReplicas)
        {
            _logger.LogInternalInformation($"[scale_container_app] Invoked with resourceId: {resourceId}, memory: {desiredMemory}, minReplicas: {minReplicas}, maxReplicas: {maxReplicas}");

            try
            {
                // Dictionary of valid memory-to-CPU mappings
                var validCpuMemoryCombinations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    { "0.25Gi", 0.25 }, { "0.5Gi", 0.5 }, { "0.75Gi", 0.75 }, { "1Gi", 1.0 },
                    { "1.25Gi", 1.25 }, { "1.5Gi", 1.5 }, { "1.75Gi", 1.75 }, { "2Gi", 1.0 },
                    { "256Mi", 0.25 }, { "512Mi", 0.5 }, { "1024Mi", 1.0 }, { "2048Mi", 2.0 }
                };

                if (!validCpuMemoryCombinations.TryGetValue(desiredMemory, out double cpu))
                {
                    _logger.LogInternalError($"Unsupported memory size: {desiredMemory}. Valid options include: 0.25Gi, 0.5Gi, 1Gi, 2Gi, 256Mi, 512Mi, etc.");
                    return false;
                }

                var armClient = await _armClientFactory.GetArmOperationClient();

                // Get the Container App
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerAppResponse = await containerAppResource.GetAsync();
                var containerApp = containerAppResponse.Value;

                if (containerApp.Data.Template?.Containers == null || containerApp.Data.Template.Containers.Count == 0)
                {
                    _logger.LogInternalError($"No container definition found in the app {resourceId}");
                    return false;
                }

                // Patch request
                var containerAppUpdateData = containerApp.Data;
                var secrets = containerApp.Data.Configuration.Secrets;
                await foreach(var v in containerAppResource.GetSecretsAsync())
                {
                    var secret = secrets.FirstOrDefault(secrets => secrets.Name == v.Name); 
                    if (secret != null)
                    {
                        secret.KeyVaultUri = v.KeyVaultUri;
                        secret.Value = v.Value;
                        secret.Identity = v.Identity;
                    }
                }

                // Update all containers' resources
                foreach (var container in containerAppUpdateData.Template.Containers)
                {
                    if (container.Resources == null)
                    {
                        _logger.LogInternalInformation($"Creating new resources for container {container.Name}");
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
                    _logger.LogInternalInformation("Creating new scale configuration");
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

                // Apply the update 1. List Secrets
                _logger.LogInternalInformation("Applying container app scale update...");
                await containerAppResource.UpdateAsync(WaitUntil.Completed, containerAppUpdateData);

                _logger.LogInternalInformation($"Successfully scaled container app {resourceId} to {cpu} vCPU / {desiredMemory} with min {minReplicas}, max {maxReplicas} replicas");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error scaling container app {resourceId}");
                return false;
            }
        }

        public async Task<string> GetContainerAppLogsAsync(string resourceId, string? revisionName = null)
        {
            _logger.LogInternalInformation("GetRevisionLogsAsync(resourceId: {resourceId}, revisionName: {revisionName})", resourceId, revisionName);

            try
            {
                return await GetContainerAppLogs(resourceId, true, revisionName);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetRevisionLogsAsync with resourceId {resourceId}, revisionName {revisionName}");
                return null;
            }
        }

        private async Task<string> GetContainerAppLogs(string resourceId, bool summarizeLogs, string? revisionName = null)
        {
            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                var containerApp = await armClient.GetContainerAppResource(new ResourceIdentifier(resourceId)).GetAsync();
                if (!containerApp.HasValue)
                {
                    _logger.LogInternalWarning("Container App with ID '{resourceId}' not found.", resourceId);
                    return string.Empty;
                }

                revisionName = NormalizeRevisionName(containerApp, revisionName);
                if (string.IsNullOrEmpty(revisionName))
                {
                    _logger.LogInternalWarning("Revision name is null or empty.");
                    return string.Empty;
                }

                var streamToken = await containerApp.Value.GetAuthTokenAsync();
                if (!streamToken.HasValue)
                {
                    _logger.LogInternalWarning("No auth token found for Container App {containerAppName}", containerApp.Value.Data.Name);
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

                var logsJson = JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });

                if (!summarizeLogs)
                {
                    return logsJson;
                }

                return await SummarizeLogs(logsJson);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in GetContainerAppLogs with resourceId {resourceId}, revisionName {revisionName}");
                return null;
            }
        }

        private async Task<string> SummarizeLogs(string logs)
        {
            _logger.LogInternalInformation("Summarizing logs");
            const string prompt = $"Please summarize these application logs. " +
                                  $"This summary will be used to determine if there any potential issues with the application. " +
                                  $"Make sure it's complete, detailed, and references any particular numbers, error messages, error codes verbatim in case they are relevant for debugging" +
                                  $"Some Logs insights: \n" +
                                  $"A startup probe is just a check that the application is able to start successfully. " +
                                  $"Transient failures during startup are expected and acceptable since http server can take time to be responsive. Unless persistent probe failures lead to degraded app state, revision provisioning fails then probes need to be looked at. Liveliness and readiness probes are checks that the application is running and able to serve traffic. " +
                                  $"Sometimes probes are misconfigured, but usually a probe failing means look elsewhere for the problem. " +
                                  $"Some problems include: Image pull errors, port mismatch, application startup errors/exceptions, timeouts, etc. Include full stack traces for the logs";

            var messages = new[]
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
            _logger.LogInternalInformation("[UpdateTargetPort] Invoked with resourceId: {resourceId}, targetPort: {targetPort}", resourceId, targetPort);

            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();

                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                if (containerApp.Value.Data.Configuration?.Ingress == null)
                {
                    _logger.LogInternalError($"No ingress configuration found in the app {resourceId}");
                    return false;
                }

                // Update the target port
                containerApp.Value.Data.Configuration.Ingress.TargetPort = targetPort;

                // Apply the update
                await containerAppResource.UpdateAsync(WaitUntil.Completed, containerApp.Value.Data);

                _logger.LogInternalInformation($"Successfully updated target port of container app {resourceId} to {targetPort}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error updating target port for container app {resourceId}");
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
            _logger.LogInternalInformation($"Getting image reference for resource: {resourceId}");
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            try
            {
                return await GetContainerAppImageReference(resourceIdentifier);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error getting image reference for resource {resourceId}");
                return null;
            }
        }

        public async Task<bool> VerifyExternalRegistryAsync(string resourceId, string imageReference)
        {
            _logger.LogInternalInformation($"Verifying external registry connectivity for {resourceId} and image {imageReference}");

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
                    _logger.LogInternalWarning($"Basic connectivity test to registry {registryHostname} failed");
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
                _logger.LogInternalError(ex, $"Error verifying external registry for image {imageReference}");
                return false;
            }
        }

        public async Task<RollbackResult> RollbackToLastKnownWorkingRevision(string resourceId)
        {
            _logger.LogInternalInformation($"Rolling back to last known working revision for resource: {resourceId}");

            try
            {
                return await RollbackContainerApp(resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error rolling back resource {resourceId} to last known working revision");
                return RollbackResult.Failure($"Exception during rollback: {ex.Message}");
            }
        }

        public async Task<ImageUpdateResult> UpdateContainerImage(string resourceId, string newImageReference)
        {
            _logger.LogInternalInformation($"Updating container image for resource: {resourceId} to {newImageReference}");

            try
            {
                return await UpdateContainerAppImage(resourceId, newImageReference);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error updating container image for resource {resourceId}");
                return ImageUpdateResult.Failure($"Exception during image update: {ex.Message}");
            }
        }

        public async Task<ContainerAppHealthValidationResult> ValidateContainerAppHealth(string resourceId)
        {
            _logger.LogInternalInformation($"[ValidateContainerAppHealth] Validating health for container app: {resourceId}");

            var result = new ContainerAppHealthValidationResult
            {
                IsHealthy = false,
                Details = new Dictionary<string, string>(),
                Messages = new List<string>()
            };

            try
            {
                // Step 1: Check if the container app resource exists and is provisioned
                var armClient = await _armClientFactory.GetArmOperationClient();
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                if (!containerApp.HasValue)
                {
                    result.Messages.Add("Container app resource not found.");
                    return result;
                }

                result.Details["ProvisioningState"] = containerApp.Value.Data.ProvisioningState.ToString();

                if (containerApp.Value.Data.ProvisioningState != ContainerAppProvisioningState.Succeeded)
                {
                    result.Messages.Add($"Container app is in {containerApp.Value.Data.ProvisioningState} state, not Succeeded.");
                    return result;
                }

                // Step 2: Check latest revision status
                var latestRevisionName = containerApp.Value.Data.LatestRevisionName;
                result.Details["LatestRevision"] = latestRevisionName;

                if (string.IsNullOrEmpty(latestRevisionName))
                {
                    result.Messages.Add("No latest revision found for the container app.");
                    return result;
                }

                var revisions = containerAppResource.GetContainerAppRevisions();
                ContainerAppRevisionResource latestRevision = null;

                await foreach (var revision in revisions.GetAllAsync())
                {
                    if (revision.Data.Name == latestRevisionName)
                    {
                        latestRevision = revision;
                        break;
                    }
                }

                if (latestRevision == null)
                {
                    result.Messages.Add($"Latest revision {latestRevisionName} not found.");
                    return result;
                }

                result.Details["RevisionState"] = latestRevision.Data.ProvisioningState.ToString();
                result.Details["RevisionTrafficWeight"] = (latestRevision.Data.TrafficWeight ?? 0).ToString();

                if (latestRevision.Data.ProvisioningState != ContainerAppRevisionProvisioningState.Provisioned)
                {
                    result.Messages.Add($"Latest revision is in {latestRevision.Data.ProvisioningState} state, not Provisioned.");
                    return result;
                }

                // Step 3: Check replica status
                var replicas = await _armHelper.GetRevisionReplicas(latestRevision.Id.ToString());
                result.Details["ReplicaCount"] = replicas.Count.ToString();

                // Check if the revision is configured to have a minimum of 0 replicas (scale to zero)
                bool canScaleToZero = latestRevision.Data.Template?.Scale?.MinReplicas == 0;
                result.Details["CanScaleToZero"] = canScaleToZero.ToString();

                // Count ready replicas
                int readyReplicas = replicas.Count(r => r?.Properties?.RunningState?.Equals("Running", StringComparison.OrdinalIgnoreCase) == true);
                result.Details["ReadyReplicas"] = readyReplicas.ToString();

                // Only consider the replica count a problem if the app can't scale to zero
                if (replicas.Count == 0)
                {
                    if (!canScaleToZero)
                    {
                        result.Messages.Add("No replicas found for the latest revision (scale to zero is not enabled).");
                        result.Details["ReplicaIssue"] = "No replicas found";
                    }
                    else
                    {
                        // This is expected behavior for scale to zero
                        result.Details["ReplicaStatus"] = "No active replicas (scale to zero is enabled)";
                    }
                }
                else if (readyReplicas == 0)
                {
                    result.Messages.Add("No replicas are in the Running state.");
                    result.Details["ReplicaIssue"] = "No running replicas";
                }
                else
                {
                    result.Details["ReplicaStatus"] = $"{readyReplicas} of {replicas.Count} replicas are running";
                }

                // Step 4: Check recent logs for errors
                var recentLogs = await GetContainerAppLogs(resourceId, false, latestRevisionName);
                bool hasErrors = await ContainsErrorsInLogs(recentLogs);
                result.Details["HasErrorsInLogs"] = hasErrors.ToString();

                if (hasErrors)
                {
                    result.Messages.Add("Recent logs contain error messages.");
                }

                // Step 5: If the app has external ingress and transport is not tcp, check if it's responding
                if (containerApp.Value.Data.Configuration?.Ingress != null &&
                    containerApp.Value.Data.Configuration.Ingress.External == true &&
                    !string.IsNullOrEmpty(containerApp.Value.Data.Configuration.Ingress.Fqdn) &&
                    (containerApp.Value.Data.Configuration.Ingress.Transport == null ||
                     !containerApp.Value.Data.Configuration.Ingress.Transport.ToString().Equals("tcp", StringComparison.OrdinalIgnoreCase)))
                {
                    string fqdn = containerApp.Value.Data.Configuration.Ingress.Fqdn;
                    result.Details["Hostname"] = fqdn;

                    bool endpointReachable = await IsEndpointReachable(fqdn);
                    result.Details["EndpointReachable"] = endpointReachable.ToString();

                    if (!endpointReachable)
                    {
                        result.Messages.Add($"HTTP endpoint {fqdn} is not reachable.");
                    }
                }

                // Step 6: Determine overall health
                // Consider app healthy if:
                // - App and revision are properly provisioned
                // - If app can't scale to zero, it must have running replicas
                // - External HTTP endpoint is reachable (if applicable)

                bool hasValidReplicas = (canScaleToZero && replicas.Count >= 0) || (!canScaleToZero && readyReplicas > 0);    // Otherwise need running replicas

                bool isHealthy =
                    containerApp.Value.Data.ProvisioningState == ContainerAppProvisioningState.Succeeded &&
                    latestRevision.Data.ProvisioningState == ContainerAppRevisionProvisioningState.Provisioned &&
                    hasValidReplicas;

                // If app has external HTTP ingress, it must also be reachable
                if (containerApp.Value.Data.Configuration?.Ingress != null &&
                    containerApp.Value.Data.Configuration.Ingress.External == true &&
                    (containerApp.Value.Data.Configuration.Ingress.Transport == null ||
                     !containerApp.Value.Data.Configuration.Ingress.Transport.ToString().Equals("tcp", StringComparison.OrdinalIgnoreCase)))
                {
                    isHealthy = isHealthy && bool.TryParse(result.Details["EndpointReachable"], out bool endpointReachable) && endpointReachable;
                }

                result.IsHealthy = isHealthy;

                // Add summary message
                if (result.IsHealthy)
                {
                    result.Messages.Add($"Container app is healthy.");
                }
                else if (result.Messages.Count == 0)
                {
                    result.Messages.Add("Container app is unhealthy but the specific reason could not be determined.");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error validating container app health for {resourceId}");
                result.Messages.Add($"Error validating health: {ex.Message}");
                return result;
            }
        }

        private async Task<ImageUpdateResult> UpdateContainerAppImage(string resourceId, string newImageReference)
        {
            var details = new Dictionary<string, string>();

            try
            {
                _logger.LogInternalInformation($"Updating Container App {resourceId} with new image: {newImageReference}");

                // Get the Container App resource
                var armClient = await _armClientFactory.GetArmOperationClient();
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                if (!containerApp.HasValue)
                {
                    return ImageUpdateResult.Failure($"Container App with resource ID {resourceId} not found");
                }
                if (string.IsNullOrWhiteSpace(newImageReference))
                {
                    return ImageUpdateResult.Failure("Invalid container image reference format",
                        null, newImageReference, details);
                }

                // Create a data object for the update
                ContainerAppData updateData = new ContainerAppData(containerApp.Value.Data.Location)
                {
                    Template = containerApp.Value.Data.Template ?? new ContainerAppTemplate()
                };

                // Check if we have containers in the template
                if (updateData.Template?.Containers == null || updateData.Template.Containers.Count == 0)
                {
                    return ImageUpdateResult.Failure("No containers found in the Container App template",
                        null, newImageReference, details);
                }

                // Store the original image for logging
                string originalImage = updateData.Template.Containers[0].Image ?? "unknown";
                details["OriginalImage"] = originalImage;
                details["NewImage"] = newImageReference;

                var containerToUpdate = updateData.Template.Containers[0];
                if (containerToUpdate == null)
                {
                    return ImageUpdateResult.Failure("First container in the template is null",
                        originalImage, newImageReference, details);
                }

                // Update the image reference
                containerToUpdate.Image = newImageReference;

                // Update the Container App with the new template
                _logger.LogInternalInformation($"Applying update to Container App {resourceId}: changing image from {originalImage} to {newImageReference}");

                var updateOperation = await containerAppResource.UpdateAsync(
                    WaitUntil.Completed,
                    updateData,
                    CancellationToken.None
                );

                var updatedApp = updateOperation.Value;

                // Verify the update was successful
                if (updatedApp != null &&
                    updatedApp.Data.Template?.Containers != null &&
                    updatedApp.Data.Template.Containers.Count > 0 &&
                    updatedApp.Data.Template.Containers[0].Image == newImageReference)
                {
                    _logger.LogInternalInformation($"Successfully updated Container App {resourceId} image to: {newImageReference}");

                    details["Status"] = "Updated successfully";
                    details["LatestRevisionName"] = updatedApp.Data.LatestRevisionName ?? "Unknown";

                    return ImageUpdateResult.Success(originalImage, newImageReference, details);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error updating Container App {resourceId} with image {newImageReference}");

                details["ErrorType"] = ex.GetType().Name;
                details["ErrorMessage"] = ex.Message;
                return ImageUpdateResult.Failure($"Error updating image: {ex.Message}",
                    null, newImageReference, details);
            }

            _logger.LogInternalError($"Failed to update Container App {resourceId}");
            return ImageUpdateResult.Failure($"Failed to update Container App image",
                null, newImageReference, details);
        }

        private async Task<RollbackResult> RollbackContainerApp(string resourceId)
        {
            try
            {
                var details = new Dictionary<string, string>();

                // Get the Container App resource
                var armClient = await _armClientFactory.GetArmOperationClient();

                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                // Get all revisions for this Container App
                var revisions = await containerAppResource.GetContainerAppRevisions().ToListAsync();

                // Sort revisions by last active time in descending order (newest first)
                revisions = revisions
                    .OrderByDescending(r => r.Data.LastActiveOn)
                    .ToList();

                details["TotalRevisions"] = revisions.Count.ToString();

                // We need at least 2 revisions to perform a rollback
                if (revisions.Count < 2)
                {
                    return RollbackResult.Failure(
                        "Not enough revisions for rollback",
                        details);
                }

                // Get current active revision name
                var currentRevisionName = revisions.FirstOrDefault(r => r.Data.IsActive == true)?.Data.Name;
                details["CurrentRevision"] = currentRevisionName ?? "Unknown";

                // Find a healthy revision to roll back to
                // First, gather more information about each revision
                var revisionHealthInfo = new List<(ContainerAppRevisionResource Revision, bool IsActive, bool IsHealthy, string HealthReason)>();

                foreach (var revision in revisions)
                {
                    // Skip the current revision
                    if (revision.Data.Name == currentRevisionName)
                    {
                        continue;
                    }

                    bool isActive = (revision.Data.TrafficWeight ?? 0) > 0;

                    // Check if this revision was healthy
                    bool isHealthy = revision.Data.ProvisioningState == ContainerAppRevisionProvisioningState.Provisioned;
                    string healthReason = isHealthy ? "Provisioned state is good" : $"Provisioning state is {revision.Data.ProvisioningState}";

                    // Additional health checks if needed
                    if (isHealthy)
                    {
                        // Check if this revision had replicas in the past
                        var replicas = await _armHelper.GetRevisionReplicas(revision.Id.ToString());

                        // Check if the revision is configured to have a minimum of 0 replicas (scale to zero)
                        var revisionDetails = await armClient.GetContainerAppRevisionResource(revision.Id).GetAsync();
                        bool canScaleToZero = revisionDetails.Value.Data.Template?.Scale?.MinReplicas == 0;

                        if (containerApp.Value?.Data?.Configuration?.ActiveRevisionsMode != ContainerAppActiveRevisionsMode.Single)
                        {
                            int readyReplicas = replicas.Count(r => r?.Properties?.RunningState?.Equals("Running", StringComparison.OrdinalIgnoreCase) == true);

                            // If we have no replicas or no ready replicas, check if it's due to scale to zero
                            if (replicas.Count == 0 || readyReplicas == 0)
                            {
                                if (canScaleToZero)
                                {
                                    // This is expected behavior for scale to zero, so revision can still be considered healthy
                                    healthReason = "No active replicas (scale to zero is enabled)";
                                }
                                else
                                {
                                    isHealthy = false;
                                    healthReason = replicas.Count == 0
                                        ? "No replicas found (scale to zero is not enabled)"
                                        : "No running replicas found";
                                }
                            }
                            else
                            {
                                healthReason = $"{readyReplicas} of {replicas.Count} replicas were running";
                            }
                        }
                    }

                    revisionHealthInfo.Add((revision, isActive, isHealthy, healthReason));
                }

                details["EvaluatedRevisions"] = revisionHealthInfo.Count.ToString();
                details["HealthyRevisions"] = revisionHealthInfo.Count(r => r.IsHealthy).ToString();

                // First try to find a healthy revision that was active in the past
                var targetRevision = revisionHealthInfo.FirstOrDefault(r => r.IsHealthy && r.IsActive).Revision;
                string targetSelectionReason = "Found healthy and previously active revision";

                // If no healthy and active revision found, try any healthy revision
                if (targetRevision == null)
                {
                    var healthyRevision = revisionHealthInfo.FirstOrDefault(r => r.IsHealthy);
                    targetRevision = healthyRevision.Revision;
                    targetSelectionReason = "Found healthy revision (not previously active)";
                }

                // If no healthy revision found, don't fall back to any revision - just return an error
                if (targetRevision == null)
                {
                    _logger.LogInternalWarning("No healthy revisions found for Container App {resourceId}", resourceId);
                    return RollbackResult.Failure(
                        "No healthy revisions found to roll back to",
                        details);
                }

                details["TargetRevision"] = targetRevision.Data.Name;
                details["TargetRevisionSelectionReason"] = targetSelectionReason;

                // Find the target revision's health info for the log
                var healthInfo = revisionHealthInfo.First(r => r.Revision.Id == targetRevision.Id);
                details["TargetRevisionHealth"] = healthInfo.HealthReason;

                // Activate the target revision
                _logger.LogInternalInformation($"Activating revision {targetRevision.Data.Name} for Container App {resourceId}");
                await armClient.GetContainerAppRevisionResource(targetRevision.Id).ActivateRevisionAsync();

                _logger.LogInternalInformation($"Successfully rolled back Container App {resourceId} from revision {currentRevisionName} to revision {targetRevision.Data.Name}");

                return RollbackResult.Success(
                    targetRevision.Data.Name,
                    details);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error rolling back Container App {resourceId}");
                return RollbackResult.Failure($"Exception during rollback: {ex.Message}");
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

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (response.Headers.Contains("X-RateLimit-Remaining"))
                    {
                        var rateLimitRemaining = response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
                        var rateLimitLimit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
                        var rateLimitReset = response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault();

                        _logger.LogInternalInformation($"Rate Limit Remaining: {rateLimitRemaining}/{rateLimitLimit}, Reset Time: {rateLimitReset}");

                        // If remaining requests are 0, handle rate limiting
                        if (int.TryParse(rateLimitRemaining, out int remaining) && remaining == 0)
                        {
                            _logger.LogInternalWarning("Rate limit exceeded. Please wait until the limit resets.");
                            return false;
                        }
                    }

                    // Fallback to Retry-After logic if needed
                    if (response.Headers.TryGetValues("Retry-After", out var values))
                    {
                        var retryAfter = values.FirstOrDefault();
                        if (retryAfter != null && int.TryParse(retryAfter, out int seconds))
                        {
                            _logger.LogInternalWarning($"Rate limit exceeded. Retry after {seconds} seconds.");
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
                _logger.LogInternalError(ex, $"Error verifying Docker Hub registry for {imageReference}");
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

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogInternalInformation($"Successfully verified MCR image: {imageReference}");
                    return true;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInternalWarning($"Image {imageReference} not found in Microsoft Container Registry");
                    return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error verifying Microsoft Container Registry for {imageReference}");
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

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogInternalInformation($"Successfully verified GCR image: {imageReference}");
                    return true;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInternalWarning($"Image {imageReference} not found in Google Container Registry");
                    return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error verifying Google Container Registry for {imageReference}");
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
                    _logger.LogInternalWarning($"Could not extract registry hostname from {imageReference}");
                    return false;
                }

                var armClient = await _armClientFactory.GetArmOperationClient();
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
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

                    var httpClient = _httpClientFactory.CreateClient();
                    var response = await httpClient.SendAsync(request);

                    // 200 OK means the registry is accessible and doesn't require auth for basic API access
                    // 401 Unauthorized is also acceptable as it confirms the registry exists but needs auth
                    if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogInternalInformation($"Successfully verified registry API accessibility for: {registryHostname}");
                        return true;
                    }

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogInternalWarning($"Registry API endpoint not found at {manifestUrl}. The registry may not implement the Docker Registry HTTP API V2.");
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogInternalWarning(ex, $"Error connecting to registry API for {registryHostname}. This may be due to network restrictions or registry configuration.");
                }

                // If container app has registry credentials, assume the registry is accessible
                // Even if the API check failed, the credentials configuration suggests intentional use
                return hasRegistryCredentials;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error verifying private registry for {imageReference}");
                return false;
            }
        }

        private async Task<string> GetDockerOAuthTokenAsync(string repo)
        {
            var authUrl = "https://auth.docker.io/token";
            var authRequest = new HttpRequestMessage(HttpMethod.Get, $"{authUrl}?service=registry.docker.io&scope=repository:{repo}:pull");

            var httpClient = _httpClientFactory.CreateClient();
            var authResponse = await httpClient.SendAsync(authRequest);

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

        public async Task<string> RollbackToLastRevision(string resourceId)
        {
            try
            {
                var listRevisions = await ListContainerAppRevisionsAsync(resourceId);

                if (listRevisions == null || !listRevisions.Any())
                {
                    string message = ($"No revisions found for Container App {resourceId}");
                    _logger.LogInternalWarning(message);
                    return message;
                }

                // Sort revisions by LastActiveOn in descending order
                var sortedRevisions = listRevisions
                    .Where(r => !string.IsNullOrEmpty(r.LastActiveOn))
                    .OrderByDescending(r => r.LastActiveOn)
                    .ToList();

                if (sortedRevisions.Count < 2)
                {
                    string message = $"Not enough revisions found for Container App {resourceId} to perform rollback";
                    _logger.LogInternalWarning(message);
                    return message;
                }

                // Skip the first (current) revision and get the second one (previous)
                var previousRevision = sortedRevisions[1];

                _logger.LogInternalInformation($"Rolling back Container App {resourceId} to revision {previousRevision.RevisionName}");
                var restartResult = await RestartContainerApp(appResourceId: resourceId, revisionName: previousRevision.RevisionName);

                return restartResult.Equals("Restart succeeded") ? "Rollback succeeded" : "Rollback failed";
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error in RollbacToLastRevision for Container App {resourceId}");
                return $"Rollback failed due to an exception: {ex.Message}";
            }
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
                _logger.LogInternalError(e, "Failed to get logs from {eventsStreamUrl}.", eventsStreamUrl);
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
                _logger.LogInternalWarning("Revision {revisionName} not found for Container App {containerAppName}", revisionName, containerApp.Data.Name);
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

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.SendAsync(request);
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
                        _logger.LogInternalError(e, "Failed to deserialize JSON from {logStreamEndpoint}", logStreamEndpoint);
                        return [];
                    }
                }

                var content = await response.Content.ReadAsStringAsync();
                return content.Split("\n");
            }
            else
            {
                _logger.LogInternalError("Failed to get logs from {logStreamEndpoint}. Status code: {StatusCode}", logStreamEndpoint, response.StatusCode);
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
                _logger.LogInternalWarning("No workspace ID found for Container App {containerAppName}", containerApp.Data.Name);
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
                _logger.LogInternalWarning("No logs found for Container App {containerAppName} in the last 2 hours", containerApp.Data.Name);
                return [];
            }

            return logAnalyticsLogs
                .Select(log => $"[{log.TimeGenerated}] {log.Log}")
                .ToList();
        }

        private async Task<string> GetContainerAppWorkspaceIdAsync(ContainerAppResource containerApp)
        {
            var armClient = await _armClientFactory.GetArmOperationClient();
            var environment = await armClient.GetContainerAppManagedEnvironmentResource(containerApp.Data.EnvironmentId).GetAsync();
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
                _logger.LogInternalError(ex, $"Error extracting registry hostname from {imageReference}");
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
                    var httpClient = _httpClientFactory.CreateClient();
                    var httpResponse = await httpClient.SendAsync(request);
                    if (httpResponse.IsSuccessStatusCode || httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.LogInternalInformation($"Successfully connected to registry {hostname} via HTTPS");
                        return true;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogInternalWarning(ex, $"HTTPS connection failed to {hostname}");
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error testing connectivity to external registry: {hostname}");
                return false;
            }
        }

        private async Task<string> GetContainerAppImageReference(ResourceIdentifier resourceId)
        {
            var armClient = await _armClientFactory.GetArmOperationClient();
            var containerAppResource = armClient.GetContainerAppResource(resourceId);
            var containerApp = await containerAppResource.GetAsync();
            string latestRevisionName = containerApp.Value.Data.LatestRevisionName;

            // If we have a latest revision name, get that revision specifically
            if (!string.IsNullOrEmpty(latestRevisionName))
            {
                string revisionResourceId = $"{resourceId}/revisions/{latestRevisionName}";
                try
                {
                    var revisionResource = armClient.GetContainerAppRevisionResource(new ResourceIdentifier(revisionResourceId));
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
                    _logger.LogInternalWarning(ex, $"Could not retrieve latest revision {latestRevisionName} for app {resourceId}, falling back to template");
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

        private static async Task SendResize(WebSocket socket, int x, int y, CancellationToken token)
        {
            List<byte> bytes = [];
            bytes.Add(0); //forward byte
            bytes.Add(4); //resize
            byte[] message = Encoding.UTF8.GetBytes(FormattableString.Invariant($"{{{{\"Width\": {x}, \"Height\": {y}}}}}"));
            foreach (byte b in message)
            {
                bytes.Add(b);
            }
            await socket.SendAsync(bytes.ToArray(), WebSocketMessageType.Text, true, token);
        }

        private static async Task Write(WebSocket socket, string line, CancellationToken token)
        {
            byte[] bytes = [0, 0, 0];

            byte[] message = Encoding.UTF8.GetBytes(line);

            foreach (byte b in message)
            {
                bytes[2] = b;
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, token);
            }
        }

        // Logic is from https://msazure.visualstudio.com/One/_git/AAPT-Antares-AzureFunctionsUx?path=%2Fclient-react%2Fsrc%2Fpages%2Fcontainer-app%2Fconsole%2FConsoleDataLoader.tsx&_a=contents&version=GBmaster
        private static async Task<bool> Read(WebSocket socket, CancellationToken token, string completionMarker, StringBuilder stdout)
        {
            Memory<byte> buffer = new Memory<byte>(new byte[8 * 1024]);
            var result = await socket.ReceiveAsync(buffer, token);

            var data = buffer[..result.Count].ToArray();
            var text = string.Empty;

            switch (data[0])
            {
                case 0: // forwarded from k8s cluster exec endpoint
                    if (data[1] == 1 || data[1] == 2 || data[1] == 3)
                    {
                        text = Encoding.UTF8.GetString(data, 2, data.Length - 2);
                        stdout.AppendLine(text);

                        // Check if the completion marker is in the output
                        if (!string.IsNullOrEmpty(completionMarker) && text.Contains(completionMarker))
                        {
                            Console.WriteLine("Execution completed successfully.");
                            return true; // Signal completion
                        }
                    }
                    else if (data[1] == 4)
                    {
                        // terminal resize
                    }
                    else
                    {
                        throw new Exception($"Unknown Proxy API exec signal {data[1]}");
                    }
                    break;

                case 1: // info from Proxy API
                    text = "INFO: " + Encoding.UTF8.GetString(data, 1, data.Length - 1) + "\r\n";
                    Console.WriteLine(text);
                    break;

                case 2: // error from Proxy API
                    text = "ERROR: " + Encoding.UTF8.GetString(data, 1, data.Length - 1) + "\r\n";
                    Console.WriteLine(text);
                    break;

                default:
                    throw new Exception($"Unknown Proxy API exec signal {data[0]}");
            }

            return false;
        }

        private async Task<string> InvokeExecCommand(string resourceId, string command)
        {
            try
            {
                // Get Container App Details.
                ResourceIdentifier resourceIdentifer = new ResourceIdentifier(resourceId);
                string subscriptionId = resourceIdentifer.SubscriptionId;
                var armClient = await _armClientFactory.GetArmOperationClient();
                var containerAppResource = armClient.GetContainerAppResource(resourceIdentifer);
                var containerApp = await containerAppResource.GetAsync();
                var activeRevisions = containerAppResource.GetContainerAppRevisions();
                var firstActiveRevision = activeRevisions.FirstOrDefault(r => r.Data.IsActive == true);
                var firstReplica = await firstActiveRevision.GetContainerAppReplicas().FirstOrDefault().GetAsync();

                string execEndPoint = firstReplica.Value.Data.Containers.First().ExecEndpoint;

                var uriBuilder = new UriBuilder(execEndPoint);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                query.Add("command", "/bin/bash");
                uriBuilder.Query = query.ToString();

                string token = await _armHelper.GetProxyApiTokenAsync(subscriptionId, resourceIdentifer.ResourceGroupName, containerApp.Value.Data.Name);

                var webSocket = new ClientWebSocket();
                webSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
                webSocket.Options.HttpVersion = HttpVersion.Version11;
                webSocket.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                webSocket.Options.UseDefaultCredentials = false;

                var resultBuilder = new StringBuilder();

                await webSocket.ConnectAsync(uriBuilder.Uri, CancellationToken.None);
                _logger.LogInternalInformation("Connected to WebSocket endpoint.");

                await SendResize(webSocket, 80, 24, CancellationToken.None);

                // Define completion marker and create a TaskCompletionSource for synchronization
                string completionMarker = "COMPLETED ANALYSIS";
                var completionSource = new TaskCompletionSource<bool>();

                var listeningTask = Task.Run(async () =>
                {
                    try
                    {
                        while (webSocket.State == WebSocketState.Open)
                        {
                            bool isCompleted = await Read(webSocket, CancellationToken.None, completionMarker, resultBuilder);
                            if (isCompleted)
                            {
                                bool setResult = completionSource.TrySetResult(true);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        completionSource.TrySetException(ex);
                    }
                });

                // Setup.
                await Write(webSocket, command + "\n", CancellationToken.None);

                // Wait for the completion signal or timeout after a reasonable period
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
                var completedTask = await Task.WhenAny(completionSource.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogInternalError($"[InvokeCommand] Command execution timed out for {resourceId}.");
                }

                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);

                string result = resultBuilder.ToString();
                string pattern = @"STARTED ANALYSIS\s*(.*?)\s*COMPLETED ANALYSIS";
                Match match = Regex.Match(result, pattern, RegexOptions.Singleline);

                if (match.Success)
                {
                    string analysisResult = match.Groups[1].Value.Trim();
                    _logger.LogInternalError($"[InvokeExecCommand] InvokeExecCommand for command: {command} - {analysisResult}.");
                    return analysisResult;
                }
                else
                {
                    _logger.LogInternalError($"[InvokeExecCommand] No Analysis found: {command}.");
                    return result; // TODO:FIX
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"[InvokeExecCommand] Error executing command: {command}: {ex.Message}");
                throw;
            }
        }

        public async Task<string> GetContainerMemoryAnalysisForDotnet(string resourceId)
        {
            _logger.LogInternalInformation($"[GetContainerMemoryAnalysisForDotnet] Getting memory analysis for {resourceId}");
            try
            {
                string commands = " apt-get update; apt-get install -y curl; curl https://dotnetanalysis.blob.core.windows.net/acascripts/dotnet-dump-analyze.sh -o dotnet-dump-analyze.sh; chmod +x ./dotnet-dump-analyze.sh; sh ./dotnet-dump-analyze.sh";
                return await InvokeExecCommand(resourceId, commands);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"[GetContainerMemoryAnalysisForDotnet] Error executing command: {ex.Message} for {resourceId}");
                throw;
            }
        }

        public async Task<bool> IsDotnetBased(string resourceId)
        {
            _logger.LogInternalInformation($"[IsDotnetBased] Checking if .NET Based {resourceId}");
            try
            {
                string commands = " apt-get update; apt-get install -y curl; curl https://dotnetanalysis.blob.core.windows.net/acascripts/dotnet-detect.sh -o dotnet-detect.sh; chmod +x ./dotnet-detect.sh; sh ./dotnet-detect.sh";
                var result = await InvokeExecCommand(resourceId, commands);
                return result.Any();
            }

            catch (Exception ex)
            {
                _logger.LogInternalError($"[IsDotnetBased] Error executing command: {ex.Message} for {resourceId}");
                throw;
            }
        }

        private async Task<bool> ContainsErrorsInLogs(string logs)
        {
            if (string.IsNullOrEmpty(logs))
            {
                return false;
            }

            const string prompt = "You are a log analyzer specialized in container applications. " +
                                 "Analyze these application logs and determine if they contain any errors or issues. " +
                                 "Focus on critical problems that would affect application functionality, such as: " +
                                 "- Exceptions or crashes" +
                                 "- Failed startup or health probes" +
                                 "- Network connectivity problems" +
                                 "- Permission/authentication errors" +
                                 "- Application runtime errors" +
                                 "- Service unavailability" +
                                 "- Resource allocation failures" +
                                 "Your response should be exactly 'true' if errors are detected or 'false' if no significant errors are found.";

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, prompt),
                new ChatMessage(ChatRole.User, logs)
            };

            var options = new ChatOptions
            {
                Temperature = (float)0.0,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };

            try
            {
                var response = await _chatClient.GetResponseAsync(messages, options);
                string result = response.Text.Trim().ToLowerInvariant();

                return result == "true";
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<bool> IsEndpointReachable(string hostname)
        {
            try
            {
                if (string.IsNullOrEmpty(hostname))
                {
                    return false;
                }

                using var httpClient = new HttpClient();
                var url = $"https://{hostname}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                var response = await httpClient.SendAsync(request);

                // Log the status code for debugging
                _logger.LogInternalInformation($"Endpoint {url} returned status code {(int)response.StatusCode} ({response.StatusCode})");

                // Consider endpoint reachable if:
                // - 2xx Success codes
                // - 3xx Redirect codes
                // - 401 Unauthorized (indicating the endpoint is protected but reachable)
                bool isReachable = response.IsSuccessStatusCode ||
                                   (int)response.StatusCode >= 300 && (int)response.StatusCode < 400 ||
                                   response.StatusCode == HttpStatusCode.Unauthorized;

                return isReachable;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Error checking endpoint reachability for {hostname}");
                return false;
            }
        }

        public async Task<bool> ModifyContainerAppScaleRuleAsync(string resourceId, string ruleName, string modificationType, string scaleRuleType, IDictionary<string, string> metadata)
        {
            _logger.LogInternalInformation($"[{nameof(ModifyContainerAppScaleRuleAsync)}] Invoked with {resourceId}, {ruleName}, {modificationType}, {scaleRuleType}");

            try
            {
                var armClient = await _armClientFactory.GetArmOperationClient();
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();
                var data = containerApp.Value.Data;

                var newRule = CreateNewScaleRuleWithType(ruleName, scaleRuleType, metadata);
                if (TryCreateNewScaleRules(data.Template.Scale, newRule, modificationType, out var effectiveRules))
                {
                    _logger.LogInternalInformation($"[{nameof(ModifyContainerAppScaleRuleAsync)}] Scale rule {ruleName} modified successfully.");
                    data.Template.Scale = effectiveRules;
                    await containerAppResource.UpdateAsync(WaitUntil.Completed, data);
                }
                else
                {
                    _logger.LogInternalWarning($"[{nameof(ModifyContainerAppScaleRuleAsync)}] Scale rule {ruleName} not found for modification.");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"[{nameof(ModifyContainerAppScaleRuleAsync)}] Error retrieving Container App {resourceId}");
                return false;
            }
        }

        /// <summary>
        /// Tries to compile new scale rules based on the current rules and the new rule to be added or updated.
        /// </summary>
        /// <param name="currentRules">The current scale rules.</param>
        /// <param name="newRule">The new scale rule to be added or updated.</param>
        /// <param name="modificationType">The type of modification (add, remove, update).</param>
        /// <param name="effectiveRules">The effective rules after modification.</param>
        /// <returns>True if the modification was successful; otherwise, false.</returns>
        private bool TryCreateNewScaleRules(ContainerAppScale? currentRules, ContainerAppScaleRule newRule, string modificationType, out ContainerAppScale effectiveRules)
        {
            effectiveRules = new ContainerAppScale
            {
                CooldownPeriod = currentRules?.CooldownPeriod,
                MinReplicas = currentRules?.MinReplicas,
                MaxReplicas = currentRules?.MaxReplicas,
                PollingInterval = currentRules?.PollingInterval,
            };

            switch (modificationType.ToLowerInvariant())
            {
                case "add":
                    foreach (var existingRule in currentRules?.Rules ?? [])
                    {
                        effectiveRules.Rules.Add(existingRule);
                    }

                    effectiveRules.Rules.Add(newRule);
                    return true;
                case "remove":
                case "delete":
                    foreach (var existingRule in currentRules?.Rules ?? [])
                    {
                        if (!existingRule.Name.Equals(newRule.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            effectiveRules.Rules.Add(existingRule);
                        }
                    }
                    return true;
                case "update":
                    foreach (var existingRule in currentRules?.Rules ?? [])
                    {
                        if (existingRule.Name.Equals(newRule.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            effectiveRules.Rules.Add(newRule);
                        }
                        else
                        {
                            effectiveRules.Rules.Add(existingRule);
                        }
                    }
                    return true;
                default:
                    _logger.LogInternalWarning($"[{nameof(ModifyContainerAppScaleRuleAsync)}] Invalid modification type: {modificationType}");
                    return false;
            }
        }

        /// <summary>
        /// Constructs a new scale rule object based on the provided type and metadata.
        /// It'll copy the metadata to the appropriate scale rule type.
        /// </summary>
        private ContainerAppScaleRule CreateNewScaleRuleWithType(string ruleName, string scaleRuleType, IDictionary<string, string> metadata)
        {
            var rule = new ContainerAppScaleRule { Name = ruleName };
            switch (scaleRuleType.ToLowerInvariant())
            {
                case "http":
                    rule.Http = new ContainerAppHttpScaleRule();
                    foreach (var p in metadata)
                    {
                        rule.Http.Metadata.Add(p.Key, p.Value);
                    }
                    break;
                case "tcp":
                    rule.Tcp = new ContainerAppTcpScaleRule();
                    foreach (var p in metadata)
                    {
                        rule.Tcp.Metadata.Add(p.Key, p.Value);
                    }
                    break;
                default:
                    rule.Custom = new ContainerAppCustomScaleRule { CustomScaleRuleType = scaleRuleType };
                    foreach (var p in metadata)
                    {
                        rule.Custom.Metadata.Add(p.Key, p.Value);
                    }
                    break;
            }

            return rule;
        }

        private async Task<string> GetContainerUpdateDeploymentInformation(string logsJson, string availabilityJson)
        {
            try
            {
                var prompt = @$"
You are a cloud operations analyst. I will provide you with Azure activity logs for a resource group of a container app. Follow these steps to find

1. I want you to get me all the successful deployment times or whenever the containerapp has been created / deployed and has been updated and put into production.

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

{logsJson}";

                var response = await _chatClient.GetResponseAsync(prompt);
                return response.Text;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error summarizing logs with LLM");
                return $"Error summarizing logs: {ex.Message}";
            }
        }


        public async Task<List<DateTimeOffset>> GetDeploymentTimes(string resourceId)
        {
            ResourceIdentifier resourceIdentifier = new ResourceIdentifier(resourceId);
            string containerAppName = resourceIdentifier.Name;

            var logsAndComponents = await _graphDbPlugin.FetchActivityLogsAndComponents(resourceId);
            var logs = logsAndComponents.ActivityLogs;
            var successfulDeployments =
                                 logs.Where(l => l.TryGetValue("operationName", out var operationName)      &&
                                            l.TryGetValue("authorizationScope", out var authorizationScope) &&
                                            l.TryGetValue("status", out var status)                         &&
                                            //operationName.ToString().Contains("containerApps/write", StringComparison.OrdinalIgnoreCase) &&
                                            status.ToString().Equals("Succeeded", StringComparison.OrdinalIgnoreCase) &&
                                            authorizationScope.ToString().Contains(containerAppName, StringComparison.OrdinalIgnoreCase));
            var times = successfulDeployments 
                      .Select(log => DateTimeOffset.Parse(log["eventTimestamp"].ToString()))
                      .OrderByDescending(t => t)
                      .ToList();
            var s = JsonSerializer.Serialize(times, new JsonSerializerOptions { WriteIndented = true });
            var d = await GetContainerUpdateDeploymentInformation(s, "");
            return times;
        }
    }
}
