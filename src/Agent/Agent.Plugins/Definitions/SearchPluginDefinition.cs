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
        private const string SearchTextDescription = """
            The plain text question/query to be searched in the knowledge base. , ensuring it:
            - Concisely captures the user's intent or problem.
            - Avoids including entity names, IDs, source filenames, or document names.
            - If the query pertains to yourself, reframe the question explicitly for Azure SRE Agent.
            - Excludes any text inside brackets ([]) or special characters.
            - Translates non-English questions into English before framing the search query.
            """;

        private readonly ISearchPlugin _plugin;

        public SearchPluginDefinition(ISearchPlugin plugin)
        {
            _plugin = plugin;
        }

        [Description("""
            Performs a search for documents in a knowledge base. The knowledge base contains up-to-date documentation that may be newer than your own knowledge.
            The knowledge base contains following topics:
            - Az CLI documentation
            - Kubectl documentation
            - Documentation and user manual of yourself, Azure SRE Agent.
            """)]
        public async Task<string> SearchDocumentsAsync(
            [Description(SearchTextDescription)] string searchText)
        {
            return await _plugin.SearchDocumentsAsync(searchText);
        }

        [Description("""
            Performs a search for runbooks for troubleshooting in a knowledge base. The knowledge base contains highly expertised runbooks for different areas.
            The knowledge base contains runbooks for following topics:
            - Kubernetes.
            """)]
        public async Task<string> SearchRunbooksAsync(
            [Description(SearchTextDescription)] string searchText)
        {
            return await _plugin.SearchRunbooksAsync(searchText);
        }
    }
}
