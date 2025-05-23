// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.Services;

public interface ISearchEndpointService
{
    /// <summary>
    /// Returns all documents in the search index
    /// </summary>
    Task<IReadOnlyList<SearchDocument>> GetAllDocumentsAsync();

    /// <summary>
    /// Searches for documents containing the specified search term
    /// </summary>
    /// <param name="term">The term to search for in the documents</param>
    Task<IReadOnlyList<SearchDocument>> SearchDocumentsAsync(string term);
}
