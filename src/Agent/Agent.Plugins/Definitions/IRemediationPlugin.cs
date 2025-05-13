// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions
{
    public interface IRemediationPlugin
    {
        Task<RemediationResult> ScaleAppServicePlanVertically(string resourceId);

        Task<RemediationResult> CollectMemoryDump(string resourceId);

        Task<RemediationResult> RestartWebApp(string resourceId);

        Task<RemediationResult> SuggestNextSku(string resourceId, string direction, string currentSku);

        Task<RemediationResult> StorageAccountSetSharedKeySupport(string resourceId, FeatureState featureState);

        Task<RemediationResult> StorageAccountSetContainerPublicAccess(string resourceId, FeatureState featureState);

        Task<RemediationResult> CosmosDbSetKeyBasedAuthenticationSupport(string resourceId, FeatureState featureState);

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
