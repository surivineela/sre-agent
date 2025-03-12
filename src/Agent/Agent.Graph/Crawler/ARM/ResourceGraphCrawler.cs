using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using Agent.Core.Configuration;
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
    private readonly ArmClient _armClient;
    private readonly CrawlerSettings _crawlerSettings;
    private readonly object _lockObj;

    public ResourceGraphCrawler(ILogger<ResourceGraphCrawler> logger, CrawlerSettings crawlerSettings, ArmResourceCrawlerFactory factory, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, [FromKeyedServices("CrawlerArmClient")] ArmClient armClient)
    {
        _logger = logger;
        _factory = factory;
        _graphDbClient = graphDbClient;
        _graphClient = graphClient;
        _armClient = armClient;
        _crawlerSettings = crawlerSettings;
        _lockObj = new();
    }

    public async Task Crawl(string rootId, HashSet<Type> filters = null, CancellationToken? cancellationToken = null)
    {
        ArmResourceNode rootNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(rootId);
        await Crawl(new List<ArmResourceNode>() { rootNode }, filters);
    }

    public async Task Crawl(IList<ArmResourceNode> nodes, HashSet<Type> filters = null, CancellationToken? cancellationToken = null)
    {
        try
        {
            lock (_lockObj)
            {
                HashSet<string> crawled = new();
                Queue queue = new();
                Queue toCrawl = Queue.Synchronized(queue);
                ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();

                var startTS = DateTime.UtcNow.Ticks;
                var sw = new Stopwatch();
                sw.Start();

                foreach (var node in nodes)
                {
                    if (filters == null || filters.Contains(node.GetType()))
                    {
                        toCrawl.Enqueue(node);
                    }
                }

                while (toCrawl.Count > 0 || tasks.Count > 0)
                {
                    while (toCrawl.Count > 0 && tasks.Count < _crawlerSettings.MaxParallelism)
                    {
                        var node = toCrawl.Dequeue() as ArmResourceNode;
                        if (node == null)
                        {
                            continue;
                        }

                        if (crawled.Contains(node.GetHashString()))
                        {
                            continue;
                        }

                        crawled.Add(node.GetHashString());

                        tasks.Add(Task.Run(async () =>
                        {
                            var crawler = _factory.CreateFromNode(node, _graphDbClient, _graphClient, _armClient);
                            await foreach (var n in crawler.Crawl(node))
                            {
                                if (filters == null || filters.Contains(n.GetType()))
                                {
                                    toCrawl.Enqueue(n);
                                }
                            }

                            _logger.LogDebug($"Cleaning up stale edges from {node.ResourceId} (older than {startTS})");
                            await _graphDbClient.Query($"g.V('{GetSanitizedCosmosDBId(node.ResourceId)}').outE().or(__.not(has('updateTs')),__.has('updateTs', P.lt({startTS}))).drop()");
                        }));
                    }

                    if (tasks.Count == 0)
                    {
                        continue;
                    }

                    Task.WhenAny(tasks).Wait();
                    var newTasks = new ConcurrentBag<Task>();
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
                _logger.LogInformation($"Done crawling. Time taken: {sw.ElapsedMilliseconds}ms.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crawling resources");
        }
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
