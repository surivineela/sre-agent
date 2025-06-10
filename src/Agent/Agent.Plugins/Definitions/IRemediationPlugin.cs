// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions
{
    public interface IRemediationPlugin
    {
        public Guid? ThreadId { get; set; }
        Task<RemediationResult> ScaleAppServicePlanVertically(string resourceId);

        Task<RemediationResult> CollectMemoryDump(string resourceId);

        Task<RemediationResult> RestartWebApplication(string resourceId);

        Task<RemediationResult> SuggestNextSku(string resourceId, string direction, string currentSku);

        Task<RemediationResult> StorageAccountSetSharedKeySupport(string resourceId, FeatureState featureState);

        Task<RemediationResult> StorageAccountSetContainerPublicAccess(string resourceId, FeatureState featureState);

        Task<RemediationResult> CosmosDbSetKeyBasedAuthSupport(string resourceId, FeatureState featureState);

        Task<RemediationResult> EventHubSetLocalAuthSupport(string resourceId, FeatureState featureState);

        Task<RemediationResult> ServiceBusSetLocalAuthSupport(string resourceId, FeatureState featureState);

        Task<RemediationResult> AzureSqlServerSetLocalAuthSupport(string resourceId, FeatureState featureState);

        Task<RemediationResult> AzureAppServiceSetFtpAuthenticationSupport(string resourceId, FeatureState featureState);

        Task<RemediationResult> AzureAppServiceSetScmAuthenticationSupport(string resourceId, FeatureState featureState);

        Task<RemediationResult> CalculateScalingCost(
            string resourceId,
            string direction,
            string currentSku,
            string targetSku);
    }
}
