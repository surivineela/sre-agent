// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services
{
    public class CrawlerTriggerService : ICrawlerTriggerService
    {
        private readonly ConcurrentQueue<string> _resourceIdQueue = new();
        private readonly SemaphoreSlim _semaphore = new(0);
        private readonly ILogger<CrawlerTriggerService> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _recentlyCrawled = new();
        private readonly ConcurrentDictionary<string, DateTime> _deletedResources = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _resourceThreadIds = new();
        private readonly HashSet<string> _pendingResourceIds = new();
        private readonly object _pendingLock = new();
        private static readonly Regex ArmResourceIdRegex = new(
            @"/subscriptions/[a-fA-F0-9-]+/resourceGroups/[^/]+/providers/[^/]+/[^/]+/[a-zA-Z0-9\-_]+(?:/[a-zA-Z0-9\-_]+)*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Consider a resource recently crawled if it was crawled within the last 3 minutes
        private static readonly TimeSpan RecentCrawlThreshold = TimeSpan.FromMinutes(3);
        // Keep track of deleted resources for 30 minute to avoid unnecessary crawl attempts
        private static readonly TimeSpan DeletedResourceThreshold = TimeSpan.FromMinutes(30);

        public CrawlerTriggerService(ILogger<CrawlerTriggerService> logger)
        {
            _logger = logger;

            // Start background cleanup task for recently crawled cache
            _ = Task.Run(CleanupRecentlyCrawledCache);
        }

        // Trigger a crawl for a single ARM resource ID
        public void TriggerCrawl(string resourceId)
        {
            TriggerCrawl(resourceId, null, false);
        }

        public void TriggerCrawl(string resourceId, string? threadId = null, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return;

            // Extract ARM resource IDs from the input string
            var matches = ArmResourceIdRegex.Matches(resourceId);
            foreach (Match match in matches)
            {
                var extractedResourceId = match.Value.Trim().TrimEnd('"', ',');

                // Track thread ID if provided
                if (!string.IsNullOrWhiteSpace(threadId))
                {
                    _resourceThreadIds.AddOrUpdate(extractedResourceId,
                        new HashSet<string> { threadId },
                        (key, existing) =>
                        {
                            lock (existing)
                            {
                                existing.Add(threadId);
                                return existing;
                            }
                        });
                }

                // Skip cache checks only if force is false
                if (!force)
                {
                    // Check if this resource was recently marked as deleted
                    if (IsRecentlyDeleted(extractedResourceId))
                    {
                        _logger.LogDebug("Skipping recently deleted resource: {ResourceId}", extractedResourceId);
                        continue;
                    }

                    // Check if this resource was recently crawled
                    if (IsRecentlyCrawled(extractedResourceId))
                    {
                        _logger.LogDebug("Skipping recently crawled resource: {ResourceId}", extractedResourceId);
                        continue;
                    }
                }

                // Check if this resource is already pending
                lock (_pendingLock)
                {
                    if (_pendingResourceIds.Contains(extractedResourceId))
                    {
                        _logger.LogDebug("Resource already pending for crawl: {ResourceId}", extractedResourceId);
                        continue;
                    }

                    _pendingResourceIds.Add(extractedResourceId);
                }

                _resourceIdQueue.Enqueue(extractedResourceId);
                _semaphore.Release();
                _logger.LogInternalInformation("{Action} queued resource ID for crawling: {ResourceId} (ThreadId: {ThreadId})",
                    force ? "Force" : "", extractedResourceId, threadId ?? "N/A");
            }
        }

        public void TriggerCrawl(IEnumerable<string> resourceIds)
        {
            foreach (var resourceId in resourceIds)
            {
                TriggerCrawl(resourceId);
            }
        }

        public void MarkResourceAsDeleted(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return;

            _deletedResources[resourceId] = DateTime.UtcNow;
            _logger.LogDebug("Marked resource as deleted: {ResourceId}", resourceId);
        }

        public async IAsyncEnumerable<string> GetResourceIdsToProcess([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _semaphore.WaitAsync(cancellationToken);

                if (_resourceIdQueue.TryDequeue(out var resourceId))
                {
                    // Remove from pending set and mark as recently crawled
                    lock (_pendingLock)
                    {
                        _pendingResourceIds.Remove(resourceId);
                    }

                    // Always update the timestamp, even if the resource was previously crawled
                    _recentlyCrawled[resourceId] = DateTime.UtcNow;
                    yield return resourceId;
                }
            }
        }

        public HashSet<string> GetThreadIdsForResource(string resourceId)
        {
            if (_resourceThreadIds.TryGetValue(resourceId, out var threadIds))
            {
                lock (threadIds)
                {
                    return new HashSet<string>(threadIds);
                }
            }
            return new HashSet<string>();
        }

        private bool IsRecentlyCrawled(string resourceId)
        {
            if (_recentlyCrawled.TryGetValue(resourceId, out var lastCrawlTime))
            {
                return DateTime.UtcNow - lastCrawlTime < RecentCrawlThreshold;
            }
            return false;
        }

        private bool IsRecentlyDeleted(string resourceId)
        {
            if (_deletedResources.TryGetValue(resourceId, out var deletedTime))
            {
                return DateTime.UtcNow - deletedTime < DeletedResourceThreshold;
            }
            return false;
        }

        private async Task CleanupRecentlyCrawledCache()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1)); // Cleanup every minute

                    var cutoffTime = DateTime.UtcNow - RecentCrawlThreshold;
                    var deletedCutoffTime = DateTime.UtcNow - DeletedResourceThreshold;
                    var expiredThreadIdKeys = new List<string>();

                    // Clean up recently crawled cache
                    var expiredCrawledKeys = _recentlyCrawled
                        .Where(kvp => kvp.Value < cutoffTime)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in expiredCrawledKeys)
                    {
                        _recentlyCrawled.TryRemove(key, out _);
                        if (_resourceThreadIds.TryRemove(key, out _))
                        {
                            expiredThreadIdKeys.Add(key);
                        }
                    }

                    // Clean up deleted resources cache
                    var expiredDeletedKeys = _deletedResources
                        .Where(kvp => kvp.Value < deletedCutoffTime)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in expiredDeletedKeys)
                    {
                        _deletedResources.TryRemove(key, out _);
                        if (_resourceThreadIds.TryRemove(key, out _))
                        {
                            expiredThreadIdKeys.Add(key);
                        }
                    }

                    if (expiredCrawledKeys.Count > 0 || expiredDeletedKeys.Count > 0 || expiredThreadIdKeys.Count > 0)
                    {
                        _logger.LogDebug("Cleaned up {CrawledCount} expired crawled entries, {DeletedCount} expired deleted entries, and {ThreadIdCount} expired thread ID entries",
                            expiredCrawledKeys.Count, expiredDeletedKeys.Count, expiredThreadIdKeys.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error during cache cleanup");
                }
            }
        }
    }
}
