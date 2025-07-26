// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.DataConnectors;
using Azure.Search.Documents.Indexes;

namespace Agent.Plugins.DataConnectors.KustoMetadata;

public record KustoTableMetadata : DataConnectorSourceDocument
{
    public required string ClusterUri { get; init; }

    public required string DatabaseName { get; init; }

    public required string TableName { get; init; }

    public required string TableDescription { get; init; }

    public required List<KustoLogMessageSamples> LogMessageSamples { get; init; }

    public required List<KustoColumnMetadata> Columns { get; init; }
}

public record struct KustoLogMessageSamples
{
    public required string LogColumnName { get; init; }

    [SearchableField]
    public required List<string> UniqueMessages { get; init; }
}

public record struct KustoColumnMetadata
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    [SearchableField]
    public required string Description { get; init; }
}

public record KustoExampleQueryDocument : DataConnectorSourceDocument
{
    public required string ClusterUri { get; init; }

    public required string DatabaseName { get; init; }

    public required string TableName { get; init; }

    public required List<KustoExampleQueryAndDescription> ExampleQueries { get; init; }
}

public record struct KustoExampleQueryAndDescription
{
    public required string Id { get; init; }

    public required string Query { get; init; }

    public required string Description { get; init; }
}


