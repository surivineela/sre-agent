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
        /// <param name="searchText">The search query text</param>
        /// <returns>A collection of search results</returns>
        Task<string> SearchDocumentsAsync(
            string searchText);

        /// <summary>
        /// Performs a semantic search using Azure AI Search to find relevant runbooks.
        /// </summary>
        /// <param name="searchText">The search query text</param>
        /// <returns>A collection of search results</returns>
        Task<string> SearchRunbooksAsync(
            string searchText);
    }
}
