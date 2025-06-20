// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Agent.Graph.Schema;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager.Monitor.Models;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Services;

internal class CrawlProgressCounter(int crawledCount, int crawlingCount, int pendingCount)
{
    public int CrawledCount = crawledCount;
    public int CrawlingCount = crawlingCount;
    public int PendingCount = pendingCount;
}

public class ResourceGraphCrawlerService : ICrawlerService
{
    private readonly ILogger<ResourceGraphCrawlerService> _logger;
    private readonly ArmResourceCrawlerFactory _factory;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly CrawlerSettings _crawlerSettings;
    private readonly IWatchEventService _activityLogService;
    private readonly IWatchEventService _kubernetesWatchService;
    private readonly ICrawlerTriggerService _crawlerTriggerService;

    private bool _isCrawling = false;
    private bool _hasCompletedInitialGraphCrawl = false;
    private int _crawledCount = 0;
    private int _crawlingCount = 0;
    private int _pendingCount = 0;
    private readonly ConcurrentDictionary<string, CrawlProgressCounter> _progressByResourceType = new();
    private static readonly TimeSpan SoftDeletedNodesStaleThreshold = TimeSpan.FromDays(3); // Threshold for soft-deleted nodes cleanup

    public ResourceGraphCrawlerService(ILogger<ResourceGraphCrawlerService> logger,
        CrawlerSettings crawlerSettings,
        ArmResourceCrawlerFactory factory,
        IGraphDatabaseClient graphDbClient,
        [FromKeyedServices("ActivityLog")] IWatchEventService activityLogService,
        [FromKeyedServices("Kubernetes")] IWatchEventService kubernetesWatchService,
        ICrawlerTriggerService crawlerTriggerService,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _factory = factory;
        _graphDbClient = graphDbClient;
        _crawlerSettings = crawlerSettings;
        _activityLogService = activityLogService;
        _kubernetesWatchService = kubernetesWatchService;
        _crawlerTriggerService = crawlerTriggerService;
        _httpClientFactory = httpClientFactory;

        // Start background task to process triggered crawls
        _ = Task.Run(ProcessTriggeredCrawls);
    }

