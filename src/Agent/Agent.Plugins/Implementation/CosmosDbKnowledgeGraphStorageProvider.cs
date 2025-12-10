// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
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
    private const int MaxEntitiesInSearchResult = 50;

    public CosmosDbKnowledgeGraphStorageProvider(
        Microsoft.Azure.Cosmos.Container container,
        ILogger<CosmosDbKnowledgeGraphStorageProvider> logger,
        IChatClientProvider chatClientProvider)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _chatClientProvider = chatClientProvider ?? throw new ArgumentNullException(nameof(chatClientProvider));
    }

    public async Task<List<Entity>> CreateEntitiesAsync(List<Entity> entities)
    {
        var newEntities = new List<Entity>();

        foreach (var entity in entities)
        {
            // Check if entity already exists by name
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>();
            var query = queryable.Where(e => e.DocumentType == "entity" && e.Name == entity.Name);

            using var iterator = query.ToFeedIterator();
            bool exists = false;

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                exists = response.Any();
            }

            if (exists)
            {
                // Entity exists, skip it
                _logger.LogInternalInformation("Entity {Name} already exists, skipping", entity.Name);
            }
            else
            {
                // Entity doesn't exist, create it
                var document = entity.ToDocument();
                await _container.CreateItemAsync(document, new PartitionKey(document.PartitionKey));

                // Create observations for this entity
                if (entity.Observations != null && entity.Observations.Any())
                {
                    foreach (var observation in entity.Observations)
                    {
                        var obsDocument = KnowledgeGraphDocumentExtensions.ToObservationDocument(document.Id, observation);
                        await _container.CreateItemAsync(obsDocument, new PartitionKey(obsDocument.PartitionKey));
                    }
                }

                newEntities.Add(entity);
                _logger.LogInternalInformation("Created entity {Name} with {ObservationCount} observations",
                    entity.Name, entity.Observations?.Count ?? 0);
            }
        }

        return newEntities;
    }

    public async Task<List<Relation>> CreateRelationsAsync(List<Relation> relations)
    {
        var newRelations = new List<Relation>();

        foreach (var relation in relations)
        {
            // Check if relation already exists by from/to/type
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphRelationDocument>();
            var query = queryable.Where(r =>
                r.DocumentType == "relation" &&
                r.From == relation.From &&
                r.To == relation.To &&
                r.RelationType == relation.RelationType);

            using var iterator = query.ToFeedIterator();
            bool exists = false;

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                exists = response.Any();
            }

            if (exists)
            {
                // Relation exists, skip it
                _logger.LogInternalInformation("Relation {From}->{To} ({Type}) already exists, skipping",
                    relation.From, relation.To, relation.RelationType);
            }
            else
            {
                // Relation doesn't exist, create it
                var document = relation.ToDocument();
                await _container.CreateItemAsync(document, new PartitionKey(document.PartitionKey));
                newRelations.Add(relation);
                _logger.LogInternalInformation("Created relation {From}->{To} ({Type})",
                    relation.From, relation.To, relation.RelationType);
            }
        }

        return newRelations;
    }

    public async Task<List<ObservationResult>> AddObservationsAsync(List<ObservationInput> observations)
    {
        var results = new List<ObservationResult>();

        foreach (var observation in observations)
        {
            // Find entity by name and get its ID
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>();
            var query = queryable.Where(e => e.DocumentType == "entity" && e.Name == observation.EntityName);

            using var iterator = query.ToFeedIterator();
            KnowledgeGraphEntityDocument? entityDocument = null;

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                entityDocument = response.FirstOrDefault();
            }

            if (entityDocument == null)
            {
                throw new InvalidOperationException($"Entity with name {observation.EntityName} not found");
            }

            // Get existing observations for this entity using its document ID
            var existingObservationsQuery = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>();
            var existingQuery = existingObservationsQuery.Where(o =>
                o.DocumentType == "observation" && o.EntityId == entityDocument.Id);

            var existingContents = new HashSet<string>();
            using var existingIterator = existingQuery.ToFeedIterator();
            while (existingIterator.HasMoreResults)
            {
                var response = await existingIterator.ReadNextAsync();
                foreach (var obs in response)
                {
                    existingContents.Add(obs.Content);
                }
            }

            // Filter out duplicate observations
            var newObservations = observation.Contents
                .Where(content => !existingContents.Contains(content))
                .ToList();

            // Create new observation documents with entity document ID
            foreach (var content in newObservations)
            {
                var obsDocument = KnowledgeGraphDocumentExtensions.ToObservationDocument(entityDocument.Id, content);
                await _container.CreateItemAsync(obsDocument, new PartitionKey(obsDocument.PartitionKey));
            }

            results.Add(new ObservationResult
            {
                EntityName = observation.EntityName,
                AddedObservations = newObservations
            });
        }

        return results;
    }

    public async Task DeleteEntitiesAsync(List<string> entityNames)
    {
        var entityIdsToDelete = new List<string>();

        foreach (var entityName in entityNames)
        {
            // Find entity by name using LINQ
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>();
            var query = queryable.Where(e => e.DocumentType == "entity" && e.Name == entityName);

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
            var observationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>();
            var observationQuery = observationQueryable.Where(o =>
                o.DocumentType == "observation" &&
                entityIdsToDelete.Contains(o.EntityId));

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
            var entityQueryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>();
            var entityQuery = entityQueryable.Where(e => e.DocumentType == "entity" && e.Name == deletion.EntityName);

            using var entityIterator = entityQuery.ToFeedIterator();
            KnowledgeGraphEntityDocument? entityDocument = null;

            if (entityIterator.HasMoreResults)
            {
                var response = await entityIterator.ReadNextAsync();
                entityDocument = response.FirstOrDefault();
            }

            if (entityDocument == null)
            {
                _logger.LogInternalWarning("Entity {Name} not found, skipping observation deletion", deletion.EntityName);
                continue;
            }

            // Find observations for this entity using its document ID
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>();
            var query = queryable.Where(o =>
                o.DocumentType == "observation" &&
                o.EntityId == entityDocument.Id);

            using var iterator = query.ToFeedIterator();
            var observationsToDelete = new List<KnowledgeGraphObservationDocument>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var obs in response)
                {
                    if (deletion.Observations.Contains(obs.Content))
                    {
                        observationsToDelete.Add(obs);
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
        var entityQueryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>();
        var entityQuery = entityQueryable.Where(e => e.DocumentType == "entity");

        var entityIdToEntityMap = new Dictionary<string, Entity>();
        using var entityIterator = entityQuery.ToFeedIterator();
        while (entityIterator.HasMoreResults)
        {
            var response = await entityIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                var entity = doc.ToEntity();
                graph.Entities.Add(entity);
                entityIdToEntityMap[doc.Id] = entity;
            }
        }

        // Read all observations and populate entities
        if (entityIdToEntityMap.Any())
        {
            var observationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>();
            var observationQuery = observationQueryable.Where(o => o.DocumentType == "observation");

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

    public async Task<KnowledgeGraph> SearchNodesAsync(string query)
    {
        var graph = new KnowledgeGraph();

        // Tokenize the query into individual words
        var tokens = await TokenizeQueryWithLLM(query, await GetGraphSchemaAsync());
        _logger.LogInternalInformation("Searching knowledge graph with tokens: {Tokens}", string.Join(", ", tokens));

        // Track entity documents by ID to avoid duplicates, maintaining order
        var entityDocumentsById = new Dictionary<string, KnowledgeGraphEntityDocument>();

        // Search entities using Cosmos DB fulltext search with SQL and ORDER BY RANK for relevance sorting
        _logger.LogInternalInformation("Searching entities by fulltext search on name and entityType");

        // Build parameter placeholders for tokens
        var tokenParams = string.Join(", ", tokens.Select((_, i) => $"@token{i}"));
        var entitySql = $@"
            SELECT c.id, c.documentType, c.partitionKey, c.name, c.entityType
            FROM c
            WHERE c.documentType = 'entity'
              AND (FULLTEXTCONTAINSANY(c.name, {tokenParams}) OR FULLTEXTCONTAINSANY(c.entityType, {tokenParams}))
            ORDER BY RANK FULLTEXTSCORE(c.name, {tokenParams})";

        var entityQueryDefinition = new QueryDefinition(entitySql);
        for (int i = 0; i < tokens.Length; i++)
        {
            entityQueryDefinition.WithParameter($"@token{i}", tokens[i]);
        }

        using var entityIterator = _container.GetItemQueryIterator<KnowledgeGraphEntityDocument>(entityQueryDefinition);
        while (entityIterator.HasMoreResults)
        {
            var response = await entityIterator.ReadNextAsync();
            foreach (var doc in response)
            {
                if (!entityDocumentsById.ContainsKey(doc.Id))
                {
                    entityDocumentsById[doc.Id] = doc;
                }
            }
        }
        _logger.LogInternalInformation("Found {Count} entities matching fulltext search", entityDocumentsById.Count);

        // Search observations using Cosmos DB fulltext search with ORDER BY RANK
        _logger.LogInternalInformation("Searching observations by fulltext search on content");
        var observationSql = $@"
            SELECT c.id, c.documentType, c.partitionKey, c.entityId, c.content
            FROM c
            WHERE c.documentType = 'observation'
              AND FULLTEXTCONTAINSANY(c.content, {tokenParams})
            ORDER BY RANK FULLTEXTSCORE(c.content, {tokenParams})";

        var observationQueryDefinition = new QueryDefinition(observationSql);
        for (int i = 0; i < tokens.Length; i++)
        {
            observationQueryDefinition.WithParameter($"@token{i}", tokens[i]);
        }

        using var observationIterator = _container.GetItemQueryIterator<KnowledgeGraphObservationDocument>(observationQueryDefinition);
        var entityIdsFromObservations = new HashSet<string>();
        while (observationIterator.HasMoreResults)
        {
            var response = await observationIterator.ReadNextAsync();
            foreach (var obs in response)
            {
                entityIdsFromObservations.Add(obs.EntityId);
            }
        }
        _logger.LogInternalInformation("Found {Count} entities with matching observations", entityIdsFromObservations.Count);

        // Fetch entities that matched via observations but weren't already in results
        if (entityIdsFromObservations.Any())
        {
            var missingEntityIds = entityIdsFromObservations
                .Where(id => !entityDocumentsById.ContainsKey(id))
                .ToList();

            if (missingEntityIds.Any())
            {
                _logger.LogInternalInformation("Fetching {Count} additional entities from observation matches", missingEntityIds.Count);
                var additionalEntityQueryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>();
                var additionalEntityQuery = additionalEntityQueryable.Where(e =>
                    e.DocumentType == "entity" &&
                    missingEntityIds.Contains(e.Id));

                using var additionalIterator = additionalEntityQuery.ToFeedIterator();
                while (additionalIterator.HasMoreResults)
                {
                    var response = await additionalIterator.ReadNextAsync();
                    foreach (var doc in response)
                    {
                        if (!entityDocumentsById.ContainsKey(doc.Id))
                        {
                            entityDocumentsById[doc.Id] = doc;
                        }
                    }
                }
            }
        }

        // Fetch direct neighbors of the found entities
        if (entityDocumentsById.Any())
        {
            var foundEntityNames = entityDocumentsById.Values.Select(e => e.Name).ToHashSet();
            _logger.LogInternalInformation("Finding direct neighbors of {Count} entities", foundEntityNames.Count);

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

            if (neighborNames.Any())
            {
                _logger.LogInternalInformation("Fetching {Count} neighbor entities", neighborNames.Count);
                var neighborEntityQueryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>();
                var neighborEntityQuery = neighborEntityQueryable.Where(e =>
                    e.DocumentType == "entity" &&
                    neighborNames.Contains(e.Name));

                using var neighborIterator = neighborEntityQuery.ToFeedIterator();
                while (neighborIterator.HasMoreResults)
                {
                    var response = await neighborIterator.ReadNextAsync();
                    foreach (var doc in response)
                    {
                        if (!entityDocumentsById.ContainsKey(doc.Id))
                        {
                            entityDocumentsById[doc.Id] = doc;
                        }
                    }
                }
                _logger.LogInternalInformation("Total entities after adding neighbors: {Count}", entityDocumentsById.Count);
            }
        }

        // Limit results to max entities, preserving order
        var entityIdToEntityMap = new Dictionary<string, Entity>();
        var count = 0;

        foreach (var kvp in entityDocumentsById)
        {
            if (count >= MaxEntitiesInSearchResult)
                break;

            var doc = kvp.Value;
            var entity = doc.ToEntity();
            graph.Entities.Add(entity);
            entityIdToEntityMap[doc.Id] = entity;
            count++;
        }
        _logger.LogInternalInformation("Returning {Count} entities (limited to {MaxCount})", graph.Entities.Count, MaxEntitiesInSearchResult);

        // Fetch all observations for the entities in the result
        if (entityIdToEntityMap.Any())
        {
            _logger.LogInternalInformation("Fetching observations for {Count} entities", entityIdToEntityMap.Count);
            var entityIds = entityIdToEntityMap.Keys.ToList();
            var allObservationsQueryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>();
            var allObservationsQuery = allObservationsQueryable.Where(o =>
                o.DocumentType == "observation" &&
                entityIds.Contains(o.EntityId));

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
            _logger.LogInternalInformation("Loaded {Count} observations for entities", observationCount);
        }

        // Get relations between found entities using entity names
        if (graph.Entities.Any())
        {
            _logger.LogInternalInformation("Fetching relations between {Count} entities", graph.Entities.Count);
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
            _logger.LogInternalInformation("Found {Count} relations between entities", graph.Relations.Count);
        }

        _logger.LogInternalInformation("Search completed: {EntityCount} entities, {RelationCount} relations",
            graph.Entities.Count, graph.Relations.Count);
        return graph;
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
            .Select(e => $"{e.EntityType}: {string.Join(", ", e.Examples.Take(10))}")
            .ToList();
        var relationTypes = schema.RelationTypes;

        var systemPrompt = $"""
            You are a query tokenizer for a knowledge graph search system.
            Your task is to generate the most relevant search tokens from a user query.

            The knowledge graph has the following schema:
            Entity Types and Examples:
            {string.Join("\n", entityTypesWithExamples.Select(e => $"- {e}"))}

            Relation Types: {string.Join(", ", relationTypes)}

            Rules:
            1. Generate 1 to 5 tokens that are most likely to match entities, entity types, or observations in the graph.
            2. Prefer exact entity names or entity types from the schema when possible.
            3. Include technical terms, names, and identifiers.
            4. Remove stop words, articles, and generic verbs.
            5. Rewrite tokens from their original form if necessary to better match entity names/types.
            """;

        var userMessage = $"Generate search tokens from this query: {query}";

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
            var queryable = _container.GetItemLinqQueryable<KnowledgeGraphEntityDocument>();
            var query = queryable.Where(e => e.DocumentType == "entity" && e.Name == name);

            using var iterator = query.ToFeedIterator();

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var doc = response.FirstOrDefault();

                if (doc != null)
                {
                    var entity = doc.ToEntity();
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
            var observationQueryable = _container.GetItemLinqQueryable<KnowledgeGraphObservationDocument>();
            var observationQuery = observationQueryable.Where(o =>
                o.DocumentType == "observation" &&
                entityIds.Contains(o.EntityId));

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
