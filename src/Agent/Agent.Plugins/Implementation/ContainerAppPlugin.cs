// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class ContainerAppPlugin : IContainerAppPlugin
    {
        private readonly ArmHelper _armHelper;
        private readonly ILogger<ContainerAppPlugin> _logger;

        public ContainerAppPlugin(ArmHelper armHelper, ILogger<ContainerAppPlugin> logger)
        {
            _armHelper = armHelper;
            _logger = logger;
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
                // Authenticate using DefaultAzureCredential
                var credential = new DefaultAzureCredential();

                // Create an instance of the ArmClient to interact with Azure
                var armClient = new ArmClient(credential);

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
                    await foreach (var containerApp in resourceGroup.GetContainerApps().GetAllAsync())
                    {
                        string state = containerApp.Data.ProvisioningState.ToString() ?? "Unknown";

                        string environmentId = containerApp.Data.ManagedEnvironmentId?.ToString() ?? "N/A";

                        // Get revisions for this Container App
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

                        var containerAppDescriptor = new ContainerAppDescriptor(
                            ResourceId: containerApp.Id.ToString(),
                            Name: containerApp.Data.Name,
                            Kind: containerApp.Data.Kind.ToString(),
                            Location: containerApp.Data.Location,
                            WorkloadProfile: containerApp.Data.WorkloadProfileName,
                            State: state,
                            ResourceGroup: resourceGroup.Data.Name,
                            Environment: environmentId,
                            IsIngressEnabled: containerApp.Data.Configuration?.Ingress?.External ?? false,
                            Revisions: revisions);

                        containerApps.Add(containerAppDescriptor);
                    }
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation($"Subscription with ID '{subscriptionId}' not found.");
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in ListContainerAppsAsync with subscription {subscriptionId}");
                return [];
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
    }
}