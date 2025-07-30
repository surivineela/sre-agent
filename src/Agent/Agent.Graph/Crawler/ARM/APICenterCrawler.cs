using System.Net.Http;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Helpers;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using ApimConstants = Agent.Graph.Helpers.APIManagementGraphHelper.Constants;

namespace Agent.Graph.Crawler.ARM
{
    public class APICenterCrawler : GenericArmResourceCrawler
    {
        private readonly ILogger<APICenterCrawler> _logger;
        private readonly IGraphDatabaseClient _graphDbClient;
        private readonly AzureResourceGraphClient _graphClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public APICenterCrawler(ILogger<APICenterCrawler> logger, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, ArmClient armClient, IHttpClientFactory httpClientFactory)
        : base(logger, graphDbClient, armClient, false)
        {
            _logger = logger;
            _graphDbClient = graphDbClient;
            _graphClient = graphClient;
            _httpClientFactory = httpClientFactory;
        }

        public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
        {
            await foreach (var n in base.Crawl(node))
            {
                yield return n;
            }

            var apiCenterNode = (APICenterNode)node;
            _logger.LogInternalInformation($"Crawling API Center {apiCenterNode.ResourceId}");

            string resourceLinksJson = await GetApicResourceLinks(apiCenterNode.ResourceId);

            if (string.IsNullOrEmpty(resourceLinksJson))
            {
                yield break;
            }

            apiCenterNode.PopulateFromApiCenterResourceLinks(resourceLinksJson);

            await _graphDbClient.AddOrUpdateNodeAsync(apiCenterNode);
        }

        public async Task<string> GetApicResourceLinks(string apicArmResourceId)
        {
            _logger.LogInternalInformation($"Retrieving resource links for API Center with ID: {apicArmResourceId}");

            var requestUrl = $"{ApimConstants.ManagementAzureBaseUrl}{apicArmResourceId}{ApimConstants.ApicDefaultWorkspaceSegment}/links?api-version={ApimConstants.ApicApiVersion}";

            var httpClient = _httpClientFactory.CreateClient(ApimConstants.ArmOperation);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            HttpResponseMessage responseMessage = await httpClient.SendAsync(request);
            if (responseMessage == null || !responseMessage.IsSuccessStatusCode)
            {
                Console.WriteLine($"APICenterCrawler Failed to retrieve resource links. Status Code: {responseMessage?.StatusCode}");
                return string.Empty;
            }

            return await responseMessage.Content.ReadAsStringAsync();
        }
    }
}
