// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Plugins.DataConnectors.TSG;
using Agent.Plugins.Interface;
using Azure.Search.Documents.Models;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class AzureSearchPluginDefinition(Agent.Plugins.Interface.IAzureSearchPlugin plugin)
    {
        private readonly Agent.Plugins.Interface.IAzureSearchPlugin _plugin = plugin;

        [KernelFunction(KernelFunctionNames.AzureSearch.LookupRelatedGitHubIssues)]
        [Description("Perform a semantic search using Azure Search to get top 5 high confidence results.")]
        public async Task<IEnumerable<SearchResult<IndexedGitHubIssueModel>>> LookupRelatedGitHubIssues(
            [Description("Github issue URL, e.g. https://github.com/owner/repo-name/issues/issueNumber")] string issueUrl,
            [Description("Descriptive summary of the issue being looked up restated in 5 different ways.")] List<string> issueSummaries,
            CancellationToken cancellationToken = default)
        {
            var result = await _plugin.LookupRelatedGitHubIssues(issueUrl, issueSummaries, cancellationToken);
            // Cast from objects to the expected type
            return result.Cast<SearchResult<IndexedGitHubIssueModel>>();
        }

        [KernelFunction(KernelFunctionNames.AzureSearch.GetTsgContent)]
        [Description("Retrieve TSG (Troubleshooting Guide) content from Azure Search based on search text. Returns up to maxResults documents with relevant troubleshooting content and metadata.")]
        public async Task<IReadOnlyList<TsgDocumentMetadata>> GetTsgContent(
            [Description("Text to search for in the TSG content")] string searchText,
            [Description("Maximum number of results to return (default: 5)")] int maxResults = 5,
            CancellationToken cancellationToken = default)
        {
            var result = await _plugin.GetTsgContent(searchText, maxResults, cancellationToken);
            return result;
        }
    }
}

