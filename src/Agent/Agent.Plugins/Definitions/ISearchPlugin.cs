// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Azure.Search.Documents.Models;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Interface for interacting with Azure AI Search to perform semantic search operations.
    /// </summary>
    public interface ISearchPlugin
    {
        /// <summary>
        /// Performs a semantic search using Azure AI Search to find relevant documents.
        /// </summary>
        /// <param name="searchIndex">The name of the search index to query</param>
        /// <param name="searchText">The search query text</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A collection of search results</returns>
        Task<List<SearchArticle>> SearchAsync(
            string searchIndex,
            string searchText,
            CancellationToken cancellationToken = default);
    }
}
