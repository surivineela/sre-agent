// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Services
{
    public interface ICrawlerTriggerService
    {
        void TriggerArmCrawl(string resourceId);
        void TriggerArmCrawl(string resourceId, bool force = false);
        void TriggerArmCrawl(IEnumerable<string> resourceIds);
        void TriggerKubernetesCrawl(string clusterResourceId, string? namespaceName, string resourceName, string group, string apiVersion, string kind, bool isDelete = false);
        void MarkResourceAsDeleted(TriggerItem item);
        IAsyncEnumerable<TriggerItem> GetResourceIdsToProcess(CancellationToken cancellationToken = default);
    }
}
