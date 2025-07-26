// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Models;

namespace Agent.Core.DataConnectors;

public record DataConnectorSearchResult<T> where T : DataConnectorSourceDocument
{
    public required SearchResult<DataConnectorIndexDocument> SearchResult { get; init; }

    public required T OriginalDocument { get; init; }
}
