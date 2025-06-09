// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Models;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Plugins;
using Newtonsoft.Json;
using Agent.Core.Models;

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

        public async Task<SearchResult> GetTsgContent(string searchText, CancellationToken cancellationToken = default)
        {
            if (searchText?.Contains("MockNoResponse", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new SearchResult();
            }
            
            return await Task.FromResult(new SearchResult
            {
                Title = $"Mock TSG Result for: {searchText}",
                Content = $"This is a mock troubleshooting guide content for the query: '{searchText}'. It contains steps to diagnose and resolve the issue.",
                Confidence = "0.85",
                Source = "Mock TSG Repository",
                ResultType = "TSG",
                Rank = 1,
                Link = "https://example.com/mock-tsg-content"
            });
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

