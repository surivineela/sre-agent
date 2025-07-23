// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Runtime.CompilerServices;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Sql.Models;
using Azure.ResourceManager.Storage.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class RemediationPlugin : IRemediationPlugin
    {
        public Guid? ThreadId { get; set; }
        private readonly ILogger? _logger;
        private readonly ArmHelper _armHelper;
        private readonly IAuthenticationService _authService;
        private static readonly Dictionary<string, decimal> HourlyRates = new(StringComparer.OrdinalIgnoreCase)
        {
            {"F1", 0}, {"D1", 0},
            {"B1", 0.074M}, {"B2", 0.149M}, {"B3", 0.298M},
            {"S1", 0.100M}, {"S2", 0.199M}, {"S3", 0.399M},
            {"Premium1v2", 0.227M}, {"Premium2v2", 0.454M}, {"Premium3v2", 0.908M},
            {"Premium0v3", 0.078M}, {"Premium1v3", 0.252M}, {"Premium2v3", 0.504M}, {"Premium3v3", 1.008M},
        };

        public RemediationPlugin(ILogger<RemediationPlugin> logger, ArmHelper armHelper, IAuthenticationService authService)
        {
            _authService = authService;
            _armHelper = armHelper;
            _logger = logger;
        }

        public async Task<RemediationResult> CalculateScalingCost(string resourceId, string direction, string currentSku, string targetSku)
        {
            try
            {
                Console.WriteLine($"[calculate_scaling_cost] Invoked with resourceId: {resourceId}, direction: {direction}, currentSku: {currentSku}, targetSku: {targetSku}");
                var appServicePlanId = await _armHelper.GetAppServicePlanNameAsync(resourceId);

                if (!HourlyRates.TryGetValue(currentSku, out var currentRate))
                    return new RemediationResult(false,
                        "Cost Calculation",
                        "Current SKU rate not found",
                        OperationId: null,
                        FinishedTime: DateTime.Now);

                if (!HourlyRates.TryGetValue(targetSku, out var targetRate))
                    return new RemediationResult(
                        false,
                        "Cost Calculation",
                        "Target SKU rate not found",
                        OperationId: null,
                        FinishedTime: DateTime.Now);

                var hourlyDiff = targetRate - currentRate;
                var dailyDiff = hourlyDiff * 24;
                var monthlyDiff = dailyDiff * 30;

                return new RemediationResult(
                    Success: true,
                    Action: $"Cost difference for scaling {direction} from {currentSku} to {targetSku}",
                    Details: $"Hourly: ${hourlyDiff:F3}\nDaily: ${dailyDiff:F2}\nMonthly: ${monthlyDiff:F2}",
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
            catch (Exception ex)
            {
                return new RemediationResult(
                    Success: false,
                    Action: "Failed to calculate scaling costs",
                    Details: ex.Message,
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
        }

        public async Task<RemediationResult> CollectMemoryDump(string resourceId)
        {
            _logger?.LogInternalInformation("[CollectMemoryDump] Starting memory dump collection for resource: {ResourceId}", resourceId);
            try
            {
                _logger?.LogInternalInformation($"[collect_memory_dump] Invoked with resourceId: {resourceId}");
                var dumpPath = await _armHelper.TakeMemoryDumpAsync(resourceId);

                return new RemediationResult(
                    Success: !string.IsNullOrEmpty(dumpPath),
                    Action: "Memory dump collected",
                    Details: !string.IsNullOrEmpty(dumpPath) ?
                        $"Dump available at: {dumpPath}" :
                        "Failed to collect memory dump",
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "[CollectMemoryDump] Error during memory dump collection for resource {ResourceId}: {ErrorMessage}",
                    resourceId, ex.Message);
                return new RemediationResult(
                    Success: false,
                    Action: "Failed to collect memory dump",
                    Details: ex.Message,
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
        }

        public async Task<RemediationResult> RestartWebApplication(string resourceId)
        {
            try
            {
                _logger?.LogInternalInformation($"[restart_webapp] Invoked with resourceId: {resourceId}");

                var response = await _armHelper.RestartWebAppAsync(resourceId);

                return new RemediationResult(
                    Success: response.IsSuccessStatusCode,
                    Action: "Restarted Web App",
                    Details: response.IsSuccessStatusCode ?
                        "Restart completed successfully" :
                        $"Failed to restart: {response.ReasonPhrase}",
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
            catch (Exception ex)
            {
                return new RemediationResult(
                    Success: false,
                    Action: "Failed to restart Web App",
                    Details: ex.Message,
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
        }

        public async Task<RemediationResult> ScaleAppServicePlanVertically(string resourceId)
        {
            _logger?.LogInternalInformation("[ScaleAppServicePlanVertically] Starting vertical scaling for resource: {ResourceId}", resourceId);
            try
            {
                // Get App Service Plan ID from Web App
                var appServicePlanId = await _armHelper.GetAppServicePlanNameAsync(resourceId);
                _logger?.LogInternalInformation("[ScaleAppServicePlanVertically] Retrieved App Service Plan ID: {AppServicePlanId}", appServicePlanId);

                // Get current SKU
                var currentSku = await _armHelper.GetCurrentSkuAsync(appServicePlanId);
                _logger?.LogInternalInformation("[ScaleAppServicePlanVertically] Current SKU: {CurrentSku}", currentSku.Name);

                // Get next SKU in progression
                var targetSku = ArmHelper.GetNextSku(currentSku);
                _logger?.LogInternalInformation("[ScaleAppServicePlanVertically] Target SKU for scaling: {TargetSku}", targetSku.Name);

                // Perform scaling operation
                var success = await _armHelper.ScaleUpAppServicePlanByNameAsync(
                    appServicePlanId,
                    targetSku);

                if (success)
                {
                    _logger?.LogInternalInformation("[ScaleAppServicePlanVertically] Successfully scaled to {TargetSku}", targetSku.Name);
                }
                else
                {
                    _logger?.LogInternalError("[ScaleAppServicePlanVertically] Failed to scale to {TargetSku}", targetSku.Name);
                }

                return new RemediationResult(
                    Success: success,
                    Action: $"Scaled App Service Plan to {targetSku.Name}",
                    Details: $"Previous tier: {currentSku.Name}",
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "[ScaleAppServicePlanVertically] Error during scaling for resource {ResourceId}: {ErrorMessage}",
                    resourceId, ex.Message);
                return new RemediationResult(
                    Success: false,
                    Action: "Failed to scale App Service Plan",
                    Details: ex.Message,
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
        }

        public async Task<RemediationResult> SuggestNextSku(string resourceId, string direction, string currentSku)
        {
            try
            {
                _logger?.LogInternalInformation($"[possible_next_sku] Invoked with resourceId: {resourceId}, direction: {direction}, currentSku: {currentSku}");
                var appServicePlanId = await _armHelper.GetAppServicePlanNameAsync(resourceId);

                // Validate current SKU exists
                if (!HourlyRates.TryGetValue(currentSku, out var currentRate))
                {
                    return new RemediationResult(
                        false,
                        "SKU Suggestion",
                        $"Current SKU {currentSku} not found in rate table",
                        OperationId: null,
                        FinishedTime: DateTime.Now);
                }

                // Get the family prefix of the current SKU
                string family = GetSkuFamily(currentSku);

                // Get all SKUs in the same family
                var familySkus = HourlyRates
                    .Where(kvp => GetSkuFamily(kvp.Key) == family)
                    .OrderBy(kvp => kvp.Value)
                    .ToList();

                // Find current SKU position
                int currentIndex = familySkus.FindIndex(kvp => kvp.Key.Equals(currentSku, StringComparison.OrdinalIgnoreCase));

                // Determine next SKU based on direction
                KeyValuePair<string, decimal>? nextSku = null;
                if (direction.Equals("up", StringComparison.OrdinalIgnoreCase) && currentIndex < familySkus.Count - 1)
                {
                    nextSku = familySkus[currentIndex + 1];
                }
                else if (direction.Equals("down", StringComparison.OrdinalIgnoreCase) && currentIndex > 0)
                {
                    nextSku = familySkus[currentIndex - 1];
                }

                if (nextSku == null)
                {
                    return new RemediationResult(
                        false,
                        "SKU Suggestion",
                        $"No {direction} scaling option available for SKU {currentSku} in family {family}",
                        OperationId: null,
                        FinishedTime: DateTime.Now);
                }

                // Calculate cost differences
                var hourlyDiff = nextSku.Value.Value - currentRate;
                var dailyDiff = hourlyDiff * 24;
                var monthlyDiff = dailyDiff * 30;

                return new RemediationResult(
                    Success: true,
                    Action: $"Suggested SKU for scaling {direction} from {currentSku}",
                    Details: $"Suggested SKU: {nextSku.Value.Key}\n" +
                            $"Cost difference:\n" +
                            $"Hourly: ${hourlyDiff:F3}\n" +
                            $"Daily: ${dailyDiff:F2}\n" +
                            $"Monthly: ${monthlyDiff:F2}",
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
            catch (Exception ex)
            {
                return new RemediationResult(
                    Success: false,
                    Action: "Failed to suggest next SKU",
                    Details: ex.Message,
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
        }

        public async Task<RemediationResult> StorageAccountSetContainerPublicAccess(string resourceId, FeatureState featureState)
        {
            return await RemediateArmResource(
                resourceId,
                "Storage Account",
                fetchResourceFunc: (id) => _armHelper.GetStorageAccountAsync(id),
                remediationActionFunc: async (resource) =>
                {
                    bool desiredState = featureState == FeatureState.Enabled;
                    if (desiredState != resource.Data.AllowBlobPublicAccess)
                    {
                        var blobService = resource.GetBlobService();
                        var containers = blobService.GetBlobContainers().GetAllAsync();

                        if (!desiredState)
                        {
                            await foreach (var container in containers)
                            {
                                if (container.Data.PublicAccess != StoragePublicAccessType.None)
                                {
                                    if (_logger != null)
                                    {
                                        _logger.LogInternalInformation($"Disabling public access to container: {container.Data.Name}");
                                    }

                                    container.Data.PublicAccess = StoragePublicAccessType.None;
                                    await container.UpdateAsync(container.Data);
                                }
                            }
                        }

                        await _armHelper.SetStorageAccountContainerPublicAccess(resourceId, featureState);
                    }
                });
        }

        public async Task<RemediationResult> StorageAccountSetSharedKeySupport(string resourceId, FeatureState featureState)
        {
            return await RemediateArmResource(
                resourceId,
                "Storage Account",
                fetchResourceFunc: (id) => _armHelper.GetStorageAccountAsync(id),
                remediationActionFunc: async (resource) =>
                {
                    bool desiredState = featureState == FeatureState.Enabled;
                    if (desiredState != resource.Data.AllowSharedKeyAccess)
                    {
                        _logger?.LogInternalInformation($"Setting shared key access to {desiredState} for storage account");
                        await _armHelper.SetStorageAccountSharedKeySupportAsync(resourceId, featureState);
                    }
                });
        }

        public async Task<RemediationResult> CosmosDbSetKeyBasedAuthSupport(string resourceId, FeatureState featureState)
        {
            return await RemediateArmResource(
                resourceId,
                "CosmosDb",
                fetchResourceFunc: (id) => _armHelper.GetCosmosDbAccountAsync(id),
                remediationActionFunc: async (resource) =>
                {
                    _logger?.LogInternalInformation($"Setting local auth support for CosmosDb");
                    await _armHelper.SetCosmosDbLocalAuthSupport(resourceId, featureState);
                });
        }

        public async Task<RemediationResult> EventHubSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return await RemediateArmResource(
                resourceId,
                "EventHub",
                fetchResourceFunc: (id) => _armHelper.GetEventHubAccountAsync(id),
                remediationActionFunc: async (resource) =>
                {
                    _logger?.LogInternalInformation($"Setting local auth support for Event Hub");
                    await _armHelper.SetEventHubLocalAuthSupport(resourceId, featureState);
                });
        }

        public async Task<RemediationResult> ServiceBusSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return await RemediateArmResource(
                resourceId,
                "ServiceBus",
                fetchResourceFunc: (id) => _armHelper.GetServiceBusAccountAsync(id),
                remediationActionFunc: async (resource) =>
                {
                    _logger?.LogInternalInformation($"Setting local auth support for Service Bus");
                    await _armHelper.SetServiceBusLocalAuthSupport(resourceId, featureState);
                });
        }

        public async Task<RemediationResult> AzureSqlServerSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return await RemediateArmResource(
                resourceId,
                "SqlServer",
                fetchResourceFunc: (id) => _armHelper.GetSqlServerAsync(id),
                remediationActionFunc: async (resource) =>
                {
                    _logger?.LogInternalInformation($"Setting local auth support for Sql Server");
                    if (resource.Data.Administrators?.AdministratorType == SqlAdministratorType.ActiveDirectory)
                    {
                        _logger?.LogInternalInformation($"Setting local auth support for Sql Server");
                        await _armHelper.SetSqlServerEntraAuthSupport(resourceId, featureState);
                    }
                    else
                    {
                        _logger?.LogInternalInformation($"Sql Server is not configured with an AD Administrator.");
                    }
                });
        }

        public async Task<RemediationResult> AzureAppServiceSetFtpAuthenticationSupport(string resourceId, FeatureState featureState)
        {
            return await RemediateArmResource(
                resourceId,
                "App Service",
                fetchResourceFunc: _armHelper.GetAppServiceAsync,
                remediationActionFunc: async (resource) =>
                {
                    _logger?.LogInternalInformation($"Setting FTP authentication support for App Service");
                    await _armHelper.SetWebSiteFtpAuthenticationSupport(resourceId, featureState);
                });
        }

        public async Task<RemediationResult> AzureAppServiceSetScmAuthenticationSupport(string resourceId, FeatureState featureState)
        {
            return await RemediateArmResource(
                resourceId,
                "App Service",
                fetchResourceFunc: _armHelper.GetAppServiceAsync,
                remediationActionFunc: async (resource) =>
                {
                    _logger?.LogInternalInformation($"Setting SCM authentication support for App Service");
                    await _armHelper.SetWebSiteScmAuthenticationSupport(resourceId, featureState);
                });
        }

        private static string GetSkuFamily(string sku)
        {
            if (string.IsNullOrEmpty(sku)) return string.Empty;

            // Handle free and shared tiers
            if (sku.StartsWith("F") || sku.StartsWith("D"))
                return sku[0].ToString();

            // Handle Basic tier
            if (sku.StartsWith("B"))
                return "Basic";

            // Handle Standard tier
            if (sku.StartsWith("S"))
                return "Standard";

            // Handle Premium v2 and v3 tiers
            if (sku.Contains("v2", StringComparison.OrdinalIgnoreCase))
                return "Premiumv2";
            if (sku.Contains("v3", StringComparison.OrdinalIgnoreCase))
                return "Premiumv3";

            return string.Empty;
        }

        /// <summary>
        /// Performs a given remediation on an ARM resource, after checking for management locks.
        /// Will return the correct failure/success message based on the outcome of the remediation.
        /// </summary>
        /// <typeparam name="T">Must be an ArmResource derivative</typeparam>
        /// <param name="resourceId">The full resource ID; this will be checked for correctness.</param>
        /// <param name="resourceTypeName">The name of the type of resource, used for human readable messages.</param>
        /// <param name="fetchResourceFunc">A function that returns the ArmResource given the resource ID</param>
        /// <param name="remediationActionFunc">A function that performs the actual remediation.</param>
        /// <param name="operationName">Pulled automatically from the calling method; for messages.</param>
        /// <returns>A remediation result which describes the action taken (or the error)</returns>
        private async Task<RemediationResult> RemediateArmResource<T>(
            string resourceId,
            string resourceTypeName,
            Func<string, Task<T>> fetchResourceFunc,
            Func<T, Task> remediationActionFunc,
            [CallerMemberName] string operationName = ""
        ) where T : ArmResource
        {
            try
            {
                _logger?.LogInternalInformation($"[{operationName}] Invoked with resourceId: {resourceId}");

                // Fetch the resource
                var resource = await fetchResourceFunc(resourceId);

                // Check for management locks
                var managementLockResources = resource.GetManagementLocks().GetAllAsync();
                await foreach (var managementLock in managementLockResources)
                {
                    if (managementLock.Data.Level == ManagementLockLevel.ReadOnly)
                    {
                        _logger?.LogInternalInformation($"[{operationName}] {resourceTypeName} is locked preventing updates");
                        return new RemediationResult(
                            Success: false,
                            Action: $"Failed to {operationName}",
                            Details: $"{resourceTypeName} is locked preventing updates",
                            OperationId: null,
                            FinishedTime: DateTime.Now);
                    }
                }

                // Perform the remediation action
                await remediationActionFunc(resource);

                return new RemediationResult(
                    Success: true,
                    Action: $"{operationName} completed successfully",
                    Details: $"{operationName} was applied to {resource.Id}",
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, $"[{operationName}] Error during operation for resource {resourceId}: {ex.Message}");
                return new RemediationResult(
                    Success: false,
                    Action: $"Failed to {operationName}",
                    Details: ex.Message,
                    OperationId: null,
                    FinishedTime: DateTime.Now);
            }
        }
    }
}
