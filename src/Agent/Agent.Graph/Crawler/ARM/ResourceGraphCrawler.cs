using System.Collections;
using System.Diagnostics;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.ResourceManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class ResourceGraphCrawler
{
    private readonly ILogger<ResourceGraphCrawler> _logger;
    private readonly ArmResourceCrawlerFactory _factory;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly AzureResourceGraphClient _graphClient;
    private readonly IArmClientFactory _armClientFactory;
    private readonly CrawlerSettings _crawlerSettings;
    private readonly SemaphoreSlim _semaphore;

    public ResourceGraphCrawler(ILogger<ResourceGraphCrawler> logger, CrawlerSettings crawlerSettings, ArmResourceCrawlerFactory factory, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, IArmClientFactory armClientFactory)
    {
        _logger = logger;
        _factory = factory;
        _graphDbClient = graphDbClient;
        _graphClient = graphClient;
        _armClientFactory = armClientFactory;
        _crawlerSettings = crawlerSettings;
        _semaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<int> Crawl(string rootId, HashSet<Type> filters = null, CancellationToken? cancellationToken = null)
    {
        ArmResourceNode rootNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(rootId);
        return await Crawl(new List<ArmResourceNode>() { rootNode }, filters);
    }

    public async Task<int> Crawl(IList<ArmResourceNode> nodes, HashSet<Type> filters = null, CancellationToken? cancellationToken = null)
    {
        await _semaphore.WaitAsync(cancellationToken ?? CancellationToken.None);
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
                if (filters == null || filters.Contains(node.GetType()))
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
                    _logger.LogInformation($"Crawling progress: crawling: {crawlingCount}, pending: {pendingCount}, crawled: {crawledCount}");
                    await Task.Delay(5 * 1000);
                }
                _logger.LogInformation($"Crawling progress: crawling: {crawlingCount}, pending: {pendingCount}, crawled: {crawledCount}");
            });

            while (toCrawl.Count > 0 || tasks.Count > 0)
            {
                while (toCrawl.Count > 0 && tasks.Count < _crawlerSettings.MaxParallelism)
                {
                    var node = toCrawl.Dequeue() as ArmResourceNode;
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

                    crawled.Add(node.GetHashString());

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var armClient = _armClientFactory.GetCrawlerArmClient();
                            var crawler = _factory.CreateFromNode(node, _graphDbClient, _graphClient, armClient);
                            await foreach (var n in crawler.Crawl(node))
                            {
                                if (filters == null || filters.Contains(n.GetType()))
                                {
                                    toCrawl.Enqueue(n);
                                    Interlocked.Increment(ref pendingCount);
                                }
                            }

                            _logger.LogDebug($"Cleaning up stale edges from {node.ResourceId} (older than {startTS})");
                            await _graphDbClient.Query($"g.V('{GetSanitizedCosmosDBId(node.ResourceId)}').outE().or(__.not(has('updateTs')),__.has('updateTs', P.lt({startTS}))).drop()");
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
            _logger.LogInformation($"Done crawling. Time taken: {sw.ElapsedMilliseconds}ms. Total unique crawled resources: {crawled.Count}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling resources");
        }

        _semaphore.Release();

        return crawled.Count;
    }

    // Crawl the whole subscription and remove all nodes that does not have any in edge (no longer exists)
    public async Task CleanUp(string subscription)
    {
        ArmResourceNode subNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier($"/subscriptions/{subscription}");

        // crawl for all resources to resource group level to refresh in edges
        _logger.LogInformation($"Crawling subscription {subscription} for resources");
        await Crawl([subNode], new HashSet<Type>() { typeof(SubscriptionNode), typeof(ResourceGroupNode) });
        _logger.LogInformation($"Done crawling. Start to cleanup orphan nodes under subscription {subscription} (no inE)");

        var query = $"g.V().not(hasLabel('subscription')).has('subscriptionId', '{subscription}').where(__.inE().count().is(0))";
        var result = await _graphDbClient.Query(query, maxMessageSize: 0);
        int count = result.Count;
        while (count > 0)
        {
            _logger.LogInformation($"Will drop {count} orphan nodes");
            await _graphDbClient.Query($"{query}.drop()");
            result = await _graphDbClient.Query(query, maxMessageSize: 0);
            count = result.Count;
        }
        _logger.LogInformation($"Done cleanup orphan nodes under subscription {subscription} (no inE)");

        _logger.LogInformation($"Start to cleanup orphan nodes in graph (no edges)");
        await _graphDbClient.Query($"g.V().not(hasLabel('subscription')).where(__.bothE().count().is(0)).drop()");
        _logger.LogInformation($"Done cleanup orphan nodes in graph (no edges)");

        _logger.LogInformation($"Done cleaning up");
    }

    private static string GetSanitizedCosmosDBId(string id)
    {
        return id.Replace("/", "_").Replace(":", "_").Replace(" ", "_");
    }
}
