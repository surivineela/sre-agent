// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Search.Documents.Models;

namespace Agent.Data.AgentMemory;

/// <summary>
/// Base class for all indexable content types providing common implementation
/// </summary>
public abstract class BaseIndexableContent : IIndexableContent
{
    protected BaseIndexableContent(string id, string content, string title, Dictionary<string, object>? metadata = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Metadata = metadata ?? new Dictionary<string, object>();
    }

    public string Id { get; }
    public abstract string Type { get; }
    public string Content { get; }
    public string Title { get; }
    public Dictionary<string, object> Metadata { get; }

    /// <summary>
    /// Maps the content to a search document matching the index schema
    /// </summary>
    public virtual SearchDocument ToSearchDocument()
    {
        var doc = new SearchDocument
        {
            ["chunk_id"] = Id,
            ["chunk"] = Content,
            ["type"] = Type,
            ["title"] = Title,
        };

        if (Metadata.TryGetValue("parent_id", out var parentId))
            doc["parent_id"] = parentId;

        if (Metadata.TryGetValue("indexed_at", out var indexedAt))
            doc["indexed_at"] = indexedAt;

        // Let derived classes add their specific fields
        AddContentSpecificFields(doc);

        return doc;
    }

    /// <summary>
    /// Derived classes should override this to add their content-specific fields to the search document
    /// </summary>
    protected virtual void AddContentSpecificFields(SearchDocument doc)
    {
        // No fields to add in base class
    }
}
