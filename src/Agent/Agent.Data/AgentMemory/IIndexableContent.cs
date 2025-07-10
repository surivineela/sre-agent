// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.AgentMemory;

/// <summary>
/// Defines the contract for content that can be indexed in the search service.
/// </summary>
public interface IIndexableContent
{
    /// <summary>
    /// Unique identifier for the content
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Type of the content (e.g., "document", "trajectory")
    /// </summary>
    string Type { get; }

    /// <summary>
    /// The actual content to be indexed and searched
    /// </summary>
    string Content { get; }

    /// <summary>
    /// Additional metadata for the content
    /// </summary>
    Dictionary<string, object> Metadata { get; }
} 