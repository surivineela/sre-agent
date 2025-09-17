// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class AzureSearchPluginDefinition(FirstPartyAgent.Core.Plugins.IAzureSearchPlugin plugin)
    {
        private readonly FirstPartyAgent.Core.Plugins.IAzureSearchPlugin _plugin = plugin;

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
    }
}

