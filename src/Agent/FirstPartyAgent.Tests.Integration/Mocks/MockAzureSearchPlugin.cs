// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Models;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Plugins;
using Newtonsoft.Json;
using Agent.Core.Models;
using Agent.Plugins.DataConnectors.TSG;

namespace FirstPartyAgent.Tests.Integration.Mocks
{
    public class MockAzureSearchPlugin : IAzureSearchPlugin
    {
        public async Task<IEnumerable<SearchResult<IndexedGitHubIssueModel>>> LookupRelatedGitHubIssues(string issueUrl, List<string> issueDescriptions, CancellationToken cancellationToken = default)
        {
            var searchResults = new List<SearchResult<IndexedGitHubIssueModel>>();
            foreach (var issueDescription in issueDescriptions)
            {
                var results = await GetSearchResult(issueDescription);
                searchResults.AddRange(results);
            }
            return searchResults;
        }

        public async Task<IReadOnlyList<TsgDocumentMetadata>> GetTsgContent(string searchText, int maxResults = 5, CancellationToken cancellationToken = default)
        {
            if (searchText?.Contains("MockNoResponse", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Array.Empty<TsgDocumentMetadata>();
            }
            
            var mockResults = new List<TsgDocumentMetadata>();
            for (int i = 0; i < Math.Min(maxResults, 3); i++) // Return up to 3 mock results or maxResults, whichever is smaller
            {
                mockResults.Add(new TsgDocumentMetadata
                {
                    Id = $"mock-tsg-{i + 1}",
                    Title = $"Mock TSG Result {i + 1} for: {searchText}",
                    Contents = $"This is mock troubleshooting guide content #{i + 1} for the query: '{searchText}'. It contains steps to diagnose and resolve the issue.",
                    Filter = "Mock Documentation",
                    Source = "Mock Documentation",
                    DocumentType = "troubleshooting-guide",
                    ServiceName = $"Mock Service {i + 1}",
                    Tags = new List<string> { "mock", "test", $"category-{i}" },
                    LastModified = DateTime.UtcNow.AddDays(-i),
                    IndexedAt = DateTime.UtcNow,
                    Url = $"https://example.com/mock-tsg-content-{i + 1}",
                    MetadataConcat = $"Mock TSG Result {i + 1} troubleshooting-guide mock test"
                });
            }

            return await Task.FromResult(mockResults);
        }

        private Task<IEnumerable<SearchResult<IndexedGitHubIssueModel>>> GetSearchResult(string issueDescription)
        {
            if (issueDescription?.Contains("MockNoResponse", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(Enumerable.Empty<SearchResult<IndexedGitHubIssueModel>>());
            }
            else
            {
                var searchResults = new List<SearchResult<IndexedGitHubIssueModel>>();
                for (int i = 0; i < 5; i++)
                {
                    IndexedGitHubIssueComment comment = new IndexedGitHubIssueComment();
                    if (i % 2 == 0)
                    {
                        comment.body = $"Mock comment {i + 1} for {issueDescription}";
                        comment.commentTimestamp = DateTime.UtcNow.AddDays(-i);
                    }

                    var searchDoc = new IndexedGitHubIssueModel()
                    {
                        id = $"{i + 100}",
                        issueId = $"{i + 1}",
                        owner = "mockorg",
                        repository = "mockrepo",
                        issueUrl = $"https://www.123.github.com/mockorg/mockrepo/issues/{i + 1}",
                        title = $"Mock issue {i + 1} for {issueDescription}",
                        body = $"This is mock data for iteration {i + 1} corresponding to {issueDescription}",
                        comments = JsonConvert.SerializeObject(new List<IndexedGitHubIssueComment> { comment }),
                        labels = $"label-{i},label:{i + 1}",
                        state = i % 2 == 0 ? "open" : "closed",
                        descriptiveSummary = $"Mocked summary {i + 1} for {issueDescription}",
                        createdTimestamp = DateTime.UtcNow.AddDays(-i),
                        lastUpdatedTimestamp = DateTime.UtcNow.AddDays(-i),
                    };
                    var searchResult = SearchModelFactory.SearchResult<IndexedGitHubIssueModel>(searchDoc, 100 - i * 10, default, default);
                    searchResults.Add(searchResult);
                }
                return Task.FromResult(searchResults.AsEnumerable());
            }
        }
    }
}

