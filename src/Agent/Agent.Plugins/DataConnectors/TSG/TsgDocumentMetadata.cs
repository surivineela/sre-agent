// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Attributes;
using Agent.Core.DataConnectors;
using Azure.Search.Documents.Indexes;

namespace Agent.Plugins.DataConnectors.TSG;

public record TsgDocumentMetadata : DataConnectorSourceDocument
{
    [SimpleField(IsFilterable = true)]
    public string? Source { get; init; }

    [SimpleField(IsFilterable = true)]
    public string? DocumentType { get; init; }

    [SimpleField(IsFilterable = true)]
    public string? ServiceName { get; init; }

    [SimpleField]
    public string? Url { get; init; }

    [SimpleField]
    public DateTime? LastModified { get; init; }

    [SimpleField]
    public required DateTime IndexedAt { get; init; }

    [SearchableField(IsFilterable = true)]
    public required List<string> Tags { get; init; }

    [SimpleField(IsFilterable = true)]
    public string? Team { get; init; }

    [SimpleField(IsFilterable = true)]
    public string? Repository { get; init; }

    [SimpleField(IsFilterable = true)]
    public string? FilePath { get; init; }

    [SearchableField]
    public required string MetadataConcat { get; init; }
}
