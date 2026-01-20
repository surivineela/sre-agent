// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Kusto.Data.Common;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Cosmos DB NoSQL implementation of knowledge graph storage
/// </summary>
public class CosmosDbKnowledgeGraphStorageProvider : IKnowledgeGraphStorageProvider
{
    private readonly Microsoft.Azure.Cosmos.Container _container;
    private readonly ILogger<CosmosDbKnowledgeGraphStorageProvider> _logger;
    private readonly IChatClientProvider _chatClientProvider;
    private const int MaxEntitiesInSearchResult = 200;
    private const int MaxEntitiesInEntityMatching = 50;
    private const int MaxEntitiesInObservationMatching = 50;

    // Record types for SQL query results (used with GetItemQueryIterator)
    private record EntitySearchResult(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("entityType")] string EntityType,
        [property: JsonPropertyName("similarityScores")] float[] SimilarityScores);
    private record ObservationEntityIdResult(
        [property: JsonPropertyName("entityId")] string EntityId,
        [property: JsonPropertyName("similarityScores")] float[] SimilarityScores);

    public CosmosDbKnowledgeGraphStorageProvider(
        Microsoft.Azure.Cosmos.Container container,
        ILogger<CosmosDbKnowledgeGraphStorageProvider> logger,
        IChatClientProvider chatClientProvider)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _chatClientProvider = chatClientProvider ?? throw new ArgumentNullException(nameof(chatClientProvider));
    }

    private const int MaxBatchSize = 30;

    public async Task<List<Entity>> CreateEntitiesAsync(List<Entity> entities)
    {
        _logger.LogInternalInformation("CreateEntitiesAsync called with {EntityCount} entities", entities.Count);
        var newEntities = new List<Entity>();

        // First, get all existing entity names in bulk
        var entityNames = entities.Select(e => e.Name).ToList();
        var existingNames = new HashSet<string>();

        _logger.LogInternalInformation("Checking for existing entities in database");
        var queryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>().Select(e => new { e.Name, e.DocumentType });
        var query = queryable.Where(e => e.DocumentType == "entity" && entityNames.Contains(e.Name));

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var doc in response)
            {
                existingNames.Add(doc.Name);
            }
        }

        // Filter to only new entities
        var entitiesToCreate = entities.Where(e => !existingNames.Contains(e.Name)).ToList();
        foreach (var skippedName in existingNames)
        {
            _logger.LogInternalInformation("Entity {Name} already exists, skipping", skippedName);
        }

        if (entitiesToCreate.Count == 0)
        {
            _logger.LogInternalInformation("No new entities to create, all {Count} entities already exist", entities.Count);
            return newEntities;
        }

        _logger.LogInternalInformation("Preparing to create {NewCount} new entities out of {TotalCount} requested",
            entitiesToCreate.Count, entities.Count);

        // Prepare all documents
        var entityDocuments = entitiesToCreate.Select(e => e.ToDocument()).ToList();
        var observationDocuments = new List<KnowledgeGraphObservationDocument>();

        foreach (var (entity, document) in entitiesToCreate.Zip(entityDocuments))
        {
            if (entity.Observations != null && entity.Observations.Count != 0)
            {
                foreach (var observation in entity.Observations)
                {
                    observationDocuments.Add(KnowledgeGraphDocumentExtensions.ToObservationDocument(document.Id, observation));
                }
            }
        }

        _logger.LogInternalInformation("Prepared {EntityDocCount} entity documents and {ObservationDocCount} observation documents",
            entityDocuments.Count, observationDocuments.Count);

        // Generate embeddings for entities and observations
        var embeddingGenerator = _chatClientProvider.EmbeddingModel;
        if (embeddingGenerator == null)
        {
            _logger.LogInternalWarning("Embedding generator is null, cannot create entities without embeddings");
            throw new InvalidOperationException("Embedding generator is not configured. Cannot create entities without embeddings.");
        }

        var embeddingOptions = new EmbeddingGenerationOptions
        {
            Dimensions = 1536, // Standard embedding dimension
        };

        // Bulk create entities using transactional batch (all entities share partition key "entity")
        var entityBatchIndex = 0;
        foreach (var batch in entityDocuments.Chunk(MaxBatchSize))
        {
            entityBatchIndex++;
            _logger.LogInternalInformation("Processing entity batch {BatchIndex} with {BatchSize} entities",
                entityBatchIndex, batch.Length);

            // Generate embeddings for entity names in this batch
            var batchEntityNames = batch.Select(e => e.Name).ToList();
            try
            {
                _logger.LogInternalInformation("Generating embeddings for {Count} entity names in batch {BatchIndex}",
                    batchEntityNames.Count, entityBatchIndex);

                var entityEmbeddings = await embeddingGenerator.GenerateAsync(batchEntityNames, embeddingOptions);

                if (entityEmbeddings == null || entityEmbeddings.Count == 0)
                {
                    _logger.LogInternalWarning("Embedding generation returned null or empty result for entity batch {BatchIndex}", entityBatchIndex);
                }
                else
                {
                    _logger.LogInternalInformation("Successfully generated {EmbeddingCount} embeddings for entity batch {BatchIndex}",
                        entityEmbeddings.Count, entityBatchIndex);

                    // Assign embeddings to entity documents
                    for (int i = 0; i < batch.Length; i++)
                    {
                        if (i < entityEmbeddings.Count && entityEmbeddings[i]?.Vector != null)
                        {
                            batch[i].Vector = entityEmbeddings[i].Vector.ToArray();
                        }
                        else
                        {
                            _logger.LogInternalWarning("Missing embedding for entity {EntityName} at index {Index} in batch {BatchIndex}",
                                batch[i].Name, i, entityBatchIndex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to generate embeddings for entity batch {BatchIndex}. Entities will be created without embeddings. Error: {ErrorMessage}",
                    entityBatchIndex, ex.Message);
            }

            var transactionalBatch = _container.CreateTransactionalBatch(new PartitionKey("entity"));
            foreach (var doc in batch)
            {
                transactionalBatch.CreateItem(doc);
            }
            var batchResponse = await transactionalBatch.ExecuteAsync();
            if (!batchResponse.IsSuccessStatusCode)
            {
                _logger.LogInternalWarning("Failed to create entities batch {BatchIndex}: {StatusCode}", entityBatchIndex, batchResponse.StatusCode);
                throw new InvalidOperationException($"Failed to create entities batch: {batchResponse.StatusCode}");
            }

            _logger.LogInternalInformation("Successfully created entity batch {BatchIndex} with {BatchSize} entities",
                entityBatchIndex, batch.Length);
        }

        // Bulk create observations using transactional batch (all observations share partition key "observation")
        if (observationDocuments.Count != 0)
        {
            _logger.LogInternalInformation("Creating {Count} observations across multiple batches", observationDocuments.Count);
            var observationBatchIndex = 0;

            foreach (var batch in observationDocuments.Chunk(MaxBatchSize))
            {
                observationBatchIndex++;
                _logger.LogInternalInformation("Processing observation batch {BatchIndex} with {BatchSize} observations",
                    observationBatchIndex, batch.Length);

                // Generate embeddings for observation contents in this batch
                var observationContents = batch.Select(o => o.Content).ToList();
                try
                {
                    _logger.LogInternalInformation("Generating embeddings for {Count} observations in batch {BatchIndex}",
                        observationContents.Count, observationBatchIndex);

                    var observationEmbeddings = await embeddingGenerator.GenerateAsync(observationContents, embeddingOptions);

                    if (observationEmbeddings == null || observationEmbeddings.Count == 0)
                    {
                        _logger.LogInternalWarning("Embedding generation returned null or empty result for observation batch {BatchIndex}", observationBatchIndex);
                    }
                    else
                    {
                        _logger.LogInternalInformation("Successfully generated {EmbeddingCount} embeddings for observation batch {BatchIndex}",
                            observationEmbeddings.Count, observationBatchIndex);

                        // Assign embeddings to observation documents
                        for (int i = 0; i < batch.Length; i++)
                        {
                            if (i < observationEmbeddings.Count && observationEmbeddings[i]?.Vector != null)
                            {
                                batch[i].Vector = observationEmbeddings[i].Vector.ToArray();
                            }
                            else
                            {
                                _logger.LogInternalWarning("Missing embedding for observation at index {Index} in batch {BatchIndex}. Content preview: {ContentPreview}",
                                    i, observationBatchIndex, batch[i].Content?.Substring(0, Math.Min(50, batch[i].Content?.Length ?? 0)));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Failed to generate embeddings for observation batch {BatchIndex}. Observations will be created without embeddings. Error: {ErrorMessage}",
                        observationBatchIndex, ex.Message);
                }

                var transactionalBatch = _container.CreateTransactionalBatch(new PartitionKey("observation"));
                foreach (var doc in batch)
                {
                    transactionalBatch.CreateItem(doc);
                }
                var batchResponse = await transactionalBatch.ExecuteAsync();
                if (!batchResponse.IsSuccessStatusCode)
                {
                    _logger.LogInternalWarning("Failed to create observations batch {BatchIndex}: {StatusCode}", observationBatchIndex, batchResponse.StatusCode);
                    throw new InvalidOperationException($"Failed to create observations batch: {batchResponse.StatusCode}");
                }

                _logger.LogInternalInformation("Successfully created observation batch {BatchIndex} with {BatchSize} observations",
                    observationBatchIndex, batch.Length);
            }
        }

        newEntities.AddRange(entitiesToCreate);
        foreach (var entity in entitiesToCreate)
        {
            _logger.LogInternalInformation("Created entity {Name} with {ObservationCount} observations",
                entity.Name, entity.Observations?.Count ?? 0);
        }

        _logger.LogInternalInformation("CreateEntitiesAsync completed. Created {CreatedCount} entities with {TotalObservations} total observations",
            newEntities.Count, observationDocuments.Count);

        return newEntities;
    }

    public async Task<List<Relation>> CreateRelationsAsync(List<Relation> relations)
    {
        var newRelations = new List<Relation>();

        if (!relations.Any())
        {
            return newRelations;
        }

        // Get all existing relations in bulk
        var existingRelationKeys = new HashSet<string>();
        var queryable = _container.GetItemLinqQueryable<KnowledgeGraphRelationDocument>();
        var query = queryable.Where(r => r.DocumentType == "relation");

        using var iterator = query.ToFeedIterator();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var doc in response)
            {
                existingRelationKeys.Add($"{doc.From}|{doc.To}|{doc.RelationType}");
            }
        }

        // Filter to only new relations
        var relationsToCreate = new List<Relation>();
        foreach (var relation in relations)
        {
            var key = $"{relation.From}|{relation.To}|{relation.RelationType}";
            if (existingRelationKeys.Contains(key))
            {
                _logger.LogInternalInformation("Relation {From}->{To} ({Type}) already exists, skipping",
                    relation.From, relation.To, relation.RelationType);
            }
            else
            {
                relationsToCreate.Add(relation);
            }
        }

        if (!relationsToCreate.Any())
        {
            return newRelations;
        }

        // Bulk create relations using transactional batch (all relations share partition key "relation")
        var documents = relationsToCreate.Select(r => r.ToDocument()).ToList();
        foreach (var batch in documents.Chunk(MaxBatchSize))
        {
            var transactionalBatch = _container.CreateTransactionalBatch(new PartitionKey("relation"));
            foreach (var doc in batch)
            {
                transactionalBatch.CreateItem(doc);
            }
            var batchResponse = await transactionalBatch.ExecuteAsync();
            if (!batchResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to create relations batch: {batchResponse.StatusCode}");
            }
        }

        newRelations.AddRange(relationsToCreate);
        foreach (var relation in relationsToCreate)
        {
            _logger.LogInternalInformation("Created relation {From}->{To} ({Type})",
                relation.From, relation.To, relation.RelationType);
        }

        return newRelations;
    }

    public async Task<List<ObservationResult>> AddObservationsAsync(List<ObservationInput> observations)
    {
        var results = new List<ObservationResult>();

        if (!observations.Any())
        {
            return results;
        }

        // First, bulk fetch all required entities
        var entityNames = observations.Select(o => o.EntityName).Distinct().ToList();
        var entityQueryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>()
            .Where(e => e.DocumentType == "entity" && entityNames.Contains(e.Name))
            .Select(e => new { e.Id, e.Name });
        var entityQuery = entityQueryable;

        var entityNameToId = new Dictionary<string, string>();
        using var entityIterator = entityQuery.ToFeedIterator();
        while (entityIterator.HasMoreResults)
        {
            var response = await entityIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                entityNameToId[doc.Name] = doc.Id;
            }
        }

        // Validate all entities exist
        foreach (var observation in observations)
        {
            if (!entityNameToId.ContainsKey(observation.EntityName))
            {
                throw new InvalidOperationException($"Entity with name {observation.EntityName} not found");
            }
        }

        // Bulk fetch all existing observations for these entities
        var entityIds = entityNameToId.Values.ToList();
        var existingObservationsQuery = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>()
            .Where(o => o.DocumentType == "observation" && entityIds.Contains(o.EntityId))
            .Select(o => new { o.EntityId, o.Content });
        var existingQuery = existingObservationsQuery;

        var existingObsByEntityId = new Dictionary<string, HashSet<string>>();
        using var existingIterator = existingQuery.ToFeedIterator();
        while (existingIterator.HasMoreResults)
        {
            var response = await existingIterator.ReadNextAsync();
            foreach (var obs in response)
            {
                if (!existingObsByEntityId.TryGetValue(obs.EntityId, out var set))
                {
                    set = new HashSet<string>();
                    existingObsByEntityId[obs.EntityId] = set;
                }
                set.Add(obs.Content);
            }
        }

        // Prepare all new observation documents
        var allNewObservationDocs = new List<KnowledgeGraphObservationDocument>();
        foreach (var observation in observations)
        {
            var entityId = entityNameToId[observation.EntityName];
            var existingContents = existingObsByEntityId.GetValueOrDefault(entityId) ?? new HashSet<string>();

            var newObservations = observation.Contents
                .Where(content => !existingContents.Contains(content))
                .ToList();

            foreach (var content in newObservations)
            {
                allNewObservationDocs.Add(KnowledgeGraphDocumentExtensions.ToObservationDocument(entityId, content));
            }

            results.Add(new ObservationResult
            {
                EntityName = observation.EntityName,
                AddedObservations = newObservations
            });
        }

        // Bulk create all new observations using transactional batch (all observations share partition key "observation")
        if (allNewObservationDocs.Any())
        {
            foreach (var batch in allNewObservationDocs.Chunk(MaxBatchSize))
            {
                var transactionalBatch = _container.CreateTransactionalBatch(new PartitionKey("observation"));
                foreach (var doc in batch)
                {
                    transactionalBatch.CreateItem(doc);
                }
                var batchResponse = await transactionalBatch.ExecuteAsync();
                if (!batchResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Failed to create observations batch: {batchResponse.StatusCode}");
                }
            }
        }

        return results;
    }

    public async Task DeleteEntitiesAsync(List<string> entityNames)
    {
        var entityIdsToDelete = new List<string>();

        foreach (var entityName in entityNames)
        {
            // Find entity by name using LINQ
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>()
                .Where(e => e.DocumentType == "entity" && e.Name == entityName)
                .Select(e => new { e.Id, e.PartitionKey });
            var query = queryable;

            using var iterator = query.ToFeedIterator();

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var document = response.FirstOrDefault();

                if (document != null)
                {
                    entityIdsToDelete.Add(document.Id);
                    try
                    {
                        // Delete the entity
                        await _container.DeleteItemAsync<KnowledgeGraphEntityDocument>(
                            document.Id,
                            new PartitionKey(document.PartitionKey));
                        _logger.LogInternalInformation("Deleted entity {Name}", entityName);
                    }
                    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogInternalInformation("Entity {Name} not found, skipping", entityName);
                    }
                }
                else
                {
                    _logger.LogInternalInformation("Entity {Name} not found, skipping", entityName);
                }
            }
        }

        // Delete all relations involving these entities using LINQ
        var relationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphRelationDocument>();
        var relationQuery = relationQueryable.Where(r =>
            r.DocumentType == "relation" &&
            (entityNames.Contains(r.From) || entityNames.Contains(r.To)));

        using var relationIterator = relationQuery.ToFeedIterator();
        while (relationIterator.HasMoreResults)
        {
            var response = await relationIterator.ReadNextAsync();
            foreach (var relation in response)
            {
                await _container.DeleteItemAsync<KnowledgeGraphRelationDocument>(
                    relation.Id,
                    new PartitionKey(relation.PartitionKey));
            }
        }

        // Delete all observations for these entities using their document IDs
        if (entityIdsToDelete.Any())
        {
            var observationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>()
                .Where(o => o.DocumentType == "observation" && entityIdsToDelete.Contains(o.EntityId))
                .Select(o => new { o.Id, o.PartitionKey });
            var observationQuery = observationQueryable;

            using var observationIterator = observationQuery.ToFeedIterator();
            while (observationIterator.HasMoreResults)
            {
                var response = await observationIterator.ReadNextAsync();
                foreach (var observation in response)
                {
                    await _container.DeleteItemAsync<KnowledgeGraphObservationDocument>(
                        observation.Id,
                        new PartitionKey(observation.PartitionKey));
                }
            }
        }
    }

    public async Task DeleteObservationsAsync(List<ObservationDeletion> deletions)
    {
        foreach (var deletion in deletions)
        {
            // Find entity by name to get its ID
            var entityQueryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>()
                .Where(e => e.DocumentType == "entity" && e.Name == deletion.EntityName)
                .Select(e => new { e.Id });
            var entityQuery = entityQueryable;

            using var entityIterator = entityQuery.ToFeedIterator();
            string? entityId = null;

            if (entityIterator.HasMoreResults)
            {
                var response = await entityIterator.ReadNextAsync();
                entityId = response.FirstOrDefault()?.Id;
            }

            if (entityId == null)
            {
                _logger.LogInternalWarning("Entity {Name} not found, skipping observation deletion", deletion.EntityName);
                continue;
            }

            // Find observations for this entity using its document ID
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>()
                .Where(o => o.DocumentType == "observation" && o.EntityId == entityId)
                .Select(o => new { o.Id, o.PartitionKey, o.Content });
            var query = queryable;

            using var iterator = query.ToFeedIterator();
            var observationsToDelete = new List<(string Id, string PartitionKey)>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var obs in response)
                {
                    if (deletion.Observations.Contains(obs.Content))
                    {
                        observationsToDelete.Add((obs.Id, obs.PartitionKey));
                    }
                }
            }

            // Delete matching observations
            foreach (var obs in observationsToDelete)
            {
                try
                {
                    await _container.DeleteItemAsync<KnowledgeGraphObservationDocument>(
                        obs.Id,
                        new PartitionKey(obs.PartitionKey));
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInternalInformation("Observation already deleted, skipping");
                }
            }

            if (observationsToDelete.Any())
            {
                _logger.LogInternalInformation("Deleted {Count} observations from entity {Name}",
                    observationsToDelete.Count, deletion.EntityName);
            }
            else
            {
                _logger.LogInternalWarning("No matching observations found for entity {Name}", deletion.EntityName);
            }
        }
    }

    public async Task DeleteRelationsAsync(List<Relation> relations)
    {
        foreach (var relation in relations)
        {
            // Find relation by from/to/type to get its ID
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphRelationDocument>();
            var query = queryable.Where(r =>
                r.DocumentType == "relation" &&
                r.From == relation.From &&
                r.To == relation.To &&
                r.RelationType == relation.RelationType);

            using var iterator = query.ToFeedIterator();

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var document = response.FirstOrDefault();

                if (document != null)
                {
                    try
                    {
                        await _container.DeleteItemAsync<KnowledgeGraphRelationDocument>(
                            document.Id,
                            new PartitionKey(document.PartitionKey));
                        _logger.LogInternalInformation("Deleted relation {From}->{To} ({Type})",
                            relation.From, relation.To, relation.RelationType);
                    }
                    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogInternalInformation("Relation {From}->{To} ({Type}) not found, skipping",
                            relation.From, relation.To, relation.RelationType);
                    }
                }
                else
                {
                    _logger.LogInternalInformation("Relation {From}->{To} ({Type}) not found, skipping",
                        relation.From, relation.To, relation.RelationType);
                }
            }
        }
    }

    public async Task<KnowledgeGraph> ReadGraphAsync()
    {
        var graph = new KnowledgeGraph();

        // Read all entities using LINQ
        var entityQueryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>()
            .Where(e => e.DocumentType == "entity")
            .Select(e => new { e.Id, e.Name, e.EntityType });
        var entityQuery = entityQueryable;

        var entityIdToEntityMap = new Dictionary<string, Entity>();
        using var entityIterator = entityQuery.ToFeedIterator();
        while (entityIterator.HasMoreResults)
        {
            var response = await entityIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                var entity = new Entity { Name = doc.Name, EntityType = doc.EntityType };
                graph.Entities.Add(entity);
                entityIdToEntityMap[doc.Id] = entity;
            }
        }

        // Read all observations and populate entities
        if (entityIdToEntityMap.Any())
        {
            var observationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>()
                .Where(o => o.DocumentType == "observation")
                .Select(o => new { o.EntityId, o.Content });
            var observationQuery = observationQueryable;

            using var observationIterator = observationQuery.ToFeedIterator();
            while (observationIterator.HasMoreResults)
            {
                var response = await observationIterator.ReadNextAsync();
                foreach (var obs in response)
                {
                    if (entityIdToEntityMap.TryGetValue(obs.EntityId, out var entity))
                    {
                        entity.Observations.Add(obs.Content);
                    }
                }
            }
        }

        // Read all relations using LINQ
        var relationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphRelationDocument>();
        var relationQuery = relationQueryable.Where(r => r.PartitionKey == "relation");

        using var relationIterator = relationQuery.ToFeedIterator();
        while (relationIterator.HasMoreResults)
        {
            var response = await relationIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                graph.Relations.Add(doc.ToRelation());
            }
        }

        return graph;
    }

    public async Task<KnowledgeGraph> SearchNodesAsync(string query, string entityType, bool includeNeighbors)
    {
        var graph = new KnowledgeGraph();
        var threadId = Core.ToolStatic.AsyncLocalThreadId.Value;

        // Get the graph schema for tokenization and identifier extraction
        var schema = await GetGraphSchemaAsync();

        // Tokenize the query into individual words
        var tokens = await TokenizeQueryWithLLM(query, schema);
        _logger.LogInternalInformation("[{ThreadId}] Searching knowledge graph with tokens: {Tokens}", threadId, string.Join(", ", tokens));

        // Extract identifiers from the query for exact matching
        var identifiers = await ExtractIdentifiersFromQuery(query, schema);
        _logger.LogInternalInformation("[{ThreadId}] Extracted identifiers from query: {Identifiers}", threadId, string.Join(", ", identifiers));

        if (tokens.Length == 0 && identifiers.Length == 0)
        {
            _logger.LogInternalInformation("[{ThreadId}] No tokens or identifiers generated from query, returning empty graph", threadId);
            throw new InvalidOperationException("The query is invalid or too vague to process. Try rephrasing the query.");
        }

        if (tokens.Length == 0)
        {
            tokens = identifiers.Take(5).ToArray();
            _logger.LogInternalInformation("[{ThreadId}] No tokens generated, using identifiers as tokens: {Tokens}", threadId, string.Join(", ", tokens));
        }

        // Generate embeddings for tokenized query
        var tokenEmbeddings = await GenerateEmbeddingsForTokensAsync(tokens, threadId);

        // Track entity data by ID
        var entityDataById = new OrderedDictionary<string, (string Name, string EntityType)>();

        // Get entities using hybrid search
        await GetEntitiesByHybridSearchAsync(tokens, tokenEmbeddings, entityType, identifiers, entityDataById, threadId);

        // Get entity IDs from observations using hybrid search
        var entityIdsFromObservations = await GetEntityIdsFromObservationsAsync(tokens, tokenEmbeddings, identifiers, threadId);

        // Fetch missing entities found via observations
        await FetchMissingEntitiesFromObservationsAsync(entityIdsFromObservations, entityDataById, threadId);

        // Fetch neighbor entities if requested
        if (includeNeighbors && entityDataById.Any())
        {
            await FetchNeighborEntitiesAsync(entityDataById, threadId);
        }

        // Build the result graph with limited entities
        var entityIdToEntityMap = BuildResultGraph(entityDataById, graph, threadId);

        // Fetch all observations for the entities in the result
        await FetchObservationsForEntitiesAsync(entityIdToEntityMap, threadId);

        // Get relations between found entities
        await FetchRelationsBetweenEntitiesAsync(graph, threadId);

        _logger.LogInternalInformation("[{ThreadId}] Search completed: {EntityCount} entities, {RelationCount} relations", threadId,
            graph.Entities.Count, graph.Relations.Count);
        return graph;
    }

    /// <summary>
    /// Generates embeddings for the given tokens using the embedding model.
    /// </summary>
    private async Task<List<float[]>> GenerateEmbeddingsForTokensAsync(string[] tokens, Guid threadId)
    {
        var embeddingGenerator = _chatClientProvider.EmbeddingModel;

        if (embeddingGenerator == null || tokens.Length == 0)
        {
            throw new InvalidOperationException("Embedding generator is not configured or no tokens provided.");
        }

        try
        {
            var embeddingOptions = new EmbeddingGenerationOptions { Dimensions = 1536 };
            var embeddings = await embeddingGenerator.GenerateAsync(tokens, embeddingOptions);
            var tokenEmbeddings = embeddings.Select(e => e.Vector.ToArray()).ToList();
            _logger.LogInternalInformation("[{ThreadId}] Generated {Count} embeddings for tokens", threadId, tokenEmbeddings.Count);
            return tokenEmbeddings;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "[{ThreadId}] Failed to generate embeddings for tokens, continuing with fulltext search only", threadId);
            throw;
        }

    }

    /// <summary>
    /// Searches entities using Cosmos DB hybrid search with RRF combining fulltext and vector search.
    /// </summary>
    private async Task GetEntitiesByHybridSearchAsync(
        string[] tokens,
        List<float[]> tokenEmbeddings,
        string entityType,
        string[] identifiers,
        OrderedDictionary<string, (string Name, string EntityType)> entityDataById,
        Guid threadId)
    {
        if (tokens.Length == 0)
        {
            return;
        }

        var entityTypeFilter = !string.IsNullOrEmpty(entityType) ? "AND c.entityType = @entityType" : "";

        // Build identifier filter for entity name matching
        var identifierFilter = "";
        if (identifiers.Length > 0)
        {
            var identifierConditions = identifiers.Select((_, i) => $"CONTAINS(c.name, @identifier{i})");
            identifierFilter = $"AND ({string.Join(" OR ", identifierConditions)})";
        }

        _logger.LogInternalInformation("Searching entities by hybrid search (fulltext + vector) on name, filtering by entityType: {EntityType}, identifiers: [{Identifiers}]", entityType, string.Join(", ", identifiers));

        // Build RRF function arguments: FullTextScore for each token + VectorDistance for each embedding
        var rrfFunctions = new List<string>();
        var vectorScores = new List<string>();

        // Add FullTextScore functions for each token
        for (int i = 0; i < tokens.Length; i++)
        {
            rrfFunctions.Add($"FullTextScore(c.name, @token{i})");
        }

        // Add VectorDistance functions for each embedding
        for (int i = 0; i < tokenEmbeddings.Count; i++)
        {
            var vectorDistanceExpr = $"VectorDistance(c.vector, @vector{i})";
            rrfFunctions.Add(vectorDistanceExpr);
            vectorScores.Add(vectorDistanceExpr);
        }

        var rrfClause = string.Join(", ", rrfFunctions);
        var vectorScoresClause = string.Join(", ", vectorScores);

        var entitySql = $@"
            SELECT TOP {MaxEntitiesInEntityMatching} c.id, c.name, c.entityType, [{vectorScoresClause}] AS similarityScores
            FROM c
            WHERE c.documentType = 'entity'
              {entityTypeFilter}
              {identifierFilter}
            ORDER BY RANK RRF({rrfClause})";

        var entityQueryDefinition = new QueryDefinition(entitySql);
        for (int i = 0; i < tokens.Length; i++)
        {
            entityQueryDefinition.WithParameter($"@token{i}", tokens[i]);
        }
        for (int i = 0; i < tokenEmbeddings.Count; i++)
        {
            entityQueryDefinition.WithParameter($"@vector{i}", tokenEmbeddings[i]);
        }
        if (!string.IsNullOrEmpty(entityType))
        {
            entityQueryDefinition.WithParameter("@entityType", entityType);
        }
        for (int i = 0; i < identifiers.Length; i++)
        {
            entityQueryDefinition.WithParameter($"@identifier{i}", identifiers[i]);
        }

        using var entityIterator = _container.GetItemQueryIterator<EntitySearchResult>(entityQueryDefinition);
        while (entityIterator.HasMoreResults)
        {
            var response = await entityIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                _logger.LogInternalInformation("[{ThreadId}] Entity '{Name}' (id: {Id}, type: {EntityType}) similarity scores: [{Scores}]",
                    threadId, doc.Name, doc.Id, doc.EntityType, string.Join(", ", doc.SimilarityScores ?? []));
                if (!entityDataById.ContainsKey(doc.Id))
                {
                    entityDataById[doc.Id] = (doc.Name, doc.EntityType);
                }
            }
        }
        _logger.LogInternalInformation("[{ThreadId}] Found {Count} entities matching hybrid search", threadId, entityDataById.Count);
    }

    /// <summary>
    /// Searches observations using Cosmos DB hybrid search and returns the entity IDs of matching observations.
    /// </summary>
    private async Task<HashSet<string>> GetEntityIdsFromObservationsAsync(
        string[] tokens,
        List<float[]> tokenEmbeddings,
        string[] identifiers,
        Guid threadId)
    {
        var entityIdsFromObservations = new HashSet<string>();

        if (tokens.Length == 0)
        {
            return entityIdsFromObservations;
        }

        // Build identifier filter for observation content matching
        var identifierFilter = "";
        if (identifiers.Length > 0)
        {
            var identifierConditions = identifiers.Select((_, i) => $"CONTAINS(c.content, @identifier{i})");
            identifierFilter = $"AND ({string.Join(" OR ", identifierConditions)})";
        }

        _logger.LogInternalInformation("[{ThreadId}] Searching observations by hybrid search (fulltext + vector) on content, identifiers: [{Identifiers}]", threadId, string.Join(", ", identifiers));

        // Build RRF function arguments for observations
        var rrfFunctions = new List<string>();
        var vectorScores = new List<string>();

        // Add FullTextScore functions for each token
        for (int i = 0; i < tokens.Length; i++)
        {
            rrfFunctions.Add($"FullTextScore(c.content, @token{i})");
        }

        // Add VectorDistance functions for each embedding
        for (int i = 0; i < tokenEmbeddings.Count; i++)
        {
            var vectorDistanceExpr = $"VectorDistance(c.vector, @vector{i})";
            rrfFunctions.Add(vectorDistanceExpr);
            vectorScores.Add(vectorDistanceExpr);
        }

        var rrfClause = string.Join(", ", rrfFunctions);
        var vectorScoresClause = string.Join(", ", vectorScores);

        var observationSql = $@"
            SELECT TOP {MaxEntitiesInObservationMatching} c.entityId, [{vectorScoresClause}] AS similarityScores
            FROM c
            WHERE c.documentType = 'observation'
              {identifierFilter}
            ORDER BY RANK RRF({rrfClause})";


        var observationQueryDefinition = new QueryDefinition(observationSql);
        for (int i = 0; i < tokens.Length; i++)
        {
            observationQueryDefinition.WithParameter($"@token{i}", tokens[i]);
        }
        for (int i = 0; i < tokenEmbeddings.Count; i++)
        {
            observationQueryDefinition.WithParameter($"@vector{i}", tokenEmbeddings[i]);
        }
        for (int i = 0; i < identifiers.Length; i++)
        {
            observationQueryDefinition.WithParameter($"@identifier{i}", identifiers[i]);
        }

        using var observationIterator = _container.GetItemQueryIterator<ObservationEntityIdResult>(observationQueryDefinition);
        while (observationIterator.HasMoreResults)
        {
            var response = await observationIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                _logger.LogInternalInformation("[{ThreadId}] Observation entityId '{EntityId}' similarity scores: [{Scores}]",
                    threadId, doc.EntityId, string.Join(", ", doc.SimilarityScores ?? []));
                entityIdsFromObservations.Add(doc.EntityId);
            }
        }
        _logger.LogInternalInformation("[{ThreadId}] Found {Count} entities with matching observations via hybrid search", threadId, entityIdsFromObservations.Count);

        return entityIdsFromObservations;
    }

    /// <summary>
    /// Fetches entity data for entities found via observations that are not already in the entityDataById dictionary.
    /// </summary>
    private async Task FetchMissingEntitiesFromObservationsAsync(
        HashSet<string> entityIdsFromObservations,
        OrderedDictionary<string, (string Name, string EntityType)> entityDataById,
        Guid threadId)
    {
        var missingEntityIds = entityIdsFromObservations
            .Where(id => !entityDataById.ContainsKey(id))
            .ToList();

        if (!missingEntityIds.Any())
        {
            return;
        }

        _logger.LogInternalInformation("[{ThreadId}] Fetching {Count} additional entities from observation matches", threadId, missingEntityIds.Count);
        var additionalEntityQueryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>()
            .Where(e => e.DocumentType == "entity" && missingEntityIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name, e.EntityType });
        var additionalEntityQuery = additionalEntityQueryable;

        using var additionalIterator = additionalEntityQuery.ToFeedIterator();
        while (additionalIterator.HasMoreResults)
        {
            var response = await additionalIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                if (!entityDataById.ContainsKey(doc.Id))
                {
                    entityDataById[doc.Id] = (doc.Name, doc.EntityType);
                }
            }
        }
    }

    /// <summary>
    /// Fetches direct neighbor entities of the found entities.
    /// </summary>
    private async Task FetchNeighborEntitiesAsync(
        OrderedDictionary<string, (string Name, string EntityType)> entityDataById,
        Guid threadId)
    {
        var foundEntityNames = entityDataById.Values.Select(e => e.Name).ToHashSet();
        _logger.LogInternalInformation("[{ThreadId}] Finding direct neighbors of {Count} entities", threadId, foundEntityNames.Count);

        // Find all relations that involve found entities (either as From or To)
        var neighborRelationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphRelationDocument>();
        var neighborRelationQuery = neighborRelationQueryable.Where(r =>
            r.DocumentType == "relation" &&
            (foundEntityNames.Contains(r.From) || foundEntityNames.Contains(r.To)));

        var neighborNames = new HashSet<string>();
        using var neighborRelationIterator = neighborRelationQuery.ToFeedIterator();
        while (neighborRelationIterator.HasMoreResults)
        {
            var response = await neighborRelationIterator.ReadNextAsync();
            foreach (var rel in response)
            {
                // Add the other end of the relation as a neighbor
                if (foundEntityNames.Contains(rel.From) && !foundEntityNames.Contains(rel.To))
                {
                    neighborNames.Add(rel.To);
                }
                if (foundEntityNames.Contains(rel.To) && !foundEntityNames.Contains(rel.From))
                {
                    neighborNames.Add(rel.From);
                }
            }
        }

        if (!neighborNames.Any())
        {
            return;
        }

        _logger.LogInternalInformation("[{ThreadId}] Fetching {Count} neighbor entities", threadId, neighborNames.Count);
        var neighborEntityQueryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>()
            .Where(e => e.DocumentType == "entity" && neighborNames.Contains(e.Name))
            .Select(e => new { e.Id, e.Name, e.EntityType });
        var neighborEntityQuery = neighborEntityQueryable;

        using var neighborIterator = neighborEntityQuery.ToFeedIterator();
        while (neighborIterator.HasMoreResults)
        {
            var response = await neighborIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                if (!entityDataById.ContainsKey(doc.Id))
                {
                    entityDataById[doc.Id] = (doc.Name, doc.EntityType);
                }
            }
        }
        _logger.LogInternalInformation("[{ThreadId}] Total entities after adding neighbors: {Count}", threadId, entityDataById.Count);
    }

    /// <summary>
    /// Builds the result graph with limited entities, preserving order.
    /// </summary>
    private Dictionary<string, Entity> BuildResultGraph(
        OrderedDictionary<string, (string Name, string EntityType)> entityDataById,
        KnowledgeGraph graph,
        Guid threadId)
    {
        var entityIdToEntityMap = new Dictionary<string, Entity>();
        var count = 0;

        foreach (var kvp in entityDataById)
        {
            if (count >= MaxEntitiesInSearchResult)
                break;

            var (name, entityTypeValue) = kvp.Value;
            var entity = new Entity { Name = name, EntityType = entityTypeValue };
            graph.Entities.Add(entity);
            entityIdToEntityMap[kvp.Key] = entity;
            count++;
        }
        _logger.LogInternalInformation("[{ThreadId}] Returning {Count} entities (limited to {MaxCount})", threadId, graph.Entities.Count, MaxEntitiesInSearchResult);

        return entityIdToEntityMap;
    }

    /// <summary>
    /// Fetches all observations for the entities in the result.
    /// </summary>
    private async Task FetchObservationsForEntitiesAsync(
        Dictionary<string, Entity> entityIdToEntityMap,
        Guid threadId)
    {
        if (!entityIdToEntityMap.Any())
        {
            return;
        }

        _logger.LogInternalInformation("[{ThreadId}] Fetching observations for {Count} entities", threadId, entityIdToEntityMap.Count);
        var entityIds = entityIdToEntityMap.Keys.ToList();
        var allObservationsQueryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>()
            .Where(o => o.DocumentType == "observation" && entityIds.Contains(o.EntityId))
            .Select(o => new { o.EntityId, o.Content });
        var allObservationsQuery = allObservationsQueryable;

        using var allObservationsIterator = allObservationsQuery.ToFeedIterator();
        var observationCount = 0;
        while (allObservationsIterator.HasMoreResults)
        {
            var response = await allObservationsIterator.ReadNextAsync();
            foreach (var obs in response)
            {
                if (entityIdToEntityMap.TryGetValue(obs.EntityId, out var entity))
                {
                    entity.Observations.Add(obs.Content);
                    observationCount++;
                }
            }
        }
        _logger.LogInternalInformation("[{ThreadId}] Loaded {Count} observations for entities", threadId, observationCount);
    }

    /// <summary>
    /// Fetches relations between the entities in the graph.
    /// </summary>
    private async Task FetchRelationsBetweenEntitiesAsync(KnowledgeGraph graph, Guid threadId)
    {
        if (!graph.Entities.Any())
        {
            return;
        }

        _logger.LogInternalInformation("[{ThreadId}] Fetching relations between {Count} entities", threadId, graph.Entities.Count);
        var entityNames = new HashSet<string>(graph.Entities.Select(e => e.Name));
        var relationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphRelationDocument>();
        var relationQuery = relationQueryable.Where(r =>
            r.DocumentType == "relation" &&
            entityNames.Contains(r.From) &&
            entityNames.Contains(r.To));

        using var relationIterator = relationQuery.ToFeedIterator();

        while (relationIterator.HasMoreResults)
        {
            var response = await relationIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                graph.Relations.Add(new Relation
                {
                    From = doc.From,
                    To = doc.To,
                    RelationType = doc.RelationType
                });
            }
        }
        _logger.LogInternalInformation("[{ThreadId}] Found {Count} relations between entities", threadId, graph.Relations.Count);
    }

    private static string[] TokenizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        return query
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim('"', '?', '!', '.', ',', ';', ':', '\''))
            .Where(token => token.Length > 1)
            .ToArray();
    }

    /// <summary>
    /// Extract identifiers from the query using LLM based on the graph schema.
    /// Identifies entity names and patterns that match the naming conventions in the schema.
    /// </summary>
    /// <param name="query">The user's natural language query</param>
    /// <param name="schema">The graph schema containing entity types and their examples</param>
    /// <returns>An array of identifiers extracted from the query</returns>
    private async Task<string[]> ExtractIdentifiersFromQuery(string query, GraphSchema schema)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        var entityTypesWithExamples = schema.EntityTypeOverView
            .Select(e => $"entity type: {e.EntityType}, examples: {string.Join(", ", e.Examples.Take(10))}")
            .ToList();

        var systemPrompt = $"""
            You are an identifier extractor for a knowledge graph system. Your task is to extract the core identifying values (IDs, version numbers, GUID, unique codes) from user queries, NOT the full entity names.
            The extracted identifiers will be used to for exact match searches in the knowledge graph. So the identifiers must be precise, unique.

            ---Goal---
            Extract the unique identifier portion from entity references in the query. The identifier is the distinguishing part that uniquely identifies an entity instance, such as numeric IDs, GUID, version numbers, or unique codes.

            ---Graph Schema---
            Entity Types with Examples:
            {string.Join("\n", entityTypesWithExamples.Select(e => $"- {e}"))}

            ---Instructions---
            1. **Output Format**: Return ONLY a raw JSON array of strings. No markdown, no code blocks, no explanations. Example: ["identifier1", "identifier2"]
            2. **Extract Core Identifiers Only**: Extract only the unique identifying portion, NOT the entity type prefix.
               - "BuildRun-140000765" or "Build Run 140000765" → extract "140000765"
               - "Release 25.12.0" or "Release-25.12.0" → extract "25.12.0"
               - "Pod-abc123" or "pod abc123" → extract "abc123"
            3. **Version Numbers**: Extract version numbers like "25.12.0", "1.2.3", etc. as identifiers.
            4. **Numeric IDs**: Extract standalone numbers that appear to be IDs only if they are long enough (at least 5 digits). Short numbers like "123", "42", "500" are too generic and should be skipped.
            5. **Alphanumeric Codes**: Extract unique codes like "abc123", "xyz-789" when they identify specific entities.
            6. **Skip Entity Type Names**: Do NOT include entity type prefixes like "BuildRun", "Release", "Pod" in the output.
            7. **Skip Generic Terms**: Do not extract generic words like "error", "issue", "problem", "status", "all", "list", etc.
            8. **Skip Short Numeric IDs**: Do not extract short numbers (fewer than 5 digits) as they are likely generic values (e.g., error codes, counts, HTTP status codes).
            9. **Empty Results**: Return [] if no specific identifiers can be extracted.

            ---Examples---
            <example>
            Query: "What happened to build run 140000765?"
            Schema: entity type "BuildRun", examples: BuildRun-67890, BuildRun-11111
            Output: ["140000765"]
            </example>

            <example>
            Query: "Show me BuildRun-140000765"
            Schema: entity type "BuildRun", examples: BuildRun-67890, BuildRun-11111
            Output: ["140000765"]
            </example>

            <example>
            Query: "What's in Release 25.12.0?"
            Schema: entity type "Release", examples: Release-25.11.0, Release-25.10.0
            Output: ["25.12.0"]
            </example>

            <example>
            Query: "Compare Release-25.12.0 and Release-25.11.0"
            Schema: entity type "Release", examples: Release-25.11.0, Release-25.10.0
            Output: ["25.12.0", "25.11.0"]
            </example>

            <example>
            Query: "Check pod abc123 and pod def456"
            Schema: entity type "Pod", examples: Pod-xyz789, Pod-uvw000
            Output: ["abc123", "def456"]
            </example>

            <example>
            Query: "Find incidents related to cluster orangeocean-d8145f39"
            Schema: entity type "Cluster", examples: Cluster-bluewhale-12345, Cluster-redtiger-67890
            Output: ["orangeocean-d8145f39"]
            </example>

            <example>
            Query: "What happened with resource a1b2c3d4-e5f6-7890-abcd-ef1234567890?"
            Schema: entity type "Resource", examples: Resource-f47ac10b-58cc-4372-a567-0e02b2c3d479
            Output: ["a1b2c3d4-e5f6-7890-abcd-ef1234567890"]
            </example>

            <example>
            Query: "What's the status of all releases?"
            Schema: entity type "Release", examples: Release-25.11.0, Release-25.10.0
            Output: []
            </example>
            """;

        var userMessage = $"Extract identifiers from this query: {query}";

        var options = new ChatOptions
        {
            Temperature = 0.1f
        };

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userMessage)
        };

        try
        {
            var response = await _chatClientProvider.ReasoningFastModel.GetResponseAsync<string[]>(messages, options);
            var identifiers = response.Result ?? Array.Empty<string>();

            _logger.LogInternalInformation("LLM extracted {Count} identifiers from query: {Identifiers}",
                identifiers.Length, string.Join(", ", identifiers));

            return identifiers;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to extract identifiers with LLM, returning empty array");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Tokenizes a query using LLM to extract the most relevant search tokens based on the graph schema.
    /// Returns up to 5 tokens that are optimized for knowledge graph search.
    /// </summary>
    /// <param name="query">The user's natural language query</param>
    /// <param name="schema">The graph schema containing entity types and relation types</param>
    /// <returns>An array of up to 5 tokens optimized for graph search</returns>
    private async Task<string[]> TokenizeQueryWithLLM(string query, GraphSchema schema)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        var entityTypesWithExamples = schema.EntityTypeOverView
            .Select(e => $"entity type: {e.EntityType}, examples: {string.Join(", ", e.Examples.Take(10))}")
            .ToList();
        var relationTypes = schema.RelationTypes;

        var systemPrompt = $"""
            You are a keyword extractor for a knowledge graph search system, which helps users find entities and their relationships in a complex graph database
            based on their natural language queries and the current schema of the knowledge graph.

            ---Goal---
            Extract 0 to 5 keywords from the user query that will match entities in the knowledge graph. Focus on specific entities, proper nouns, technical terms, product names, and concrete items.

            ---Graph Schema---
            Entity Types with Examples:
            {string.Join("\n", entityTypesWithExamples.Select(e => $"- {e}"))}

            Relation Types: {string.Join(", ", relationTypes)}

            ---Instructions---
            1. **Output Format**: Return ONLY a raw JSON array of 0-5 strings. No markdown, no code blocks, no explanations. Example: ["keyword1", "keyword2"]
            2. **Source Fidelity**: Only extract keywords explicitly present or directly derivable from the query. Never invent entities.
            3. **Phrase Preservation**: Keep multi-word concepts together. "Apple Inc." stays as one keyword, not split into "Apple" and "Inc."
            4. **Match Schema Patterns**: Transform keywords to match entity naming conventions in the schema.
               - If schema has "BuildRun-1234" pattern and query mentions "build run 5678", extract "BuildRun-5678"
               - If schema has "ServiceAuth" and query mentions "auth service", extract "ServiceAuth" (match the known entity)
            5. **Match Casing and Delimiters**: Use the same casing and separators (hyphens, underscores, spaces) as schema examples.
            6. **Skip Generic Terms**: Omit stop words, generic verbs (get, find, show, list, what, how), temporal references (yesterday, last week, today), and vague terms (issue, problem, error without specifics).
            7. **Skip Generic Codes**: HTTP status codes (404, 500), generic error codes, and standalone numbers without entity context are too generic.
            8. **Numbers with Context**: Extract numbers only when combined with an entity type pattern. "Pod-123" is valid; "123" alone is not.
            9. **Typo Tolerance**: If a query term closely resembles a schema example (minor typo), use the correct schema spelling.
            10. **When Uncertain, Omit**: If you're unsure whether a term matches an entity, leave it out. Precision over recall.
            11. **Empty Results**: Return [] for nonsensical, trivial, or overly vague queries (e.g., "hello", "ok", "help me", "show me everything").

            ---Examples---
            <example>
            Query: "What happened to build run 12345 yesterday?"
            Graph Schema: entity type "BuildRun", examples: BuildRun-67890, BuildRun-11111 (other entity types omitted for brevity)
            Thinking: The query mentions "build run 12345". The schema shows "BuildRun-" prefix pattern. I transform to match: "BuildRun-12345". "yesterday" is temporal, skip it.
            Output: ["BuildRun-12345"]
            </example>

            <example>
            Query: "Show me the relationship between ServiceA and ServiceB"
            Graph Schema: entity type "Service", examples: ServiceB, ServiceC (other entity types omitted for brevity)
            Thinking: The query mentions "ServiceA" and "ServiceB". Both match the Service entity naming pattern (PascalCase). Extract both.
            Output: ["ServiceA", "ServiceB"]
            </example>

            <example>
            Query: "Why is SeviceX failing?" (typo: SeviceX -> ServiceX)
            Graph Schema: entity type "Service", examples: ServiceX, ServiceY, ServiceZ (other entity types omitted for brevity)
            Thinking: "SeviceX" appears to be a typo of "ServiceX" which exists in the schema examples. Use the correct spelling.
            Output: ["ServiceX"]
            </example>

            <example>
            Query: "Get me error 500 from the service"
            Graph Schema: entity type "Service", examples: ServiceAuth, ServicePayment (other entity types omitted for brevity)
            Thinking: "error 500" is a generic HTTP status code, not an entity. "the service" is too vague - no specific service named. Nothing specific to extract.
            Output: []
            </example>

            <example>
            Query: "Show me all deployments"
            Graph Schema: entity type "Deployment", examples: Deployment-001, Deployment-002 (other entity types omitted for brevity)
            Thinking: The query asks for "all deployments" but doesn't specify which ones. Should return "Deployment" because it's a valid entity type in the schema and there're entity names starting with "Deployment-".
            Output: ["Deployment"]
            </example>

            <example>
            Query: "What errors occurred in Pod-abc123 and Container-xyz789?"
            Graph Schema: entity type "Pod", examples: Pod-abc123, Pod-def456; entity type "Container", examples: Container-xyz789, Container-uvw000 (other entity types omitted for brevity)
            Thinking: Two specific entities mentioned: "Pod-abc123" and "Container-xyz789". Both match schema patterns exactly.
            Output: ["Pod-abc123", "Container-xyz789"]
            </example>

            <example>
            Query: "What's wrong with ServiceAuth's pod?"
            Graph Schema: entity type "Service", examples: ServiceAuth, ServicePayment; entity type "Pod", examples: Pod-auth-001, Pod-payment-001 (other entity types omitted for brevity)
            Thinking: "ServiceAuth" is explicitly mentioned and matches schema. "pod" is generic without a specific ID. Extract only the specific entity.
            Output: ["ServiceAuth"]
            </example>

            <example>
            Query: "Get deployment status for 25.1.0"
            Graph Schema: entity type: Build, examples: 25.11.3, 25.11.3#Nov, 25.11.5, 25.11.5#2, 25.12.0, AAPT-Antares-ContainerApps-Official 2025-12-18 25.12.139.0
            Thinking: The entity type "Build" has examples that are version numbers. "25.1.0" matches the version number pattern in the schema examples. Extract it.
            Output: ["25.1.0"]
            </example>

            <example>
            Query: "hello"
            Graph Schema: omitted for brevity
            Thinking: The query is a generic greeting with no meaningful keywords to extract.
            Output: []
            </example>
            """;

        var userMessage = $"Extract keywords from this query: {query}";

        var options = new ChatOptions
        {
            Temperature = 0.1f
        };

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userMessage)
        };

        try
        {
            var response = await _chatClientProvider.ReasoningFastModel.GetResponseAsync<string[]>(messages, options);
            var tokens = response.Result ?? Array.Empty<string>();

            // Ensure we return at most 5 tokens
            tokens = tokens.Take(5).ToArray();

            _logger.LogInternalInformation("LLM tokenized query into {Count} tokens: {Tokens}", tokens.Length, string.Join(", ", tokens));

            return tokens;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to tokenize query with LLM, falling back to simple tokenization");
            // Fall back to simple tokenization if LLM fails
            return TokenizeQuery(query).Take(5).ToArray();
        }
    }

    public async Task<KnowledgeGraph> OpenNodesAsync(List<string> names)
    {
        var graph = new KnowledgeGraph();
        var entityIdToEntityMap = new Dictionary<string, Entity>();

        // Fetch specified entities
        foreach (var name in names)
        {
            // Find entity by name using LINQ
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>()
                .Where(e => e.DocumentType == "entity" && e.Name == name)
                .Select(e => new { e.Id, e.Name, e.EntityType });
            var query = queryable;

            using var iterator = query.ToFeedIterator();

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var doc = response.FirstOrDefault();

                if (doc != null)
                {
                    var entity = new Entity { Name = doc.Name, EntityType = doc.EntityType };
                    graph.Entities.Add(entity);
                    entityIdToEntityMap[doc.Id] = entity;
                }
                else
                {
                    _logger.LogInternalWarning("Entity {Name} not found", name);
                }
            }
        }

        // Fetch all observations for the entities
        if (entityIdToEntityMap.Any())
        {
            var entityIds = entityIdToEntityMap.Keys.ToList();
            var observationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>()
                .Where(o => o.DocumentType == "observation" && entityIds.Contains(o.EntityId))
                .Select(o => new { o.EntityId, o.Content });
            var observationQuery = observationQueryable;

            using var observationIterator = observationQuery.ToFeedIterator();
            while (observationIterator.HasMoreResults)
            {
                var response = await observationIterator.ReadNextAsync();
                foreach (var obs in response)
                {
                    if (entityIdToEntityMap.TryGetValue(obs.EntityId, out var entity))
                    {
                        entity.Observations.Add(obs.Content);
                    }
                }
            }
        }

        // Get relations between found entities using LINQ
        if (graph.Entities.Any())
        {
            var relationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphRelationDocument>();
            var relationQuery = relationQueryable.Where(r =>
                r.PartitionKey == "relation" &&
                names.Contains(r.From) &&
                names.Contains(r.To));

            using var relationIterator = relationQuery.ToFeedIterator();

            while (relationIterator.HasMoreResults)
            {
                var response = await relationIterator.ReadNextAsync();
                foreach (var doc in response)
                {
                    graph.Relations.Add(new Relation
                    {
                        From = doc.From,
                        To = doc.To,
                        RelationType = doc.RelationType
                    });
                }
            }
        }

        return graph;
    }

    public async Task<GraphSchema> GetGraphSchemaAsync()
    {
        var overview = new GraphSchema();

        // Get distinct entity types
        var distinctEntityTypesSql = @"
            SELECT DISTINCT VALUE c.entityType
            FROM c
            WHERE c.partitionKey = 'entity'
              AND c.documentType = 'entity'";

        var entityTypeQuery = new QueryDefinition(distinctEntityTypesSql);
        var entityTypes = new List<string>();

        using var entityTypeIterator = _container.GetItemQueryIterator<string>(entityTypeQuery);
        while (entityTypeIterator.HasMoreResults)
        {
            var response = await entityTypeIterator.ReadNextAsync();
            entityTypes.AddRange(response);
        }

        // For each entity type, get up to 10 example names
        foreach (var entityType in entityTypes)
        {
            var examplesSql = @"
                SELECT TOP 10 VALUE c.name
                FROM c
                WHERE c.partitionKey = 'entity'
                  AND c.documentType = 'entity'
                  AND c.entityType = @entityType
                ORDER BY c.name";

            var examplesQuery = new QueryDefinition(examplesSql)
                .WithParameter("@entityType", entityType);

            var examples = new List<string>();
            using var examplesIterator = _container.GetItemQueryIterator<string>(examplesQuery);
            while (examplesIterator.HasMoreResults)
            {
                var response = await examplesIterator.ReadNextAsync();
                examples.AddRange(response);
            }

            overview.EntityTypeOverView.Add(new EntityTypeOverView
            {
                EntityType = entityType,
                Examples = examples
            });
        }

        // Get distinct relation types using SQL
        var distinctRelationTypesSql = @"
            SELECT DISTINCT VALUE c.relationType
            FROM c
            WHERE c.documentType = 'relation'";

        var relationTypeQuery = new QueryDefinition(distinctRelationTypesSql);
        using var relationTypeIterator = _container.GetItemQueryIterator<string>(relationTypeQuery);
        while (relationTypeIterator.HasMoreResults)
        {
            var response = await relationTypeIterator.ReadNextAsync();
            overview.RelationTypes.AddRange(response);
        }

        _logger.LogInternalInformation("Graph overview: {EntityTypeCount} entity types, {RelationTypeCount} relation types",
            overview.EntityTypeOverView.Count, overview.RelationTypes.Count);

        return overview;
    }
}
