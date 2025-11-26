// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.DataConnectors;

/// <summary>
/// Base document for all data connector content.
/// The fields in this record are indexed. Derived records may add additional fields for retrieval from storage, but they will not be indexed.
/// </summary>
public abstract record DataConnectorSourceDocument
{
    /// <summary>
    /// A unique ID for the document. This is used as the indexed document ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// A title for the document. This is used as a searchable field in the index.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The type is used to filter documents for a specific data connector when searching the index.
    /// </summary>
    public string Type => GetType().Name;

    /// <summary>
    /// Additional search filter. Can be any string.
    /// </summary>
    public required string Filter { get; init; }

    /// <summary>
    /// The main content of the document. This gets chunked and vectorized for search.
    /// </summary>
    public required string Contents { get; init; }

    /// <summary>
    /// Gets the name of the derived type.
    /// </summary>
    public string DerivedTypeName => GetType().Name;
}