    public async Task CrawlAsync(IEnumerable<string> rootIds, IEnumerable<string>? typeFilters = null, bool cascade = true, CancellationToken? cancellationToken = null)
    {
        _logger.LogInternalInformation($"Crawl roots: {string.Join(",", rootIds)}. Cascade = {cascade}.");
        List<GraphNode> roots = new List<GraphNode>();
        foreach (var rootId in rootIds)
        {
            GraphNode rootNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(rootId);
            if (rootNode != null)
            {
                roots.Add(rootNode);
            }
        }

        HashSet<string> scopeFiltersSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sorted = rootIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
        var last = string.Empty;
        foreach (var id in sorted)
        {
            if (!string.IsNullOrEmpty(last) && id.StartsWith(last, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            scopeFiltersSet.Add(id);
            last = id;
        }

        HashSet<string> typeFiltersSet = null;
        if (typeFilters != null)
        {
            typeFiltersSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filter in typeFilters)
            {
                typeFiltersSet.Add(filter);
            }
        }
        await Crawl(roots, scopeFiltersSet, typeFiltersSet, cascade, cancellationToken);
    }

    public Task<CrawlerResult> GetCrawlerResult()
    {
        return Task.FromResult(new CrawlerResult
        {
            IsCrawling = _isCrawling,
            HasCompletedInitialGraphCrawl = _hasCompletedInitialGraphCrawl,
            CrawledCount = _crawledCount,
            TotalVisibleResources = _crawledCount + _crawlingCount + _pendingCount,
            ProgressByResourceType = _progressByResourceType
                .Select(kvp => new KeyValuePair<string, CrawlProgress>(kvp.Key, new CrawlProgress(CrawledCount: kvp.Value.CrawledCount, TotalResources: kvp.Value.CrawledCount + kvp.Value.CrawlingCount + kvp.Value.PendingCount)))
                .ToDictionary(),
        });
    }

    public void StartActivityLogCrawler(IEnumerable<string> resourceIds, CancellationToken? cancellationToken = null)
    {
        _logger.LogInternalInformation($"Start activity log crawler for resources: {string.Join(",", resourceIds)}");

        var sources = GetArmWatchEventSources(resourceIds);

        _ = Task.Run(async () =>
        {
            await foreach (var watchEvent in _activityLogService.WatchEvents(sources, cancellationToken))
            {
                var eventData = watchEvent.EventData as EventDataInfo;
                if (eventData == null)
                {
                    _logger.LogInternalWarning($"Received unknown event data when watching activity log: {watchEvent.EventData}");
                    continue;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var armOperationType = GetArmResourceOperationType(eventData);
                        if (armOperationType == ArmResourceOperationType.Other)
                        {
                            // Log with safe null handling for HttpRequest.Method and ResourceId
                            _logger.LogInternalInformation($"Ignoring event: {eventData.HttpRequest?.Method ?? "unknown method"} {eventData.ResourceId ?? "unknown resource"}.");
                            return;
                        }
                        else if (armOperationType == ArmResourceOperationType.Delete)
                        {
                            _logger.LogInternalInformation($"Deleting resource: {eventData.HttpRequest?.Method ?? "unknown method"} {eventData.ResourceId ?? "unknown resource"}.");
                            if (!string.IsNullOrEmpty(eventData.ResourceId))
                            {
                                await _graphDbClient.SoftDeleteResourceById(eventData.ResourceId);
                            }
                            return;
                        }
                        else if (armOperationType == ArmResourceOperationType.Update)
                        {
                            if (string.IsNullOrEmpty(eventData.ResourceId))
                            {
                                _logger.LogInternalWarning("Received update event with null or empty ResourceId");
                                return;
                            }

                            GraphNode node = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(eventData.ResourceId);
                            if (node != null)
                            {
                                _logger.LogInternalInformation($"Crawling on event: {eventData.HttpRequest?.Method ?? "unknown method"} {eventData.ResourceId}.");
                                await OnDemandCrawl(node);
                            }
                        }
                        else
                        {
                            _logger.LogInternalWarning($"Unknown arm operation: {armOperationType} {eventData.HttpRequest?.Method ?? "unknown method"} {eventData.ResourceId ?? "unknown resource"}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, $"Error processing activity log event: {eventData.HttpRequest.Method} {eventData.ResourceId}.");
                    }
                }, cancellationToken ?? CancellationToken.None);
            }
        }, cancellationToken ?? CancellationToken.None);
    }

    public async Task StartKubernetesWatchCrawler(IEnumerable<string> resourceIds, CancellationToken? cancellationToken = null)
    {
        _logger.LogInternalInformation($"Start Kubernetes watch crawler for resources: {string.Join(",", resourceIds)}");

        var sources = await GetKubernetesWatchEventSources(resourceIds);

        _ = Task.Run(async () =>
        {
            await foreach (var watchEvent in _kubernetesWatchService.WatchEvents(sources, cancellationToken))
            {
                var eventData = watchEvent.EventData as KubernetesEventData;
                if (eventData == null)
                {
                    _logger.LogInternalWarning($"Received unknown event data when watching Kubernetes: {watchEvent.EventData}");
                    continue;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        GraphNode node = ArmResourceCrawlerFactory.CreateKubernetesResourceNode(
                            k8sObject: eventData.K8sObject,
                            subscriptionId: eventData.SubscriptionId,
                            resourceGroupName: eventData.ResourceGroupName,
                            location: null,
                            clusterResourceId: eventData.ClusterResourceId,
                            namespaceName: eventData.Namespace,
                            resourceName: eventData.ResourceName,
                            group: eventData.Group,
                            apiVersion: eventData.ApiVersion,
                            kind: eventData.Kind
                        );

                        if (node != null)
                        {
                            _logger.LogInternalInformation($"Crawling on Kubernetes event: {eventData.ClusterResourceId} {eventData.Namespace} {eventData.Group}" +
                                                $" {eventData.ApiVersion} {eventData.Kind} {eventData.ResourceName}.");
                            await OnDemandCrawl(node);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, $"Error processing Kubernetes event: {eventData.ClusterResourceId} {eventData.Namespace} {eventData.Group}" +
                                                $" {eventData.ApiVersion} {eventData.Kind} {eventData.ResourceName}.");
                    }
                }, cancellationToken ?? CancellationToken.None);
            }
        }, cancellationToken ?? CancellationToken.None);
    }

    private async Task Crawl(IList<GraphNode> nodes, HashSet<string> scopeFilters, HashSet<string> typeFilters = null, bool cascade = true, CancellationToken? cancellationToken = null)
    {
        _logger.LogInternalInformation($"Crawling resources: {string.Join(", ", nodes.Select(n => n.GetNodeId()))}. Cascade = {cascade}. Scope Filters = {string.Join(", ", scopeFilters ?? Enumerable.Empty<string>())}. Type Filters = {string.Join(", ", typeFilters ?? Enumerable.Empty<string>())}");
        _isCrawling = true;
        HashSet<string> crawled = new();
        try
        {
            Queue queue = new();
            Queue toCrawl = Queue.Synchronized(queue);
            IList<Task> tasks = new List<Task>();

            _crawledCount = 0;
            _crawlingCount = 0;
            _pendingCount = 0;

            _progressByResourceType.Clear();

            var startTS = DateTime.UtcNow.Ticks;
            var sw = new Stopwatch();
            sw.Start();

            foreach (var node in nodes)
            {
                if (typeFilters == null || FilterResourceType(typeFilters, node))
                {
                    toCrawl.Enqueue(node);
                    Interlocked.Increment(ref _pendingCount);

                    _progressByResourceType.AddOrUpdate(node.GetNodeLabel(),
                        (resouceType) => new CrawlProgressCounter(0, 0, 1),
                        (resourceType, progress) =>
                        {
                            Interlocked.Increment(ref progress.PendingCount);
                            return progress;
                        });
                }
            }

            var cts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken ?? CancellationToken.None);
            _ = Task.Run(async () =>
            {
                while (!linkedCts.IsCancellationRequested)
                {
                    _logger.LogInternalInformation($"Crawling progress: crawling: {_crawlingCount}, pending: {_pendingCount}, crawled: {_crawledCount}");
                    await Task.Delay(5 * 1000);
                }
                _logger.LogInternalInformation($"Crawling progress: crawling: {_crawlingCount}, pending: {_pendingCount}, crawled: {_crawledCount}");
            });

            while (toCrawl.Count > 0 || tasks.Count > 0)
            {
                while (toCrawl.Count > 0 && tasks.Count < _crawlerSettings.MaxParallelism)
                {
                    var node = toCrawl.Dequeue() as GraphNode;
                    var resourceType = node is null ? string.Empty : node.GetNodeLabel();
                    var progressByResourceType = _progressByResourceType.GetOrAdd(resourceType,
                        (resourceType) => new CrawlProgressCounter(0, 0, 0));
                    Interlocked.Decrement(ref _pendingCount);
                    Interlocked.Decrement(ref progressByResourceType.PendingCount);
                    Interlocked.Increment(ref _crawlingCount);
                    Interlocked.Increment(ref progressByResourceType.CrawlingCount);
                    if (node == null)
                    {
                        Interlocked.Decrement(ref _crawlingCount);
                        Interlocked.Decrement(ref progressByResourceType.CrawlingCount);
                        Interlocked.Increment(ref _crawledCount);
                        Interlocked.Increment(ref progressByResourceType.CrawledCount);
                        continue;
                    }


                    if (crawled.Contains(node.GetHashString()))
                    {
                        Interlocked.Decrement(ref _crawlingCount);
                        Interlocked.Decrement(ref progressByResourceType.CrawlingCount);
                        Interlocked.Increment(ref _crawledCount);
                        Interlocked.Increment(ref progressByResourceType.CrawledCount);
                        continue;
                    }

                    crawled.Add(node.GetHashString());

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var crawler = _factory.CreateFromNode(node);
                            await foreach (var n in crawler.Crawl(node))
                            {
                                if (cascade
                                    && FilterResourceType(typeFilters, n)
                                    && FilterResourceScope(scopeFilters, n))
                                {
                                    toCrawl.Enqueue(n);
                                    Interlocked.Increment(ref _pendingCount);
                                    var progressByType = _progressByResourceType.AddOrUpdate(n.GetNodeLabel(),
                                        (resourceType) => new CrawlProgressCounter(0, 0, 1),
                                        (resourceType, progress) =>
                                        {
                                            Interlocked.Increment(ref progress.PendingCount);
                                            return progress;
                                        });
                                }
                            }

                            _logger.LogInternalInformation($"Cleaning up stale edges from {node.GetNodeId()} (older than {startTS})");
                            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, node, startTS);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalError(ex, $"Error crawling {node.GetNodeId()}");
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _crawlingCount);
                            Interlocked.Decrement(ref progressByResourceType.CrawlingCount);
                            Interlocked.Increment(ref _crawledCount);
                            Interlocked.Increment(ref progressByResourceType.CrawledCount);
                        }
                    }));
                }

                if (tasks.Count == 0)
                {
                    continue;
                }

                await Task.WhenAny(tasks);
                var newTasks = new List<Task>();
                foreach (var task in tasks)
                {
                    if (!task.IsCompleted)
                    {
                        newTasks.Add(task);
                    }
                }
                tasks = newTasks;
            }

            sw.Stop();
            cts.Cancel();
            _isCrawling = false;
            _hasCompletedInitialGraphCrawl = true;
            _logger.LogInternalInformation($"Done crawling. Time taken: {sw.ElapsedMilliseconds}ms. Total unique crawled resources: {crawled.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error crawling resources");
        }
    }

    // This method is called from event triggered crawls.
    // The reasoning Crawl() is not used is the counter logic in Crawl() assumes single thread.
    private async Task OnDemandCrawl(GraphNode node)
    {
        try
        {
            var startTS = DateTime.UtcNow.Ticks;
            var crawler = _factory.CreateFromNode(node);
            await foreach (var _ in crawler.Crawl(node)) { }

            _logger.LogInternalInformation($"Cleaning up stale edges from {node.GetNodeId()} (older than {startTS})");
            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, node, startTS);

            _logger.LogInternalInformation($"Completed triggered crawl for resource: {node.GetNodeId()}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error during triggered crawl for resource: {node.GetNodeId()}");
        }

    }

    // Need to rethink how to do the cleanup
    // It's not easy for arm resources because a resource can reference another resource in another subscription

    // Crawl the whole subscription and remove all nodes that does not have any in edge (no longer exists)
    //public async Task CleanUp(string subscription)
    //{
    //    ArmResourceNode subNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier($"/subscriptions/{subscription}");

    //    // crawl for all resources to resource group level to refresh in edges
    //    _logger.LogInternalInformation($"Crawling subscription {subscription} for resources");
    //    await Crawl([subNode], new HashSet<string>() { "subscriptions", "resourcegroups" });
    //    _logger.LogInternalInformation($"Done crawling. Start to cleanup orphan nodes under subscription {subscription} (no inE)");

    //    var query = $"g.V().not(hasLabel('subscription')).has('subscriptionId', '{subscription}').where(__.inE().count().is(0))";
    //    var result = await _graphDbClient.Query(query, maxMessageSize: 0);
    //    int count = result.Count;
    //    while (count > 0)
    //    {
    //        _logger.LogInternalInformation($"Will drop {count} orphan nodes");
    //        await _graphDbClient.Query($"{query}.drop()");
    //        result = await _graphDbClient.Query(query, maxMessageSize: 0);
    //        count = result.Count;
    //    }
    //    _logger.LogInternalInformation($"Done cleanup orphan nodes under subscription {subscription} (no inE)");

    //    _logger.LogInternalInformation($"Start to cleanup orphan nodes in graph (no edges)");
    //    await _graphDbClient.Query($"g.V().not(hasLabel('subscription')).where(__.bothE().count().is(0)).drop()");
    //    _logger.LogInternalInformation($"Done cleanup orphan nodes in graph (no edges)");

    //    _logger.LogInternalInformation($"Done cleaning up");
    //}

    private (string, string, ResourceIdentifier?) ParseResourceId(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return (null, null, null);
        }

        ResourceIdentifier id = ResourceIdentifier.Parse(resourceId);
        if (!string.IsNullOrEmpty(id.SubscriptionId) && string.IsNullOrEmpty(id.ResourceGroupName))
        {
            return (id.SubscriptionId, null, null);
        }
        else if (!string.IsNullOrEmpty(id.SubscriptionId) && !string.IsNullOrEmpty(id.ResourceGroupName) && string.Equals(id.ResourceType.Type, "resourcegroups", StringComparison.OrdinalIgnoreCase))
        {
            return (id.SubscriptionId, id.ResourceGroupName, null);
        }
        else
        {
            return (id.SubscriptionId, id.ResourceGroupName, id);
        }
    }

    private List<WatchEventSource> GetArmWatchEventSources(IEnumerable<string> resourceIds)
    {
        List<WatchEventSource> sources = new List<WatchEventSource>();
        foreach (var resourceId in resourceIds)
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                continue;
            }

            var (subscriptionId, resourceGroupName, resource) = ParseResourceId(resourceId);

            sources.Add(new WatchEventSource
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                ResourceId = resource
            });
        }

        return sources;
    }

    private async Task<List<WatchEventSource>> GetKubernetesWatchEventSources(IEnumerable<string> resourceIds)
    {
        var aksClusters = new HashSet<string>();
        foreach (var resourceId in resourceIds)
        {
            var (subscriptionId, resourceGroupName, id) = ParseResourceId(resourceId);
            if (id != null)
            {
                aksClusters.Add(id);
                continue;
            }

            var query = $"g.V().has('resourceType', '{Constants.ManagedClusterType.ToLowerInvariant()}').has('subscriptionId', '{subscriptionId}')";
            if (!string.IsNullOrEmpty(resourceGroupName))
            {
                query += $".has('resourceGroupName', '{resourceGroupName}')";
            }
            query += $".values('resourceId')";

            var results = await _graphDbClient.Query<string>(query);
            if (results != null && results.Count > 0)
            {
                foreach (var result in results)
                {
                    aksClusters.Add(result);
                }
            }
        }

        var sources = new List<WatchEventSource>();
        foreach (var aksCluster in aksClusters)
        {
            if (string.IsNullOrEmpty(aksCluster))
            {
                continue;
            }

            var (subscriptionId, resourceGroupName, resource) = ParseResourceId(aksCluster);
            sources.Add(new WatchEventSource
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                ResourceId = resource
            });
        }

        return sources;
    }

    private bool FilterResourceType(HashSet<string> filters, GraphNode node)
    {
        if (filters == null)
        {
            return true;
        }

        if (node is ArmResourceNode armNode)
        {
            if (string.Equals(armNode.ResourceType, SubscriptionNode.Type)
                || string.Equals(armNode.ResourceType, ResourceGroupNode.Type))
            {
                return true;
            }

            if (filters.Contains(armNode.ResourceType))
            {
                return true;
            }
        }
        else if (node is KubernetesResourceNode k8sNode)
        {
            if (filters.Contains(k8sNode.Kind))
            {
                return true;
            }
        }

        return false;
    }

    private bool FilterResourceScope(HashSet<string> filters, GraphNode node)
    {
        foreach (var filter in filters)
        {
            if (node.GetNodeId().StartsWith(filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private enum ArmResourceOperationType
    {
        Update,
        Delete,
        Other
    }

    private ArmResourceOperationType GetArmResourceOperationType(EventDataInfo eventData)
    {
        // Add null checks for each property that could be null
        if (eventData == null || 
            eventData.Category?.Value == null || 
            eventData.ResourceId == null || 
            eventData.HttpRequest == null || 
            eventData.HttpRequest.Method == null || 
            string.IsNullOrEmpty(eventData.Status?.Value))
        {
            _logger.LogInternalWarning($"Incomplete event data received: {(eventData?.ResourceId ?? "unknown resource")}");
            return ArmResourceOperationType.Other;
        }

        // TODO: filter operation, e.g. Microsoft.App/containerApps/listSecrets/action
        if (eventData.Category.Value == "Administrative"
            && eventData.ResourceId != null
            && eventData.HttpRequest != null
            && !string.IsNullOrEmpty(eventData.Status.Value)
            && (eventData.Status.Value == "Accepted" || eventData.Status.Value == "Succeeded"))
        {
            if (eventData.HttpRequest.Method == "PUT" || eventData.HttpRequest.Method == "PATCH")
            {
                return ArmResourceOperationType.Update;
            }
            else if (eventData.HttpRequest.Method == "DELETE")
            {
                return ArmResourceOperationType.Delete;
            }
        }

        return ArmResourceOperationType.Other;
    }

    private async Task ProcessTriggeredCrawls()
    {
        try
        {
            await foreach (var item in _crawlerTriggerService.GetResourceIdsToProcess())
            {
                _ = Task.Run(async () =>
                {
                    switch (item)
                    {
                        case ArmResourceTriggerItem armItem:
                            {
                                var resourceId = armItem.GetResourceId();
                                try
                                {
                                    _logger.LogInternalInformation($"Processing triggered crawl for resource: {resourceId}");

                                    GraphNode node = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(resourceId);
                                    if (node != null)
                                    {
                                        // Check if the resource still exists before crawling
                                        if (await IsResourceDeleted(resourceId))
                                        {
                                            _logger.LogInternalInformation($"Resource {resourceId} has been deleted, removing from graph");
                                            await RemoveDeletedResourceFromGraph(node);
                                            _crawlerTriggerService.MarkResourceAsDeleted(armItem);
                                            return;
                                        }

                                        await OnDemandCrawl(node);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // If it's a rate limiting error, requeue for retry
                                    if (IsRateLimitingError(ex))
                                    {
                                        _logger.LogInternalInformation($"Rate limiting encountered for resource {resourceId}, requeuing for retry: {ex.Message}");
                                        _crawlerTriggerService.TriggerArmCrawl(resourceId, force: true);
                                        // sleep 1s before retrying to avoid immediate re-trigger
                                        await Task.Delay(1000);
                                    }
                                    // If it's a 404 error, the resource might be deleted
                                    else if (IsResourceNotFoundError(ex))
                                    {
                                        _logger.LogInternalInformation($"Resource {resourceId} appears to be deleted based on error: {ex.Message}");
                                        var node = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(resourceId);
                                        if (node != null)
                                        {
                                            await RemoveDeletedResourceFromGraph(node);
                                            _crawlerTriggerService.MarkResourceAsDeleted(armItem);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogInternalError(ex, $"Error in triggered crawl for resource: {resourceId}");
                                    }
                                }
                                break;
                            }
                        case KubernetesResourceTriggerItem k8sItem:
                            {
                                var resourceId = k8sItem.GetResourceId();
                                _logger.LogInternalInformation($"Processing triggered crawl for Kubernetes resource: {k8sItem.GetResourceId()}");
                                var node = ArmResourceCrawlerFactory.CreateKubernetesResourceNode(
                                    k8sObject: null,
                                    subscriptionId: null,
                                    resourceGroupName: null,
                                    location: null,
                                    clusterResourceId: k8sItem.ClusterResourceId,
                                    namespaceName: k8sItem.Namespace,
                                    resourceName: k8sItem.ResourceName,
                                    group: k8sItem.Group,
                                    apiVersion: k8sItem.ApiVersion,
                                    kind: k8sItem.Kind
                                );

                                if (k8sItem.IsDelete)
                                {
                                    _logger.LogInternalInformation($"Resource {resourceId} has been deleted, removing from graph");
                                    await RemoveDeletedResourceFromGraph(node);
                                    _crawlerTriggerService.MarkResourceAsDeleted(k8sItem);
                                    return;
                                }

                                await OnDemandCrawl(node);
                                break;
                            }
                        default:
                            throw new NotSupportedException($"Unsupported trigger item type: {item.GetType().Name}");
                    }

                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in ProcessTriggeredCrawls background task");
        }
    }

    private async Task<bool> IsResourceDeleted(string resourceId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient(Core.Constants.HttpClientForCrawler);
            var requestUrl = $"https://management.azure.com{resourceId}?api-version=2021-04-01";
            var request = new HttpRequestMessage(HttpMethod.Head, requestUrl);

            var response = await httpClient.SendAsync(request);
            return response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, $"Failed to check if resource {resourceId} is deleted, assuming it exists");
            return false;
        }
    }

    private bool IsRateLimitingError(Exception ex)
    {
        return ex.Message.Contains("RequestRateTooLargeException") ||
               ex.Message.Contains("TooManyRequests") ||
               ex.Message.Contains("Request rate is large") ||
               (ex.InnerException != null && IsRateLimitingError(ex.InnerException));
    }

    private bool IsResourceNotFoundError(Exception ex)
    {
        return ex.Message.Contains("404") ||
               ex.Message.Contains("NotFound") ||
               ex.Message.Contains("ResourceNotFound") ||
               (ex.InnerException != null && IsResourceNotFoundError(ex.InnerException));
    }

    private async Task RemoveDeletedResourceFromGraph(GraphNode node)
    {
        try
        {
            _logger.LogDebug($"Removing deleted resource {node.GetNodeId()} from graph");

            // Remove the node and all its edges from the graph
            var query = $"g.V().has('id', '{node.GetNodeId()}').drop()";
            await _graphDbClient.Query(query);

            _logger.LogDebug($"Successfully removed resource {node.GetNodeId()} from graph");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to remove deleted resource {node.GetNodeId()} from graph");
        }
    }

    public async Task DeleteStaleSoftDeletedNodes(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation("Cleaning up stale nodes in the graph");
            var ts = (DateTimeOffset.UtcNow - SoftDeletedNodesStaleThreshold).Ticks;

            // Remove nodes that are soft-deleted and older than the threshold
            var query = $"g.V().has('isDeleted', true).has('updateTs', lt({ts})).drop()";
            await _graphDbClient.Query(query);
            _logger.LogInternalInformation($"Removed stale soft-deleted nodes older than {ts} from the graph");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during cleanup of stale nodes");
        }
    }
}
