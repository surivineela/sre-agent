// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using Agent.Core.Configuration;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Agent.Graph.Schema;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager.Monitor.Models;
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
    private readonly IActivityLogService _activityLogService;
    private readonly ICrawlerTriggerService _crawlerTriggerService;

    private bool _isCrawling = false;
    private bool _hasCompletedInitialGraphCrawl = false;
    private int _crawledCount = 0;
    private int _crawlingCount = 0;
    private int _pendingCount = 0;
    private readonly ConcurrentDictionary<string, CrawlProgressCounter> _progressByResourceType = new();

    public ResourceGraphCrawlerService(ILogger<ResourceGraphCrawlerService> logger, CrawlerSettings crawlerSettings, ArmResourceCrawlerFactory factory, IGraphDatabaseClient graphDbClient, IActivityLogService activityLogService, ICrawlerTriggerService crawlerTriggerService, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _factory = factory;
        _graphDbClient = graphDbClient;
        _crawlerSettings = crawlerSettings;
        _activityLogService = activityLogService;
        _crawlerTriggerService = crawlerTriggerService;
        _httpClientFactory = httpClientFactory;

        // Start background task to process triggered crawls
        _ = Task.Run(ProcessTriggeredCrawls);
    }

    public async Task CrawlAsync(IEnumerable<string> rootIds, IEnumerable<string>? filters = null, bool cascade = true, CancellationToken? cancellationToken = null)
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

        HashSet<string> filtersSet = null;
        if (filters != null)
        {
            filtersSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var filter in filters)
            {
                filtersSet.Add(filter);
            }
        }
        await Crawl(roots, filtersSet, cascade, cancellationToken);
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

        List<WatchEventSource> sources = new List<WatchEventSource>();
        foreach (var resourceId in resourceIds)
        {
            if (string.IsNullOrEmpty(resourceId))
            {
                continue;
            }

            ResourceIdentifier id = ResourceIdentifier.Parse(resourceId);

            if (!string.IsNullOrEmpty(id.SubscriptionId) && string.IsNullOrEmpty(id.ResourceGroupName))
            {
                sources.Add(new WatchEventSource
                {
                    SubscriptionId = id.SubscriptionId,
                    ResourceGroupName = null,
                    ResourceId = null
                });
            }
            else if (!string.IsNullOrEmpty(id.SubscriptionId) && !string.IsNullOrEmpty(id.ResourceGroupName) && string.Equals(id.ResourceType.Type, "resourcegroups", StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(new WatchEventSource
                {
                    SubscriptionId = id.SubscriptionId,
                    ResourceGroupName = id.ResourceGroupName,
                    ResourceId = null
                });
            }
            else
            {
                sources.Add(new WatchEventSource
                {
                    SubscriptionId = id.SubscriptionId,
                    ResourceGroupName = id.ResourceGroupName,
                    ResourceId = id
                });
            }
        }

        _ = Task.Run(async () =>
        {
            await foreach (var eventData in _activityLogService.WatchEvents(sources, cancellationToken))
            {
                _ = Task.Run(async () =>
                {
                    var armOperationType = GetArmResourceOperationType(eventData);
                    if (armOperationType == ArmResourceOperationType.Other)
                    {
                        _logger.LogDebug($"Ignoring event: {eventData.HttpRequest.Method} {eventData.ResourceId}.");
                        return;
                    }
                    else if (armOperationType == ArmResourceOperationType.Delete)
                    {
                        _logger.LogDebug($"Deleting resource: {eventData.HttpRequest.Method} {eventData.ResourceId}.");
                        if (!string.IsNullOrEmpty(eventData.ResourceId))
                        {
                            await _graphDbClient.SoftDeleteResourceById(eventData.ResourceId);
                        }
                        return;
                    }
                    else if (armOperationType == ArmResourceOperationType.Update)
                    {
                        GraphNode node = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(eventData.ResourceId);
                        if (node != null)
                        {
                            _logger.LogDebug($"Crawling on event: {eventData.HttpRequest.Method} {eventData.ResourceId}.");
                            var startTS = DateTime.UtcNow.Ticks;
                            var crawler = _factory.CreateFromNode(node);
                            await foreach (var _ in crawler.Crawl(node)) { }

                            _logger.LogDebug($"Cleaning up stale edges from {node.GetNodeId()} (older than {startTS})");
                            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, node, startTS);
                        }
                    }
                    else
                    {
                        _logger.LogInternalWarning($"Unknown arm operation: {armOperationType} {eventData.HttpRequest.Method} {eventData.ResourceId}.");
                    }
                }, cancellationToken ?? CancellationToken.None);
            }
        }, cancellationToken ?? CancellationToken.None);
    }

    private async Task Crawl(IList<GraphNode> nodes, HashSet<string> filters = null, bool cascade = true, CancellationToken? cancellationToken = null)
    {
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
                if (filters == null || FilterResourceType(filters, node))
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
                                if (cascade && (filters == null || FilterResourceType(filters, n)))
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

                            _logger.LogDebug($"Cleaning up stale edges from {node.GetNodeId()} (older than {startTS})");
                            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, node, startTS);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, $"Error crawling {node.GetNodeId()}");
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

    private enum ArmResourceOperationType
    {
        Update,
        Delete,
        Other
    }

    private ArmResourceOperationType GetArmResourceOperationType(EventDataInfo eventData)
    {
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
            await foreach (var resourceId in _crawlerTriggerService.GetResourceIdsToProcess())
            {
                _ = Task.Run(async () =>
                {
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
                                _crawlerTriggerService.MarkResourceAsDeleted(resourceId);
                                return;
                            }

                            var startTS = DateTime.UtcNow.Ticks;
                            var crawler = _factory.CreateFromNode(node);
                            await foreach (var _ in crawler.Crawl(node)) { }

                            _logger.LogInternalInformation($"Cleaning up stale edges from {node.GetNodeId()} (older than {startTS})");
                            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, node, startTS);

                            _logger.LogInternalInformation($"Completed triggered crawl for resource: {resourceId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // If it's a rate limiting error, requeue for retry
                        if (IsRateLimitingError(ex))
                        {
                            _logger.LogInternalInformation($"Rate limiting encountered for resource {resourceId}, requeuing for retry: {ex.Message}");
                            _crawlerTriggerService.TriggerCrawl(resourceId, force: true);
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
                                _crawlerTriggerService.MarkResourceAsDeleted(resourceId);
                            }
                        }
                        else
                        {
                            _logger.LogInternalError(ex, $"Error in triggered crawl for resource: {resourceId}");
                        }
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

            _logger.LogDebug($"Successfully removed deleted resource {node.GetNodeId()} from graph");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to remove deleted resource {node.GetNodeId()} from graph");
        }
    }
}

