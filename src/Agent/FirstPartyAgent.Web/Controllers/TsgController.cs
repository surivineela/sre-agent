using System.Text;
using FirstPartyAgent.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TsgController : Controller
{
    private readonly ILogger<ApiController> _logger;
    private readonly TsgCrawlerClient _tsgCrawlerClient;

    public TsgController(
        ILogger<ApiController> logger,
        TsgCrawlerClient tsgCrawlerClient)
    {
        _logger = logger;
        _tsgCrawlerClient = tsgCrawlerClient;
    }

    /// <summary>
    /// Initiates the crawling of the TSG repository and stores the content.
    /// </summary>
    /// <returns>A status message indicating the result of the operation.</returns>
    [HttpPost("crawl")]
    [HttpOptions("crawl")]
    public async Task<IActionResult> CrawlRepository()
    {
        try
        {
            _logger.LogInformation("Starting TSG repository crawl process");
            await _tsgCrawlerClient.CrawlAndStoreRepositoryAsync();
            return Ok(new { success = true, message = "Repository crawl completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during repository crawl process");
            return StatusCode(500, $"Error crawling repository: {ex.Message}");
        }
    }
}
