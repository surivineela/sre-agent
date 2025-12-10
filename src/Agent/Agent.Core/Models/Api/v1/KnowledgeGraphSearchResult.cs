// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents a knowledge graph search result for streaming to frontend
/// </summary>
public record KnowledgeGraphSearchResult(
    string Query,
    IReadOnlyList<KnowledgeGraphEntity> Entities,
    IReadOnlyList<KnowledgeGraphRelation> Relations,
    DateTime Timestamp,
    int TotalEntities,
    int TotalRelations
);

/// <summary>
/// Represents an entity in the knowledge graph search result
/// </summary>
public record KnowledgeGraphEntity(
    string Name,
    string EntityType,
    IReadOnlyList<string> Observations
);

/// <summary>
/// Represents a relation in the knowledge graph search result
/// </summary>
public record KnowledgeGraphRelation(
    string From,
    string To,
    string RelationType
);
