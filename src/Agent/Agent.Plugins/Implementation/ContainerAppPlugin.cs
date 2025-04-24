// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
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

                var credential = _authService.GetArmOperationCredential();
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

                var logs = await new[]
                    {
                        GetStreamedSystemLogsAsync(containerApp, streamToken),
                        GetHistoricalLogsAsync(containerApp, revisionName, LogType.System),
                        GetStreamedConsoleLogsAsync(containerApp, streamToken, revisionName),
                        GetHistoricalLogsAsync(containerApp, revisionName, LogType.Application)
                    }
                    .IgnoreAndFilterFailures(_logger);
                return await SummarizeLogs(logs.SelectMany(l => l));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetRevisionLogsAsync with resourceId {resourceId}, revisionName {revisionName}");
                return null;
            }
        }

        private async Task<string> SummarizeLogs(IEnumerable<string> logs)
        {
            _logger.LogInformation("Summarizing logs");
            const string prompt = $"Please summarize these application logs. " +
                                  $"This summary will be used to determine if there any potential issues with the application. " +
                                  $"Make sure it's complete, detailed, and references any particular numbers, error messages, error codes verbatim in case they are relevant for debugging";

            var messages = new []
            {
                new ChatMessage(ChatRole.System, prompt),
                new ChatMessage(ChatRole.User, string.Join("\n", logs))
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

        enum LogType
        {
            System,
            Application,
        }

        private async Task<IReadOnlyCollection<string>> GetStreamedSystemLogsAsync(
            ContainerAppResource containerApp,
            Response<ContainerAppAuthToken> streamToken)
        {
            var eventsStreamUrl = containerApp.Data.EventStreamEndpoint;
            try
            {
                return await this.GetLogsAsync(eventsStreamUrl.ToString(), streamToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get logs from {eventsStreamUrl}.", eventsStreamUrl);
                return [];
            }
        }

        private async Task<IReadOnlyCollection<string>> GetStreamedConsoleLogsAsync(
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
                .Select(c => this.GetLogsAsync(c.LogStreamEndpoint, streamToken))
                .IgnoreAndFilterFailures(_logger) ?? [];

            return logs.SelectMany(l => l).ToList();
        }

        private async Task<string[]> GetLogsAsync(string? argLogStreamEndpoint, ContainerAppAuthToken streamToken)
        {
            if (string.IsNullOrEmpty(argLogStreamEndpoint))
            {
                return [];
            }

            var logStreamEndpoint = new Uri($"{argLogStreamEndpoint}?follow=false&output=text&tailLines=25");
            var request = new HttpRequestMessage(HttpMethod.Get, logStreamEndpoint);
            request.Headers.Add("Authorization", $"Bearer {streamToken.Token}");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content.Split("\n");
            }
            else
            {
                _logger.LogError("Failed to get logs from {logStreamEndpoint}. Status code: {StatusCode}", logStreamEndpoint, response.StatusCode);
                return [];
            }
        }

        private async Task<IReadOnlyCollection<string>> GetHistoricalLogsAsync(
            ContainerAppResource containerApp,
            string revisionName,
            LogType logType)
        {
            // 1. Get stream token
            var streamToken = await containerApp.GetAuthTokenAsync();
            if (!streamToken.HasValue)
            {
                _logger.LogWarning("No auth token found for Container App {containerAppName}", containerApp.Data.Name);
                return [];
            }

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
                .Select(log => log.Log)
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
    }
}
