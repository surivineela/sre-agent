// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Attributes;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace Agent.Core.DataConnectors;

public class DataConnectorIndexDocument
{
    [SearchableField(IsKey = true, IsFilterable = true, AnalyzerName = LexicalAnalyzerName.Values.Keyword)]
    public required string Id { get; init; }

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true, AnalyzerName = LexicalAnalyzerName.Values.Keyword)]
    public string ChunkId { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public string ParentId { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    public required string Type { get; init; }

    [SearchableField(IsFilterable = true, IsSortable = true, IsFacetable = true)]
    public required string Filter { get; init; }

    [SimpleField]
    public required string SourceDocumentUrl { get; init; }

    [SearchableField]
    [SemanticSearch(SemanticSearchFieldType.ContentField)]
    public required string Title { get; init; }

    [SearchableField]
    [SemanticSearch(SemanticSearchFieldType.ContentField)]
    public required string Chunk{ get; init; }

    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "dataConnectorVectorProfile", IsHidden = true)]
    public List<float>? Vector { get; init; }
}
