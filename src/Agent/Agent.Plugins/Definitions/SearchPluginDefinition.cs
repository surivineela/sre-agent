// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Interface;
using System.ComponentModel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.KnowledgeBase)]
    public class SearchPluginDefinition
    {
        private readonly ISearchPlugin _plugin;

        public SearchPluginDefinition(ISearchPlugin plugin)
        {
            _plugin = plugin;
        }

        [Description("""
            Peforms a semantic search for documents in a knowledge base. The knowledge base contains up-to-date documentation that may be newer than your own knowledge.
            The knowledge base contains following topics:
            - Az CLI documentation
            - Kubectl documentation
            - Documentation and user manual of yourself, Azure SRE Agent.
            """)]
        public async Task<List<SearchDocument>> SearchDocumentsAsync(
            [Description("The plain text question/query to be searched in the knowledge base")] string searchText)
        {
            return await _plugin.SearchAsync(searchText);
        }
    }
}
