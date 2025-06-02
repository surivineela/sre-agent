// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Plugins.Definitions
{
    public class SearchPluginDefinition
    {
        private readonly ISearchPlugin _plugin;

        public SearchPluginDefinition(ISearchPlugin plugin)
        {
            _plugin = plugin;
        }

        [KernelFunction("SearchAsync")]
        [Description("Performs a semantic search using Azure AI Search to find relevant documents.")]
        public async Task<List<SearchArticle>> SearchAsync(
            [Description("The name of the search index to query")] string searchIndex,
            [Description("The search query text")] string searchText)
        {
            return await _plugin.SearchAsync(searchIndex, searchText);
        }
    }
}
