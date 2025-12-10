// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Plugin for managing a knowledge graph using entities, relations, and observations.
/// Provides persistent storage of conversational context and memories.
/// </summary>
public class KnowledgeGraphPlugin : IKnowledgeGraphPlugin
{
    private readonly IKnowledgeGraphStorageProvider _storageProvider;
    private readonly ILogger<KnowledgeGraphPlugin> _logger;

    public KnowledgeGraphPlugin(
        IKnowledgeGraphStorageProvider storageProvider,
        ILogger<KnowledgeGraphPlugin> logger)
    {
        _storageProvider = storageProvider;
        _logger = logger;
        _logger.LogInternalInformation("KnowledgeGraphPlugin initialized");
    }

    public async Task<List<Entity>> CreateEntitiesAsync(List<Entity> entities)
    {
        try
        {
            _logger.LogInternalDebug("Creating {Count} entities", entities.Count);
            var result = await _storageProvider.CreateEntitiesAsync(entities);
            _logger.LogInternalInformation("Created {Count} new entities (skipped {Skipped} duplicates)",
                result.Count, entities.Count - result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error creating entities");
            throw;
        }
    }

    public async Task<List<Relation>> CreateRelationsAsync(List<Relation> relations)
    {
        try
        {
            _logger.LogInternalDebug("Creating {Count} relations", relations.Count);
            var result = await _storageProvider.CreateRelationsAsync(relations);
            _logger.LogInternalInformation("Created {Count} new relations (skipped {Skipped} duplicates)",
                result.Count, relations.Count - result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error creating relations");
            throw;
        }
    }

    public async Task<List<ObservationResult>> AddObservationsAsync(List<ObservationInput> observations)
    {
        try
        {
            _logger.LogInternalDebug("Adding observations to {Count} entities", observations.Count);
            var result = await _storageProvider.AddObservationsAsync(observations);
            var totalAdded = result.Sum(r => r.AddedObservations.Count);
            _logger.LogInternalInformation("Added {Count} new observations", totalAdded);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error adding observations");
            throw;
        }
    }

    public async Task DeleteEntitiesAsync(List<string> entityNames)
    {
        try
        {
            _logger.LogInternalDebug("Deleting {Count} entities", entityNames.Count);
            await _storageProvider.DeleteEntitiesAsync(entityNames);
            _logger.LogInternalInformation("Deleted {Count} entities and their relations", entityNames.Count);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error deleting entities");
            throw;
        }
    }

    public async Task DeleteObservationsAsync(List<ObservationDeletion> deletions)
    {
        try
        {
            var totalObservations = deletions.Sum(d => d.Observations.Count);
            _logger.LogInternalDebug("Deleting {Count} observations from {EntityCount} entities",
                totalObservations, deletions.Count);
            await _storageProvider.DeleteObservationsAsync(deletions);
            _logger.LogInternalInformation("Deleted {Count} observations", totalObservations);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error deleting observations");
            throw;
        }
    }

    public async Task DeleteRelationsAsync(List<Relation> relations)
    {
        try
        {
            _logger.LogInternalDebug("Deleting {Count} relations", relations.Count);
            await _storageProvider.DeleteRelationsAsync(relations);
            _logger.LogInternalInformation("Deleted {Count} relations", relations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error deleting relations");
            throw;
        }
    }

    public async Task<KnowledgeGraph> ReadGraphAsync()
    {
        try
        {
            _logger.LogInternalDebug("Reading entire knowledge graph");
            var result = await _storageProvider.ReadGraphAsync();
            _logger.LogInternalInformation("Read knowledge graph: {EntityCount} entities, {RelationCount} relations",
                result.Entities.Count, result.Relations.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error reading knowledge graph");
            throw;
        }
    }

    public async Task<KnowledgeGraph> SearchNodesAsync(string query)
    {
        try
        {
            _logger.LogInternalInformation("Searching knowledge graph with query: {Query}", query);
            var result = await _storageProvider.SearchNodesAsync(query);
            _logger.LogInternalInformation("Search found {EntityCount} entities, {RelationCount} relations",
                result.Entities.Count, result.Relations.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error searching knowledge graph");
            throw;
        }
    }

    public async Task<KnowledgeGraph> OpenNodesAsync(List<string> names)
    {
        try
        {
            _logger.LogInternalDebug("Opening {Count} nodes", names.Count);
            var result = await _storageProvider.OpenNodesAsync(names);
            _logger.LogInternalInformation("Opened {EntityCount} entities, {RelationCount} relations",
                result.Entities.Count, result.Relations.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error opening nodes");
            throw;
        }
    }

    public async Task<GraphSchema> GetGraphSchemaAsync()
    {
        try
        {
            _logger.LogInternalDebug("Getting graph schema");
            var result = await _storageProvider.GetGraphSchemaAsync();
            _logger.LogInternalInformation("Graph schema: {EntityTypeCount} entity types, {RelationTypeCount} relation types",
                result.EntityTypeOverView.Count, result.RelationTypes.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting graph schema");
            throw;
        }
    }
}
