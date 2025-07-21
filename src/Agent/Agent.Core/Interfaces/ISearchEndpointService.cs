// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Core.Services;

namespace Agent.Core.Interfaces;

public interface ISearchEndpointService
{
    /// <summary>
    /// Returns all documents in the search index
    /// </summary>
    Task<IReadOnlyList<SearchDocument>> GetAllDocumentsAsync();

    /// <summary>
    /// Searches for documents containing the specified search query and vectors
    /// </summary>
    /// <param name="query">The query to search for in the documents</param>
    /// <param name="vectors">The vectors to use for vector search</param>
    Task<IReadOnlyList<SearchDocument>> SearchDocumentsAsync(string query, float[]? vectors, SearchType searchType, int? top = null);
}
