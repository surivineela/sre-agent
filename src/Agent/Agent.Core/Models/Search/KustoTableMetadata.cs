// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Indexes;

namespace Agent.Core.Models.Search;

public record KustoTableMetadata
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    public required string Id { get; init; }

    [SimpleField(IsFilterable = true)]
    public required string DatabaseName { get; init; }

    [SearchableField(IsFilterable = true)]
    public required string TableName { get; init; }

    [SearchableField]
    public required string TableDescription { get; init; }

    public required IEnumerable<KustoColumnMetadata> Columns { get; init; }

    [SearchableField]
    public required string MetadataConcat { get; init; }

}
public record struct KustoColumnMetadata
{
    [SearchableField(IsFilterable = true)]
    public required string Name { get; init; }

    [SearchableField(IsFilterable = true)]
    public required string Type { get; init; }

    [SearchableField]
    public required string Description { get; init; }
}

public record KustoExampleQueryDocument
{
    [SimpleField(IsKey = true, IsFilterable = true)] // <-- Add this!
    public required string Id { get; init; }

    [SimpleField(IsFilterable = true)]
    public required string DatabaseName { get; init; }

    [SearchableField(IsFilterable = true)]
    public required string TableName { get; init; }

    public required IEnumerable<KustoExampleQueryAndDescription> ExampleQueries { get; init; }

    [SearchableField]
    public required string MetadataConcat { get; init; }
}

public record struct KustoExampleQueryAndDescription
{
    public required string Id { get; init; }

    [SearchableField]
    public required string Query { get; init; }

    [SearchableField]
    public required string Description { get; init; }
}


