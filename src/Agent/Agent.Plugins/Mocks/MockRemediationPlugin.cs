// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;

namespace Agent.Plugins.Mocks
{
    public class MockRemediationPlugin : IRemediationPlugin
    {
        private readonly TimeProvider _timeProvider;
        private readonly MockArmPlugin _armPlugin;

        public MockRemediationPlugin(TimeProvider timeProvider, IArmPlugin armPlugin)
        {
            _timeProvider = timeProvider;
            _armPlugin = (MockArmPlugin)armPlugin;
        }

        public async Task<RemediationResult> ScaleAppServicePlanVertically(string resourceId)
        {
            return new RemediationResult(
                Success: true,
                Action: "ScaleAppServicePlan",
                Details: $"Successfully initiated scaling of App Service Plan for {resourceId}.",
                OperationId: Guid.NewGuid().ToString(),
                FinishedTime: _timeProvider.GetUtcNow().DateTime);
        }

        public async Task<RemediationResult> CollectMemoryDump(string resourceId)
        {
            return new RemediationResult(
                Success: true,
                Action: "CollectMemoryDump",
                Details: $"Memory dump collection initiated for {resourceId}. Memory dump will be available in the storage account.",
                OperationId: Guid.NewGuid().ToString(),
                FinishedTime: _timeProvider.GetUtcNow().DateTime);
        }

        public async Task<RemediationResult> RestartWebApp(string resourceId)
        {
            bool success = await _armPlugin.RestartWebApp(resourceId);

            return new RemediationResult(
                Success: success,
                Action: "RestartWebApp",
                Details: success ? $"Web App {resourceId} restarted successfully." : $"Failed to restart Web App {resourceId}.",
                OperationId: success ? Guid.NewGuid().ToString() : null,
                FinishedTime: _timeProvider.GetUtcNow().DateTime);
        }

        public async Task<RemediationResult> SuggestNextSku(string resourceId, string direction, string currentSku)
        {
            throw new NotImplementedException();
        }

        public async Task<RemediationResult> CalculateScalingCost(string resourceId, string direction, string currentSku, string targetSku)
        {
            throw new NotImplementedException();
        }

        public Task<RemediationResult> StorageAccountSetSharedKeySupport(string resourceId, FeatureState featureState)
        {
            return Task.FromResult(
                new RemediationResult(
                    Success: true,
                    Action: "DisabledSharedKey",
                    Details: $"Storage account {resourceId} can no longer use shared keys.",
                    OperationId: Guid.NewGuid().ToString(),
                    FinishedTime: _timeProvider.GetUtcNow().DateTime
            ));
        }

        public Task<RemediationResult> StorageAccountSetContainerPublicAccess(string resourceId, FeatureState featureState)
        {
            return Task.FromResult(
                new RemediationResult(
                    Success: true,
                    Action: "DisablePublicContainers",
                    Details: $"All Containers in {resourceId} have been set to private and public container feature disabled.",
                    OperationId: Guid.NewGuid().ToString(),
                    FinishedTime: _timeProvider.GetUtcNow().DateTime
            ));
        }

        public Task<RemediationResult> CosmosDbSetKeyBasedAuthSupport(string resourceId, FeatureState featureState)
        {
            return Task.FromResult(
                new RemediationResult(
                    Success: true,
                    Action: "SetLocalAuthSupport",
                    Details: $"Local Auth in {resourceId} have been set to {featureState}.",
                    OperationId: Guid.NewGuid().ToString(),
                    FinishedTime: _timeProvider.GetUtcNow().DateTime
            ));
        }

        public Task<RemediationResult> EventHubSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return Task.FromResult(
                new RemediationResult(
                    Success: true,
                    Action: "SetLocalAuthSupport",
                    Details: $"Local Auth in {resourceId} have been set to {featureState}.",
                    OperationId: Guid.NewGuid().ToString(),
                    FinishedTime: _timeProvider.GetUtcNow().DateTime
            ));
        }

        public Task<RemediationResult> ServiceBusSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return Task.FromResult(
                new RemediationResult(
                    Success: true,
                    Action: "SetLocalAuthSupport",
                    Details: $"Local Auth in {resourceId} have been set to {featureState}.",
                    OperationId: Guid.NewGuid().ToString(),
                    FinishedTime: _timeProvider.GetUtcNow().DateTime
            ));
        }

        public Task<RemediationResult> AzureSqlServerSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return Task.FromResult(
                new RemediationResult(
                    Success: true,
                    Action: "AzureSqlServerSetLocalAuthSupport",
                    Details: $"Local Auth in {resourceId} have been set to {featureState}.",
                    OperationId: Guid.NewGuid().ToString(),
                    FinishedTime: _timeProvider.GetUtcNow().DateTime
            ));
        }

        public Task<RemediationResult> AzureAppServiceSetFtpAuthenticationSupport(string resourceId, FeatureState featureState)
        {
            return Task.FromResult(
                new RemediationResult(
                    Success: true,
                    Action: "AzureAppServiceSetFtpAuthenticationSupport",
                    Details: $"FTP Authentication in {resourceId} have been set to {featureState}.",
                    OperationId: Guid.NewGuid().ToString(),
                    FinishedTime: _timeProvider.GetUtcNow().DateTime
            ));
        }

        public Task<RemediationResult> AzureAppServiceSetScmAuthenticationSupport(string resourceId, FeatureState featureState)
        {
            return Task.FromResult(
                new RemediationResult(
                    Success: true,
                    Action: "AzureAppServiceSetScmAuthenticationSupport",
                    Details: $"SCM Authentication in {resourceId} have been set to {featureState}.",
                    OperationId: Guid.NewGuid().ToString(),
                    FinishedTime: _timeProvider.GetUtcNow().DateTime
            ));
        }
    }
}

