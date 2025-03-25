// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class ContainerAppPlugin : IContainerAppPlugin
    {
        private readonly ArmHelper _armHelper;
        private readonly ILogger<ContainerAppPlugin> _logger;
        private readonly IArmClientFactory _armClientFactory;

        public ContainerAppPlugin(ArmHelper armHelper, ILogger<ContainerAppPlugin> logger, IArmClientFactory armClientFactory)
        {
            _armClientFactory = armClientFactory;
            _armHelper = armHelper;
            _logger = logger;
        }

        // This might be redundant since we have ListAllContainerApps method. 
        // However, the MetaAgent is not able to properly pass the list of container apps to the sub agent.
        // So, adding this function to explicitly get detailed info for a container app instance to test the e2e 
        // container app remediation flow. 
        public async Task<ContainerAppDescriptor> GetContainerAppInfoAsync(string resourceId)
        {
            _logger.LogInformation($"[get_container_app] Invoked with resourceId: {resourceId}");

            try
            {
                var credential = new DefaultAzureCredential();
                var armClient = new ArmClient(credential);

                // Parse resource ID into ResourceIdentifier object
                var resourceIdentifier = new ResourceIdentifier(resourceId);
                
                var containerAppResource = armClient.GetContainerAppResource(resourceIdentifier);
                var containerAppResponse = await containerAppResource.GetAsync();
                var containerApp = containerAppResponse.Value;

                if (containerApp == null)
                {
                    _logger.LogWarning($"Container App with ID '{resourceId}' not found.");
                    return null;
                }

                // Get resource group directly from ResourceIdentifier
                string resourceGroup = resourceIdentifier.ResourceGroupName;

                // Collect revisions if available
                /*
                var revisions = new List<RevisionInfo>();
                try
                {
                    await foreach (var revision in containerAppResource.GetContainerAppRevisions().GetAllAsync())
                    {
                        int trafficWeight = revision.Data.TrafficWeight ?? 0;
                        string revisionName = revision.Data.Name;
                        
                        // Extract just the revision part if name contains the app name prefix
                        if (revisionName.Contains("--"))
                        {
                            revisionName = revisionName.Split("--").Last();
                        }
                        
                        revisions.Add(new RevisionInfo(
                            RevisionName: revisionName,
                            IsActive: trafficWeight > 0,
                            TrafficWeight: trafficWeight));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error getting revisions for Container App {containerApp.Data.Name}");
                }
                */

                return new ContainerAppDescriptor(
                    ResourceId: containerApp.Id.ToString(),
                    Name: containerApp.Data.Name,
                    Kind: containerApp.Data.Kind?.ToString() ?? "ContainerApp",
                    Location: containerApp.Data.Location,
                    WorkloadProfile: containerApp.Data.WorkloadProfileName,
                    Fqdn: containerApp.Data.Configuration?.Ingress?.Fqdn ?? "",
                    State: containerApp.Data.ProvisioningState?.ToString() ?? "Unknown",
                    ResourceGroup: resourceGroup,
                    EnvironmentName: containerApp.Data.ManagedEnvironmentId?.ToString() ?? "N/A",
                    IsIngressEnabled: containerApp.Data.Configuration?.Ingress?.External ?? false,
                    Revisions: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetContainerAppAsync with resourceId {resourceId}");
                return null;
            }
        }

        public async Task<RevisionInfo?> GetLatestRevisionAsync(string resourceId)
        {
            _logger.LogInformation($"[get_latest_revision] Invoked with resourceId: {resourceId}");

            try
            {
                var credential = new DefaultAzureCredential();

                var armClient = new ArmClient(credential);

                var containerAppResource = armClient.GetContainerAppResource(new ResourceIdentifier(resourceId));
                                
                var containerApp = await containerAppResource.GetAsync();
                
                // Get the latest revision name directly from the container app properties
                string latestRevisionName = containerApp.Value.Data.LatestRevisionName;
                
                if (string.IsNullOrEmpty(latestRevisionName))
                {
                    _logger.LogWarning($"No latest revision name found for Container App {resourceId}");
                    return null;
                }
                
                // Extract the simple revision name if it contains the app name prefix
                string revisionName = latestRevisionName;
                if (latestRevisionName.Contains("--"))
                {
                    revisionName = latestRevisionName.Split("--").Last();
                }
                
                // Now get the specific revision to get traffic weight
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
                
                // If we found the latest revision, get its traffic weight
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
                    // If we couldn't find the revision details, assume it's active since it's the latest
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
                // Create an instance of the ArmClient to interact with Azure
                var armClient = _armClientFactory.GetArmClient();

                // Construct the Resource Identifier for the specified subscription
                var subscriptionResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}");

                // Get the SubscriptionResource
                SubscriptionResource subscription = armClient.GetSubscriptionResource(subscriptionResourceId);

                // Verify if the subscription exists by attempting to get its data
                var subscriptionResponse = await subscription.GetAsync();

                if (subscriptionResponse.Value == null)
                {
                    throw new InvalidOperationException($"Subscription with ID '{subscriptionId}' not found.");
                }

                // Get all resource groups in the subscription
                await foreach (var resourceGroup in subscription.GetResourceGroups().GetAllAsync())
                {
                    // Filter for the specific resource group "aca-sre-agent-demo"
                    if (!string.Equals(resourceGroup.Data.Name, "aca-sre-agent-demo", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    await foreach (var containerApp in resourceGroup.GetContainerApps().GetAllAsync())
                    {
                        string state = containerApp.Data.ProvisioningState.ToString() ?? "Unknown";

                        string environmentId = containerApp.Data.ManagedEnvironmentId?.ToString() ?? "N/A";

                        // Get revisions for this Container App
                        /*
                        var revisions = new List<RevisionInfo>();
                        try
                        {
                            // Get all revisions for this Container App
                            var revisionCollection = containerApp.GetContainerAppRevisions();
                            await foreach (var revision in revisionCollection.GetAllAsync())
                            {
                                // A revision is considered active if it has traffic weight > 0
                                int trafficWeight = revision.Data.TrafficWeight ?? 0;
                                bool isActive = trafficWeight > 0;

                                // Use the correct property - the revision name is usually the last part of the full name
                                // If there's no specific RevisionName property, extract it from the Name
                                string fullName = revision.Data.Name;
                                string revisionName = fullName;

                                if (fullName.Contains("--"))
                                {
                                    revisionName = fullName.Split("--").Last();
                                }

                                revisions.Add(new RevisionInfo(
                                    RevisionName: revisionName,
                                    IsActive: isActive,
                                    TrafficWeight: trafficWeight));
                            }
                        }
                        catch (Exception revEx)
                        {
                            _logger.LogWarning(revEx, $"Error fetching revisions for Container App {containerApp.Data.Name}");
                        }
                        */
                        var containerAppDescriptor = new ContainerAppDescriptor(
                            ResourceId: containerApp.Id.ToString(),
                            Name: containerApp.Data.Name,
                            Kind: containerApp.Data.Kind.ToString(),
                            Location: containerApp.Data.Location,
                            WorkloadProfile: containerApp.Data.WorkloadProfileName,
                            State: state,
                            Fqdn: containerApp.Data.Configuration?.Ingress?.Fqdn ?? "",
                            ResourceGroup: resourceGroup.Data.Name,
                            EnvironmentName: environmentId,
                            IsIngressEnabled: containerApp.Data.Configuration?.Ingress?.External ?? false,
                            Revisions: null);

                        containerApps.Add(containerAppDescriptor);
                    }
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation($"Subscription with ID '{subscriptionId}' not found.");
                return new List<ContainerAppDescriptor>();
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
                var credential = new DefaultAzureCredential();
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
                var credential = new DefaultAzureCredential();
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
                var credential = new DefaultAzureCredential();
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

                var credential = new DefaultAzureCredential();
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