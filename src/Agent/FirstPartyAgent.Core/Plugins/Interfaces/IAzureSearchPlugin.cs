using Azure.Search.Documents.Models;

namespace FirstPartyAgent.Plugins
{
    public interface IAzureSearchPlugin
    {
        public Task<IEnumerable<SearchResult<SearchDocument>>> PerformSemanticSearchAsync(string searchText, CancellationToken cancellationToken = default);
    }
}
