// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using System.Diagnostics;
using Agent.Core.Configuration;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Agent.Graph.Schema;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager.Monitor.Models;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using static Kusto.Data.Net.Http.OneApiError;

namespace Agent.Graph.Services;

public class ResourceGraphCrawlerService : ICrawlerService
{
    private readonly ILogger<ResourceGraphCrawlerService> _logger;
    private readonly ArmResourceCrawlerFactory _factory;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly CrawlerSettings _crawlerSettings;
    private readonly IActivityLogService _activityLogService;

    private bool _isCrawling = false;
    private bool _hasCompletedInitialGraphCrawl = false;
    private int _crawledCount = 0;
    private int _totalVisibleResources = 0; // Based on current visible nodes. Will not be an accurate count of total resources.

    public ResourceGraphCrawlerService(ILogger<ResourceGraphCrawlerService> logger, CrawlerSettings crawlerSettings, ArmResourceCrawlerFactory factory, IGraphDatabaseClient graphDbClient, IActivityLogService activityLogService)
    {
        _logger = logger;
        _factory = factory;
        _graphDbClient = graphDbClient;
        _crawlerSettings = crawlerSettings;
        _activityLogService = activityLogService;
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
            TotalVisibleResources = _totalVisibleResources,
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
                    if (IsArmResourceOperation(eventData))
                    {
                        GraphNode node = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(eventData.ResourceId);
                        if (node != null)
                        {
                            _logger.LogDebug($"Crawling on event: {eventData.HttpRequest.Method} {eventData.ResourceId}.");
                            var startTS = DateTime.UtcNow.Ticks;
                            var crawler = _factory.CreateFromNode(node);
                            await foreach (var _ in crawler.Crawl(node)){ }

                            _logger.LogDebug($"Cleaning up stale edges from {node.GetNodeId()} (older than {startTS})");
                            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, node, startTS);
                        }
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

            int crawledCount = 0;
            int crawlingCount = 0;
            int pendingCount = 0;

            var startTS = DateTime.UtcNow.Ticks;
            var sw = new Stopwatch();
            sw.Start();

            foreach (var node in nodes)
            {
                if (filters == null || FilterResourceType(filters, node))
                {
                    toCrawl.Enqueue(node);
                    Interlocked.Increment(ref pendingCount);
                }
            }

            var cts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken ?? CancellationToken.None);
            _ = Task.Run(async () =>
            {
                while (!linkedCts.IsCancellationRequested)
                {
                    _logger.LogInternalInformation($"Crawling progress: crawling: {crawlingCount}, pending: {pendingCount}, crawled: {crawledCount}");
                    await Task.Delay(5 * 1000);
                }
                _logger.LogInternalInformation($"Crawling progress: crawling: {crawlingCount}, pending: {pendingCount}, crawled: {crawledCount}");
            });

            while (toCrawl.Count > 0 || tasks.Count > 0)
            {
                while (toCrawl.Count > 0 && tasks.Count < _crawlerSettings.MaxParallelism)
                {
                    var node = toCrawl.Dequeue() as GraphNode;
                    Interlocked.Decrement(ref pendingCount);
                    Interlocked.Increment(ref crawlingCount);
                    if (node == null)
                    {
                        Interlocked.Decrement(ref crawlingCount);
                        Interlocked.Increment(ref crawledCount);
                        continue;
                    }

                    if (crawled.Contains(node.GetHashString()))
                    {
                        Interlocked.Decrement(ref crawlingCount);
                        Interlocked.Increment(ref crawledCount);
                        continue;
                    }
                    _totalVisibleResources = Math.Max(_totalVisibleResources, crawledCount + crawlingCount + pendingCount);
                    _crawledCount = Math.Max(_crawledCount, crawledCount);

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
                                    Interlocked.Increment(ref pendingCount);
                                }
                            }

                            _logger.LogDebug($"Cleaning up stale edges from {node.GetNodeId()} (older than {startTS})");
                            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, node, startTS);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref crawlingCount);
                            Interlocked.Increment(ref crawledCount);
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

    private bool IsArmResourceOperation(EventDataInfo eventData)
    {
        // TODO: filter operation, e.g. Microsoft.App/containerApps/listSecrets/action
        if (eventData.Category.Value == "Administrative"
            && eventData.ResourceId != null
            && eventData.HttpRequest != null
            && (eventData.HttpRequest.Method == "PUT" || eventData.HttpRequest.Method == "PATCH")
            && !string.IsNullOrEmpty(eventData.Status.Value)
            && (eventData.Status.Value == "Accepted" || eventData.Status.Value == "Succeeded"))
        {
            return true;
        }

        return false;
    }
}

