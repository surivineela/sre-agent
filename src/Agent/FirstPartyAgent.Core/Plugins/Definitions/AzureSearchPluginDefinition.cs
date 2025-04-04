// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Models;
using FirstPartyAgent.Constants;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class AzureSearchPluginDefinition(IAzureSearchPlugin plugin)
    {
        private readonly IAzureSearchPlugin _plugin = plugin;

        [KernelFunction(KernelFunctionNames.AzureSearch.PerformSemanticSearch)]
        [Description("Perform a semantic search using Azure Search to get top 5 high confidence results.")]
        public async Task<IEnumerable<SearchResult<SearchDocument>>> PerformSemanticSearchAsync(
            [Description("Search text to lookup via semantic search.")] string searchText,
            CancellationToken cancellationToken = default
            )
        {
            var result = await _plugin.PerformSemanticSearchAsync(searchText);
            return result;
        }
    }
}

