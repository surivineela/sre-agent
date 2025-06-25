// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Plugins.Interface
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
        /// <returns>A collection of search results</returns>
        Task<List<SearchDocument>> SearchAsync(
            string searchText);
    }
}
