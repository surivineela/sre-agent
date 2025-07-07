// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services
{
    public abstract class TriggerItem
    {
        public abstract string GetResourceId();
    }

    public class ArmResourceTriggerItem : TriggerItem
    {
        public string ResourceId { get; }

        public ArmResourceTriggerItem(string resourceId)
        {
            ResourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
        }

        public override string GetResourceId()
        {
            return ResourceId;
        }

        public override int GetHashCode()
        {
            return GetResourceId().GetHashCode(StringComparison.InvariantCultureIgnoreCase);
        }
    }

    public class KubernetesResourceTriggerItem : TriggerItem
    {
        public string ClusterResourceId { get; }
        public string? Namespace { get; }
        public string ResourceName { get; }
        public string Group { get; }
        public string ApiVersion { get; }
        public string Kind { get; }
        public bool IsDelete { get; set; } = false;

        public KubernetesResourceTriggerItem(string clusterResourceId,
            string? @namespace,
            string resourceName,
            string group,
            string version,
            string kind,
            bool isDelete = false)
        {
            ClusterResourceId = clusterResourceId ?? throw new ArgumentNullException(nameof(clusterResourceId));
            Namespace = @namespace;
            ResourceName = resourceName;
            Group = group;
            ApiVersion = version;
            Kind = kind;
            IsDelete = isDelete;
        }

        public override string GetResourceId()
        {
            return $"{ClusterResourceId}/{Namespace}/{Group}/{ApiVersion}/{Kind}/{ResourceName}";
        }

        public override int GetHashCode()
        {
            return GetResourceId().GetHashCode(StringComparison.InvariantCultureIgnoreCase);
        }
    }


    public class CrawlerTriggerService : ICrawlerTriggerService
    {
        private readonly ConcurrentQueue<TriggerItem> _resourceIdQueue = new();
        private readonly ILogger<CrawlerTriggerService> _logger;
        private readonly ConcurrentDictionary<TriggerItem, DateTime> _recentlyCrawled = new();
        private readonly ConcurrentDictionary<TriggerItem, DateTime> _deletedResources = new();
        private readonly HashSet<TriggerItem> _pendingResourceIds = new();
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
        public void TriggerArmCrawl(string resourceId)
        {
            TriggerArmCrawl(resourceId, false);
        }

        public void TriggerArmCrawl(string resourceId, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return;

            // Extract ARM resource IDs from the input string
            var matches = ArmResourceIdRegex.Matches(resourceId);
            foreach (Match match in matches)
            {
                var extractedResourceId = match.Value.Trim().TrimEnd('"', ',');
                var item = new ArmResourceTriggerItem(extractedResourceId);

                // Skip cache checks only if force is false
                if (!force)
                {
                    // Check if this resource was recently marked as deleted
                    if (IsRecentlyDeleted(item))
                    {
                        _logger.LogDebug("Skipping recently deleted resource: {ResourceId}", item.GetResourceId());
                        continue;
                    }

                    // Check if this resource was recently crawled
                    if (IsRecentlyCrawled(item))
                    {
                        _logger.LogDebug("Skipping recently crawled resource: {ResourceId}", item.GetResourceId());
                        continue;
                    }
                }

                // Check if this resource is already pending
                lock (_pendingLock)
                {
                    if (_pendingResourceIds.Contains(item))
                    {
                        _logger.LogDebug("Resource already pending for crawl: {ResourceId}", item.GetResourceId());
                        continue;
                    }

                    _pendingResourceIds.Add(item);
                }

                _resourceIdQueue.Enqueue(item);
                _logger.LogInternalInformation("{Action} queued resource ID for crawling: {ResourceId}",
                    force ? "Force" : "", item.GetResourceId());
            }
        }

        public void TriggerArmCrawl(IEnumerable<string> resourceIds)
        {
            foreach (var resourceId in resourceIds)
            {
                TriggerArmCrawl(resourceId);
            }
        }

        public void MarkResourceAsDeleted(TriggerItem item)
        {
            if (item == null)
                return;

            _deletedResources[item] = DateTime.UtcNow;
            _logger.LogDebug("Marked resource as deleted: {ResourceId}", item.GetResourceId());
        }

        public async IAsyncEnumerable<TriggerItem> GetResourceIdsToProcess([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
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

                await Task.Delay(1000, cancellationToken);
            }
        }

        private bool IsRecentlyCrawled(TriggerItem item)
        {
            if (_recentlyCrawled.TryGetValue(item, out var lastCrawlTime))
            {
                return DateTime.UtcNow - lastCrawlTime < RecentCrawlThreshold;
            }
            return false;
        }

        private bool IsRecentlyDeleted(TriggerItem item)
        {
            if (_deletedResources.TryGetValue(item, out var deletedTime))
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
                    }

                    // Clean up deleted resources cache
                    var expiredDeletedKeys = _deletedResources
                        .Where(kvp => kvp.Value < deletedCutoffTime)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in expiredDeletedKeys)
                    {
                        _deletedResources.TryRemove(key, out _);
                    }

                    if (expiredCrawledKeys.Count > 0 || expiredDeletedKeys.Count > 0 || expiredThreadIdKeys.Count > 0)
                    {
                        _logger.LogDebug("Cleaned up {CrawledCount} expired crawled entries, {DeletedCount} expired deleted entries",
                            expiredCrawledKeys.Count, expiredDeletedKeys.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error during cache cleanup");
                }
            }
        }

        public void TriggerKubernetesCrawl(string clusterResourceId, string? namespaceName, string resourceName, string group, string apiVersion, string kind, bool isDelete = false)
        {
            var item = new KubernetesResourceTriggerItem(clusterResourceId, namespaceName, resourceName, group, apiVersion, kind, isDelete);

            // Check if this resource was recently marked as deleted
            if (IsRecentlyDeleted(item))
            {
                _logger.LogDebug("Skipping recently deleted resource: {ResourceId}", item.GetResourceId());
                return;
            }

            // Check if this resource was recently crawled
            if (IsRecentlyCrawled(item))
            {
                _logger.LogDebug("Skipping recently crawled resource: {ResourceId}", item.GetResourceId());
                return;
            }

            // Check if this resource is already pending
            lock (_pendingLock)
            {
                if (_pendingResourceIds.Contains(item))
                {
                    _logger.LogDebug("Resource already pending for crawl: {ResourceId}", item.GetResourceId());
                    return;
                }

                _pendingResourceIds.Add(item);
            }

            _resourceIdQueue.Enqueue(item);
            _logger.LogInternalInformation("Queued kubernetes resource for crawling: {ResourceId}", item.GetResourceId());
        }
    }
}
