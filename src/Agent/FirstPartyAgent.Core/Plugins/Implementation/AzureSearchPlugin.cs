// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Plugins.Helpers;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Agent.Core.Configuration;

namespace FirstPartyAgent.Core.Plugins
{
    public class AzureSearchPlugin : IAzureSearchPlugin
    {
        private readonly IAzureSearchClient _searchClient;
        private readonly ILogger<AzureSearchPlugin> _logger;
        private readonly TsgCrawlerSettings _tsgSettings;

        public AzureSearchPlugin(ILogger<AzureSearchPlugin> logger, IAzureSearchClient azureSearchClient, TsgCrawlerSettings tsgSettings)
        {
            _logger = logger;
            _searchClient = azureSearchClient;
            _tsgSettings = tsgSettings;
        }

        public async Task<IEnumerable<SearchResult<IndexedGitHubIssueModel>>> LookupRelatedGitHubIssues(
            string issueUrl,
            List<string> issueSummaries,
            CancellationToken cancellationToken = default)
        {
            return await KernelFunctionHelpers.TryAction(
            nameof(AzureSearchPlugin),
            async () =>
            {
                var (owner, repo, issueNumber) = GitHubHelper.ParseGitHubIssueUrl(issueUrl);
                issueSummaries = issueSummaries.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

                string completeRepo = $"{owner}/{repo}".ToLower();
                switch (completeRepo)
                {
                    case "nmallick1/azure-functions-host":
                        owner = "azure";
                        break;

                    case "nmallick1/azure-sdk-for-net":
                        owner = "azure";
                        break;

                    default:
                        break;
                }
                if (owner.Equals("nmallick1", StringComparison.OrdinalIgnoreCase) && repo.Equals("azure-functions-host", StringComparison.OrdinalIgnoreCase))
                {
                    owner = "azure";
                }
                string searchIndex = $"githubissues_{owner}_{repo}".ToLower();

                _logger.LogInformation($"Search Index: {searchIndex} Query: {string.Join("\n|||||\n", issueSummaries)}");

                var tasks = issueSummaries.Select(issueDescription =>
                {
                    return _searchClient.SearchAsync<IndexedGitHubIssueModel>(searchIndex, issueDescription, null, cancellationToken);
                });

                var searchResults = await Task.WhenAll(tasks);
                var results = searchResults.SelectMany(result =>
                {
                    if (result.TotalCount > 0)
                    {
                        var latestResults = result.GetResults().Where(r => r.Document.lastUpdatedTimestamp > DateTime.UtcNow.AddYears(-1)).ToList();
                        if (latestResults.Count > 0)
                        {
                            return latestResults;
                        }
                        else
                        {
                            return new List<SearchResult<IndexedGitHubIssueModel>>();
                        }
                    }
                    else
                    {
                        return new List<SearchResult<IndexedGitHubIssueModel>>();
                    }
                });

                // Remove duplicates from the results by id and pick the one with highest score if multiple matches exist
                var uniqueResults = results.GroupBy(x => x.Document.issueId)
                    .Select(g => g.OrderByDescending(x => x.Score).First())
                    .ToList();

                _logger.LogInformation($"Search result Count: {uniqueResults.Count}");
                if (uniqueResults.Count > 0)
                {
                    return uniqueResults.Take(5);
                }
                else
                {
                    return new List<SearchResult<IndexedGitHubIssueModel>>();
                }
            },
            _logger
            );
        }
    }
}
