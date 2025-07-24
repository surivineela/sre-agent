// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;

namespace Agent.Tests.Common.Mocks;

/// <summary>
/// Mock implementation of ISearchEndpointService for testing.
/// 
/// Usage:
/// - Initialize with a dictionary of SearchDocument where the key is the document ID
/// - SearchDocumentsAsync treats the query parameter as a document ID and returns the matching document
/// - GetAllDocumentsAsync returns all documents in the dictionary
/// </summary>
public class MockSearchEndpointService : ISearchEndpointService
{
    private readonly Dictionary<string, SearchDocument> _documents;

    public MockSearchEndpointService(Dictionary<string, SearchDocument>? documents = null)
    {
        _documents = documents ?? new Dictionary<string, SearchDocument>();
    }

    public Task<IReadOnlyList<SearchDocument>> GetAllDocumentsAsync()
    {
        return Task.FromResult<IReadOnlyList<SearchDocument>>(_documents.Values.ToList());
    }

    public Task<IReadOnlyList<SearchDocument>> SearchDocumentsAsync(string query, float[]? vectors, SearchType searchType, int? top = null)
    {
        var results = new List<SearchDocument>();

        if (_documents.TryGetValue(query, out var document))
        {
            results.Add(document);
        }

        return Task.FromResult<IReadOnlyList<SearchDocument>>(results);
    }
}
