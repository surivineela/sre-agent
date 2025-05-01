using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Graph.Schema;

namespace Agent.Graph.Interfaces;
public interface ICrawlerService
{
    /// <summary>
    /// Crawls resources starting from the rootIds
    /// </summary>
    /// <param name="rootIds">Resource ids to start crawl</param>
    /// <param name="filters">Only crawl specific resource types</param>
    /// <param name="cascade">Crawl discovered resources too</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task CrawlAsync(IEnumerable<string> rootIds, IEnumerable<string>? filters = null, bool cascade = true, CancellationToken? cancellationToken = null);

    /// <summary>
    /// Starts the crawler that would crawl resource on updates. This implicitly does non-cascade crawl.
    /// </summary>
    /// <param name="resourceIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public void StartActivityLogCrawler(IEnumerable<string> resourceIds, CancellationToken? cancellationToken = null);

    /// <summary>
    /// Gets the current crawler result. This is a snapshot of the crawler state.
    /// </summary>
    public Task<CrawlerResult> GetCrawlerResult();
}
