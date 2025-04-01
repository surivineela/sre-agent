// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions
{
    public interface IRemediationPlugin
    {
        Task<RemediationResult> ScaleAppServicePlanVertically(string resourceId);

        Task<RemediationResult> CollectMemoryDump(string resourceId);

        Task<RemediationResult> RestartWebApp(string resourceId);

        Task<RemediationResult> SuggestNextSku(string resourceId, string direction, string currentSku);

        Task<RemediationResult> StorageAccountDisableSharedKeySupport(string resourceId);

        Task<RemediationResult> StorageAccountDisablePublicContainers(string resourceId);

        Task<RemediationResult> CalculateScalingCost(
            string resourceId,
            string direction,
            string currentSku,
            string targetSku);
    }
}
