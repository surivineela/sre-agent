// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Attributes;
using Agent.Core.DataConnectors;
using Azure.Search.Documents.Indexes;

namespace Agent.Plugins.DataConnectors.TSG;
// TODO: debug why TsgDocumentMetadata does not have all the required fields (that are now set to optional to unblock) being present in the object when the data is being pulled from blob/AI Search
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
    public DateTime? IndexedAt { get; init; }

    [SearchableField(IsFilterable = true)]
    public List<string>? Tags { get; init; }

    [SimpleField(IsFilterable = true)]
    public string? Team { get; init; }

    [SimpleField(IsFilterable = true)]
    public string? Repository { get; init; }

    [SimpleField(IsFilterable = true)]
    public string? FilePath { get; init; }

    [SearchableField]
    public string? MetadataConcat { get; init; }
}
