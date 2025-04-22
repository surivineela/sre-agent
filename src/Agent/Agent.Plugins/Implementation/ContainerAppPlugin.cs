// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Network;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class ContainerAppPlugin : IContainerAppPlugin
    {
        private readonly ArmHelper _armHelper;
        private readonly IGraphDatabaseClient _databaseClient;
        private readonly ILogger<ContainerAppPlugin> _logger;
        private readonly IArmClientFactory _armClientFactory;
        private readonly IAuthenticationService _authService;

        public ContainerAppPlugin(ArmHelper armHelper,
            IGraphDatabaseClient graphDbClient,
            ILogger<ContainerAppPlugin> logger,
            IArmClientFactory armClientFactory,
            IAuthenticationService authService)
        {
            _armClientFactory = armClientFactory;
            _databaseClient = graphDbClient;
            _armHelper = armHelper;
            _logger = logger;
            _authService = authService;
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
                var credential = _authService.GetArmOperationCredential();

                var armClient = new ArmClient(credential);

                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));

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
                var credential = _authService.GetArmOperationCredential();
                var armClient = new ArmClient(credential);

                // Get the Container App to find its environment
                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                var containerApp = await containerAppResource.GetAsync();

                if (containerApp.Value.Data.ManagedEnvironmentId == null)
                {
                    _logger.LogWarning($"Container App {resourceId} does not have a managed environment ID");
                    return result;
                }

                // Get the Container App Environment
                var environment = armClient.GetContainerAppManagedEnvironmentResource(containerApp.Value.Data.ManagedEnvironmentId);
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
