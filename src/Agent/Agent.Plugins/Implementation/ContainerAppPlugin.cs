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
                    .project('id', 'name', 'type', 'properties')
                    .by(id())
                    .by(coalesce(values('resourceName'), constant('')))
                    .by(label())
                    .by(valueMap())";

                var result = await _databaseClient.Query(query);

                if (result == null || !result.Any())
                {
                    _logger.LogWarning($"Container App with ID '{resourceId}' not found in graph database.");
                    return null;
                }

                var containerApp = result.First();
                var properties = containerApp["properties"];

                string name = containerApp["name"]?.ToString() ?? "";
                string location = GetFirstPropertyValue(properties, "location");
                string workloadProfile = GetFirstPropertyValue(properties, "workloadProfileName");
                string state = GetFirstPropertyValue(properties, "provisioningState");
                string resourceGroup = GetFirstPropertyValue(properties, "resourceGroupName");
                string fqdn = GetFirstPropertyValue(properties, "fqdn");
                string environmentName = GetFirstPropertyValue(properties, "managedEnvironmentId");
                
                bool isIngressEnabled = false;
                string ingressExternalValue = GetFirstPropertyValue(properties, "ingressExternal");
                if (!string.IsNullOrEmpty(ingressExternalValue))
                {
                    if (!bool.TryParse(ingressExternalValue, out isIngressEnabled))
                    {
                        _logger.LogWarning($"Could not parse ingressExternal value: {ingressExternalValue} to boolean");
                    }
                }

                AppHealthInfo appHealthInfo = null;
                if (properties.ContainsKey("appHealthInfo") && properties["appHealthInfo"] != null)
                {
                    try
                    {
                        var scorecardValue = properties["appHealthInfo"];
                        
                        if (scorecardValue is IEnumerable enumerable)
                        {
                            foreach (var item in enumerable)
                            {
                                if (item != null)
                                {
                                    string json = item.ToString();
                                    if (!string.IsNullOrEmpty(json))
                                    {
                                        appHealthInfo = System.Text.Json.JsonSerializer.Deserialize<AppHealthInfo>(json);
                                        break;
                                    }
                                }
                            }
                        }
                        else if (scorecardValue is string json)
                        {
                            appHealthInfo = System.Text.Json.JsonSerializer.Deserialize<AppHealthInfo>(json);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error deserializing appHealthInfo: {ex.Message}");
                        appHealthInfo = new AppHealthInfo();
                    }
                }

                return new ContainerAppDescriptor(
                    ResourceId: resourceId,
                    Name: name,
                    Location: location,
                    WorkloadProfile: workloadProfile,
                    State: state,
                    ResourceGroup: resourceGroup,
                    Fqdn: fqdn,
                    EnvironmentName: environmentName,
                    IsIngressEnabled: isIngressEnabled,
                    Revisions: null,
                    AppHealthInfo: appHealthInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetContainerAppInfoAsync with resourceId {resourceId}");
                return null;
            }
        }

        // Helper method to extract property values from AppHealthInfo object
        private object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null)
                return null;

            // If obj is a dictionary
            if (obj is IDictionary<string, object> dict)
            {
                if (dict.TryGetValue(propertyName, out var value))
                    return value;
            }

            return null;
        }

        private string GetFirstPropertyValue(dynamic properties, string propertyName)
        {
            if (properties == null || !((IDictionary<string, object>)properties).ContainsKey(propertyName))
            {
                return string.Empty;
            }

            var values = properties[propertyName];
            if (values is IEnumerable<object> enumerable && enumerable.Any())
            {
                return enumerable.First().ToString();
            }

            return string.Empty;
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

            var containerApps = new List<ContainerAppDescriptor>();

            try
            {
                string query = $@"
                    g.V()
                    .has('subscriptionId', '{subscriptionId}')
                    .hasLabel('{Graph.Crawler.ARM.Constants.ContainerAppType.ToLower()}')
                    .project('id', 'name', 'type', 'properties')
                    .by(id())
                    .by(coalesce(values('resourceName'), constant('')))
                    .by(label())
                    .by(valueMap())";

                var result = await _databaseClient.Query(query);

                if (result == null || !result.Any())
                {
                    _logger.LogInformation($"No container apps found for subscription {subscriptionId} in graph database.");
                    return containerApps;
                }

                foreach (var containerApp in result)
                {
                    var properties = containerApp["properties"];
                    
                    string id = containerApp["id"].ToString();
                    string resourceId = id.Replace("_", "/");
                    
                    string name = containerApp["name"]?.ToString() ?? "";
                    string location = GetFirstPropertyValue(properties, "location");
                    string workloadProfile = GetFirstPropertyValue(properties, "workloadProfileName");
                    string state = GetFirstPropertyValue(properties, "provisioningState");
                    string resourceGroup = GetFirstPropertyValue(properties, "resourceGroupName");
                    string fqdn = GetFirstPropertyValue(properties, "fqdn");
                    string environmentName = GetFirstPropertyValue(properties, "managedEnvironmentId");
                    
                    bool isIngressEnabled = false;
                    string ingressExternalValue = GetFirstPropertyValue(properties, "ingressExternal");
                    if (!string.IsNullOrEmpty(ingressExternalValue))
                    {
                        if (!bool.TryParse(ingressExternalValue, out isIngressEnabled))
                        {
                            _logger.LogWarning($"Could not parse ingressExternal value: {ingressExternalValue} to boolean");
                        }
                    }

                    var containerAppDescriptor = new ContainerAppDescriptor(
                        ResourceId: resourceId,
                        Name: name,
                        Location: location,
                        WorkloadProfile: workloadProfile,
                        State: state,
                        ResourceGroup: resourceGroup,
                        Fqdn: fqdn, 
                        EnvironmentName: environmentName,
                        IsIngressEnabled: isIngressEnabled,
                        Revisions: null); // skipping revisions for now

                    containerApps.Add(containerAppDescriptor);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ListContainerAppsAsync with subscription {subscriptionId}");
                return new List<ContainerAppDescriptor>();
            }

            return containerApps;
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

        public async Task<bool> CreateOrUpdateNSGRuleAsync(
            [Description("Azure resource ID of the NSG to update")] string nsgResourceId,
            [Description("The security rule data object containing all rule configuration")] SecurityRuleData rule)
        {
            _logger.LogInformation($"[create_or_update_nsg_rule] Invoked for rule '{rule.Name}' on NSG: {nsgResourceId}");

            try
            {
                var credential = _authService.GetArmOperationCredential();
                var armClient = new ArmClient(credential);

                // Get the NSG resource
                var nsgResource = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgResourceId));

                // Check if the NSG exists
                await nsgResource.GetAsync();

                // Get the security rules collection and create/update the rule
                SecurityRuleCollection securityRules = nsgResource.GetSecurityRules();

                try
                {
                    // Check if the rule exists
                    await securityRules.GetAsync(rule.Name);
                    _logger.LogInformation($"Updating existing security rule '{rule.Name}' in NSG {nsgResourceId}");
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogInformation($"Security rule '{rule.Name}' not found in NSG {nsgResourceId}, creating new rule");
                }

                // CreateOrUpdate handles both creating a new rule and updating an existing one
                await securityRules.CreateOrUpdateAsync(WaitUntil.Completed, rule.Name, rule);
                _logger.LogInformation($"Successfully created/updated security rule '{rule.Name}' in NSG {nsgResourceId}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in CreateOrUpdateNSGRuleAsync with nsgResourceId {nsgResourceId}, rule {rule.Name}");
                return false;
            }
        }

        public async Task<bool> RemoveNSGRuleAsync(
            [Description("Azure resource ID of the NSG containing the rule")] string nsgResourceId,
            [Description("Name of the security rule to remove")] string ruleName)
        {
            _logger.LogInformation($"[remove_nsg_rule] Invoked to remove rule '{ruleName}' from NSG: {nsgResourceId}");

            try
            {
                var credential = _authService.GetArmOperationCredential();
                var armClient = new ArmClient(credential);

                // Get the NSG resource
                var nsgResource = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgResourceId));

                // Check if the NSG exists
                await nsgResource.GetAsync();

                // Get the security rules collection
                SecurityRuleCollection securityRules = nsgResource.GetSecurityRules();

                try
                {
                    // Check if the rule exists
                    var existingRule = await securityRules.GetAsync(ruleName);

                    // Delete the rule
                    _logger.LogInformation($"Removing security rule '{ruleName}' from NSG {nsgResourceId}");
                    await existingRule.Value.DeleteAsync(WaitUntil.Completed);
                    _logger.LogInformation($"Successfully removed security rule '{ruleName}' from NSG {nsgResourceId}");
                    return true;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    // Rule doesn't exist, nothing to remove
                    _logger.LogInformation($"Security rule '{ruleName}' not found in NSG {nsgResourceId}, nothing to remove");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in RemoveNSGRuleAsync with nsgResourceId {nsgResourceId}, rule {ruleName}");
                return false;
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
    }
}
