using Agent.Plugins.Helpers;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using FirstPartyAgent.Core.Services;

namespace FirstPartyAgent.Core.Plugins
{
    public class AzureSearchPlugin : IAzureSearchPlugin
    {
        private readonly IAzureSearchClient _searchClient;
        private readonly ILogger<AzureSearchPlugin> _logger;
        public AzureSearchPlugin(ILogger<AzureSearchPlugin> logger, IAzureSearchClient azureSearchClient) 
        {
            _logger = logger;
            _searchClient = azureSearchClient;
        }
        
        public async Task<IEnumerable<SearchResult<SearchDocument>>> PerformSemanticSearchAsync(
            string searchText,
            CancellationToken cancellationToken = default
            )
        {
            return await KernelFunctionHelpers.TryAction(
            nameof(AzureSearchPlugin),
            async () =>
            {
                var options = new SearchOptions
                {
                    QueryType = SearchQueryType.Full,
                    IncludeTotalCount = true
                };

                var semanticSearchResults = await _searchClient.SearchAsync<SearchDocument>(searchText, options, cancellationToken);
                _logger.LogInformation($"Count: {semanticSearchResults?.TotalCount ?? 0} Search query: {searchText}");
                if (semanticSearchResults?.TotalCount > 0)
                {
                    var results = semanticSearchResults.GetResults().ToList();
                    var highConfidenceResults = results.Where(searchResultDoc => searchResultDoc.Score > 50).ToList();
                    var extractedHighConfidenceDocs = highConfidenceResults.Select(searchResultDoc => searchResultDoc.Document).ToList();
                    return highConfidenceResults.Take(5);
                }
                else
                {
                    return new List<SearchResult<SearchDocument>>() { };
                }
            },
            _logger
        );
        }
    }
}
