// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Models;

namespace Agent.Data.AgentMemory;

/// <summary>
/// Represents a document chunk in the search index
/// </summary>
public class DocumentContent : BaseIndexableContent
{
    public override string Type => "document";

    /// <summary>
    /// Creates a new document content instance
    /// </summary>
    /// <param name="id">Unique identifier for this chunk</param>
    /// <param name="content">The chunk's text content</param>
    /// <param name="title">Title of the document</param>
    /// <param name="parentId">ID of the parent document (optional, defaults to chunk ID)</param>
    /// <param name="additionalMetadata">Any additional metadata to store</param>
    public DocumentContent(
        string id,
        string content,
        string title,
        string? parentId = null,
        Dictionary<string, object>? additionalMetadata = null)
        : base(
            id: id,
            content: content,
            title: title,
            metadata: CreateMetadata(parentId, additionalMetadata))
    {
    }

    private static Dictionary<string, object> CreateMetadata(
        string? parentId,
        Dictionary<string, object>? additionalMetadata)
    {
        var metadata = additionalMetadata ?? new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(parentId))
            metadata["parent_id"] = parentId;

        metadata["indexed_at"] = DateTime.UtcNow;

        return metadata;
    }

    protected override void AddContentSpecificFields(SearchDocument doc)
    {
        // Documents currently don't have any specific fields beyond the base ones
        // If we add document-specific fields in the future, add them here
    }
}
