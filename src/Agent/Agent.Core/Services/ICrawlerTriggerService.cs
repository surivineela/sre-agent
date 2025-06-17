// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Services
{
    public interface ICrawlerTriggerService
    {
        void TriggerCrawl(string resourceId);
        void TriggerCrawl(string resourceId, string? threadId = null, bool force = false);
        void TriggerCrawl(IEnumerable<string> resourceIds);
        void MarkResourceAsDeleted(string resourceId);
        IAsyncEnumerable<string> GetResourceIdsToProcess(CancellationToken cancellationToken = default);
        HashSet<string> GetThreadIdsForResource(string resourceId);
    }
}
