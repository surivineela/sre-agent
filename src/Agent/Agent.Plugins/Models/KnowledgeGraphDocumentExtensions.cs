// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Models;

/// <summary>
/// Extension methods for converting between domain models and Cosmos DB documents
/// </summary>
public static class KnowledgeGraphDocumentExtensions
{
    /// <summary>
    /// Convert from domain Entity model to document
    /// </summary>
    public static KnowledgeGraphEntityDocument ToDocument(this Entity entity)
    {
        return new KnowledgeGraphEntityDocument
        {
            Id = Guid.NewGuid().ToString(),
            DocumentType = "entity",
            PartitionKey = "entity",
            Name = entity.Name,
            EntityType = entity.EntityType
        };
    }

    /// <summary>
    /// Convert document to domain Entity model
    /// </summary>
    public static Entity ToEntity(this KnowledgeGraphEntityDocument document)
    {
        return new Entity
        {
            Name = document.Name,
            EntityType = document.EntityType
        };
    }

    /// <summary>
    /// Convert from domain Relation model to document
    /// </summary>
    public static KnowledgeGraphRelationDocument ToDocument(this Relation relation)
    {
        return new KnowledgeGraphRelationDocument
        {
            Id = Guid.NewGuid().ToString(),
            DocumentType = "relation",
            PartitionKey = "relation",
            From = relation.From,
            To = relation.To,
            RelationType = relation.RelationType
        };
    }

    /// <summary>
    /// Convert document to domain Relation model
    /// </summary>
    public static Relation ToRelation(this KnowledgeGraphRelationDocument document)
    {
        return new Relation
        {
            From = document.From,
            To = document.To,
            RelationType = document.RelationType
        };
    }

    /// <summary>
    /// Create observation document for an entity
    /// </summary>
    public static KnowledgeGraphObservationDocument ToObservationDocument(string entityId, string content)
    {
        return new KnowledgeGraphObservationDocument
        {
            Id = Guid.NewGuid().ToString(),
            DocumentType = "observation",
            PartitionKey = "observation",
            EntityId = entityId,
            Content = content
        };
    }
}
