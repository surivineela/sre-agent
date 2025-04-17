// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Models;
using FirstPartyAgent.Constants;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Core.Models;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class AzureSearchPluginDefinition(IAzureSearchPlugin plugin)
    {
        private readonly IAzureSearchPlugin _plugin = plugin;

        [KernelFunction(KernelFunctionNames.AzureSearch.LookupRelatedGitHubIssues)]
        [Description("Perform a semantic search using Azure Search to get top 5 high confidence results.")]
        public async Task<IEnumerable<SearchResult<IndexedGitHubIssueModel>>> LookupRelatedGitHubIssues(
            [Description("Github issue URL, e.g. https://github.com/owner/repo-name/issues/issueNumber")] string issueUrl,
            [Description("Descriptive summary of the issue being looked up restated in 5 different ways.")] List<string> issueSummaries,
            CancellationToken cancellationToken = default)
        {
            var result = await _plugin.LookupRelatedGitHubIssues(issueUrl, issueSummaries, cancellationToken);
            return result;
        }
    }
}

