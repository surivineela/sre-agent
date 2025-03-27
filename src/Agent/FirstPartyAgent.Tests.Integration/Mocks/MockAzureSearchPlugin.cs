using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Search;
using Azure.Search.Documents.Models;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Plugins;


namespace FirstPartyAgent.Tests.Integration.Mocks
{
    public class MockAzureSearchPlugin : IAzureSearchPlugin
    {
        public Task<IEnumerable<SearchResult<SearchDocument>>> PerformSemanticSearchAsync(string searchText, CancellationToken cancellationToken = default)
        {
            if (searchText?.Contains("error") == true)
            {
                return Task.FromResult(Enumerable.Empty<SearchResult<SearchDocument>>());
            }
            else
            {
                var searchResults = new List<SearchResult<SearchDocument>>();
                for (int i = 0; i < 5; i++)
                {
                    var searchDoc = new SearchDocument();
                    searchDoc.Add("content", $"Mocked content {i + 1} for search text '{searchText}'");
                    var searchResult = SearchModelFactory.SearchResult<SearchDocument>(searchDoc, 100 - i *10, default, default);
                    searchResults.Add(searchResult);
                }
                return Task.FromResult(searchResults.AsEnumerable());
            }
        }
    }
}
