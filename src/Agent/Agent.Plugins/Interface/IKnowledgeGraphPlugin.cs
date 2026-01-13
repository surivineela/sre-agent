// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface;

/// <summary>
/// Interface for managing a knowledge graph using entities, relations, and observations.
/// Provides persistent storage of conversational context and memories.
/// </summary>
public interface IKnowledgeGraphPlugin
{
    /// <summary>
    /// Create multiple new entities in the knowledge graph
    /// </summary>
    /// <param name="entities">Array of entities to create</param>
    /// <returns>Array of newly created entities (excludes duplicates)</returns>
    Task<List<Entity>> CreateEntitiesAsync(List<Entity> entities);

    /// <summary>
    /// Create multiple new relations between entities in the knowledge graph
    /// </summary>
    /// <param name="relations">Array of relations to create</param>
    /// <returns>Array of newly created relations (excludes duplicates)</returns>
    Task<List<Relation>> CreateRelationsAsync(List<Relation> relations);

    /// <summary>
    /// Add new observations to existing entities in the knowledge graph
    /// </summary>
    /// <param name="observations">Array of observations to add</param>
    /// <returns>Results showing which observations were added to each entity</returns>
    Task<List<ObservationResult>> AddObservationsAsync(List<ObservationInput> observations);

    /// <summary>
    /// Delete multiple entities and their associated relations from the knowledge graph
    /// </summary>
    /// <param name="entityNames">Array of entity names to delete</param>
    Task DeleteEntitiesAsync(List<string> entityNames);

    /// <summary>
    /// Delete specific observations from entities in the knowledge graph
    /// </summary>
    /// <param name="deletions">Array of observations to delete</param>
    Task DeleteObservationsAsync(List<ObservationDeletion> deletions);

    /// <summary>
    /// Delete multiple relations from the knowledge graph
    /// </summary>
    /// <param name="relations">Array of relations to delete</param>
    Task DeleteRelationsAsync(List<Relation> relations);

    /// <summary>
    /// Read the entire knowledge graph
    /// </summary>
    /// <returns>The complete knowledge graph with all entities and relations</returns>
    Task<KnowledgeGraph> ReadGraphAsync();

    /// <summary>
    /// Search for nodes in the knowledge graph based on a query
    /// </summary>
    /// <param name="query">Search query to match against entity names, types, and observations</param>
    /// <param name="entityType">Filter results to a specific entity type. If empty, all types are included.</param>
    /// <param name="includeNeighbors">Whether to include neighboring nodes in the results</param>
    /// <returns>Filtered knowledge graph containing matching entities and their relations</returns>
    Task<KnowledgeGraph> SearchNodesAsync(string query, string entityType, bool includeNeighbors);

    /// <summary>
    /// Open specific nodes in the knowledge graph by their names
    /// </summary>
    /// <param name="names">Array of entity names to retrieve</param>
    /// <returns>Knowledge graph containing the specified entities and their relations</returns>
    Task<KnowledgeGraph> OpenNodesAsync(List<string> names);

    /// <summary>
    /// Get the schema of the knowledge graph, including entity types with examples and relation types
    /// </summary>
    /// <returns>Graph schema containing entity types with examples and relation types</returns>
    Task<GraphSchema> GetGraphSchemaAsync();
}

/// <summary>
/// Represents an entity in the knowledge graph
/// </summary>
public class Entity
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public List<string> Observations { get; set; } = new();
}

/// <summary>
/// Represents a relation between two entities
/// </summary>
public class Relation
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
}

/// <summary>
/// Represents the complete knowledge graph structure
/// </summary>
public class KnowledgeGraph
{
    public List<Entity> Entities { get; set; } = new();
    public List<Relation> Relations { get; set; } = new();
}

/// <summary>
/// Input for adding observations to an entity
/// </summary>
public class ObservationInput
{
    public string EntityName { get; set; } = string.Empty;
    public List<string> Contents { get; set; } = new();
}

/// <summary>
/// Result of adding observations
/// </summary>
public class ObservationResult
{
    public string EntityName { get; set; } = string.Empty;
    public List<string> AddedObservations { get; set; } = new();
}

/// <summary>
/// Input for deleting observations from an entity
/// </summary>
public class ObservationDeletion
{
    public string EntityName { get; set; } = string.Empty;
    public List<string> Observations { get; set; } = new();
}

public class EntityTypeOverView
{
    public string EntityType { get; set; } = string.Empty;
    // top 10 distinct entity names of this type
    public List<string> Examples { get; set; } = new();
}

public class GraphSchema
{
    public List<EntityTypeOverView> EntityTypeOverView { get; set; } = new();
    // Top 10 unique entity types in the graph
    public List<string> RelationTypes { get; set; } = new();
}
