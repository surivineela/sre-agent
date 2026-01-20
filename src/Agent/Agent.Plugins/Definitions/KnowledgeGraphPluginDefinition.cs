// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions;

/// <summary>
/// Plugin definition for knowledge graph memory operations.
/// Provides tools for managing entities, relations, and observations in a persistent knowledge graph.
/// </summary>
[AgentToolPlugin(Category = ToolCategories.KnowledgeBase, EnabledIf = "KNOWLEDGE_GRAPH_MEMORY:Enabled")]
public class KnowledgeGraphPluginDefinition
{
    private readonly IKnowledgeGraphPlugin _plugin;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

    public KnowledgeGraphPluginDefinition(
        IKnowledgeGraphPlugin plugin,
        IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
        _plugin = plugin;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
    }

    [Description("Create multiple new entities in the knowledge graph")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> CreateEntities(
        [Description("An array of entities to create")]
        List<Entity> entities)
    {
        if (entities == null || entities.Count == 0)
        {
            return "No entities provided";
        }

        var result = await _plugin.CreateEntitiesAsync(entities);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Create multiple new relations between entities in the knowledge graph. Relations should be in active voice (e.g., 'manages', 'works_with', 'located_in'). Returns only newly created relations (duplicates are skipped).")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> CreateRelations(
        [Description("An array of relations to create. Each relation must have 'from' (entity name), 'to' (entity name), and 'relationType' (string in active voice).")]
        List<Relation> relations)
    {
        if (relations == null || relations.Count == 0)
        {
            return "No relations provided";
        }

        var result = await _plugin.CreateRelationsAsync(relations);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Add new observations to existing entities in the knowledge graph. Observations are facts or notes about an entity. Only new observations are added (duplicates are skipped).")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> AddObservations(
        [Description("An array of observations to add. Each item must have 'entityName' (string) and 'contents' (array of observation strings).")]
        List<ObservationInput> observations)
    {
        if (observations == null || observations.Count == 0)
        {
            return "No observations provided";
        }

        var result = await _plugin.AddObservationsAsync(observations);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Delete multiple entities and their associated relations from the knowledge graph. This operation also removes all relations where these entities are involved.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> DeleteEntities(
        [Description("An array of entity names to delete.")]
        List<string> entityNames)
    {
        if (entityNames == null || entityNames.Count == 0)
        {
            return "No entity names provided";
        }

        await _plugin.DeleteEntitiesAsync(entityNames);
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = "Entities deleted successfully",
            deletedCount = entityNames.Count
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Delete specific observations from entities in the knowledge graph. The entities themselves are not deleted, only the specified observations are removed.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> DeleteObservations(
        [Description("An array of observation deletions. Each item must have 'entityName' (string) and 'observations' (array of observation strings to delete).")]
        List<ObservationDeletion> deletions)
    {
        if (deletions == null || deletions.Count == 0)
        {
            return "No deletions provided";
        }

        await _plugin.DeleteObservationsAsync(deletions);
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = "Observations deleted successfully"
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Delete multiple relations from the knowledge graph. The entities involved are not deleted, only the relations between them.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> DeleteRelations(
        [Description("An array of relations to delete. Each relation must have 'from' (entity name), 'to' (entity name), and 'relationType' (string).")]
        List<Relation> relations)
    {
        if (relations == null || relations.Count == 0)
        {
            return "No relations provided";
        }

        await _plugin.DeleteRelationsAsync(relations);
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = "Relations deleted successfully",
            deletedCount = relations.Count
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Read the entire knowledge graph, returning all entities and relations. Use this to get a complete view of all stored knowledge.")]
    [AgentTool(ToolMode.Auto, DisableOutputTruncation = true)]
    public async Task<string> ReadGraph()
    {
        var result = await _plugin.ReadGraphAsync();

        // Stream the result to frontend
        var threadId = Core.ToolStatic.AsyncLocalThreadId.Value;
        if (threadId != Guid.Empty)
        {
            await PushKnowledgeGraphResultToChat(threadId, "Knowledge Graph Overview", result);
        }

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Search for nodes in the knowledge graph based on a natural language query string. Returns matching entities and their relations. Prefer to get graph schema first to generate effective queries")]
    [AgentTool(ToolMode.Auto, DisableOutputTruncation = true)]
    public async Task<string> SearchNodes(
        [Description("The natural language question to search the knowledge graph for. e.g. 'Find all employees who work in the Seattle office', 'What's the latest status of ACA deployment xxx in eastus?'")]
        string query,
        [Description("Filter results to a specific entity type. Entity types can be found via GetGraphSchema tool. If empty, no filtering is applied.")]
        string entityType = "",
        [Description("whether to include neighboring nodes in the results. If true, related entities and their relations will be included.")]
        bool includeNeighbors = true)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Query cannot be empty";
        }

        var result = await _plugin.SearchNodesAsync(query, entityType, includeNeighbors);

        // Stream the result to frontend
        var threadId = Core.ToolStatic.AsyncLocalThreadId.Value;
        if (threadId != Guid.Empty)
        {
            await PushKnowledgeGraphResultToChat(threadId, query, result);
        }

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Pushes knowledge graph search results to the agent chat interface using streaming pattern.
    /// Creates KnowledgeGraphSearchResult and streams it to frontend for real-time rendering.
    /// </summary>
    private async Task PushKnowledgeGraphResultToChat(Guid threadId, string query, KnowledgeGraph result)
    {
        var knowledgeGraphSearchResult = new KnowledgeGraphSearchResult(
            Query: query,
            Entities: result.Entities
                .Select(e => new KnowledgeGraphEntity(
                    Name: e.Name,
                    EntityType: e.EntityType,
                    Observations: e.Observations
                ))
                .ToList(),
            Relations: result.Relations
                .Select(r => new KnowledgeGraphRelation(
                    From: r.From,
                    To: r.To,
                    RelationType: r.RelationType
                ))
                .ToList(),
            Timestamp: DateTime.UtcNow,
            TotalEntities: result.Entities.Count,
            TotalRelations: result.Relations.Count
        );

        await _agentOutboundCommunicationService.AppendAgentKnowledgeGraphSearchMessage(
            threadId,
            knowledgeGraphSearchResult
        );
    }

    [Description("Open specific nodes in the knowledge graph by their names. Returns the requested entities and the relations between them. Useful for retrieving specific entities you know exist.")]
    [AgentTool(ToolMode.Auto, DisableOutputTruncation = true)]
    public async Task<string> OpenNodes(
        [Description("An array of entity names to retrieve.")]
        List<string> names)
    {
        if (names == null || names.Count == 0)
        {
            return "No entity names provided";
        }

        var result = await _plugin.OpenNodesAsync(names);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("Get the schema of the knowledge graph, including all entity types with example names and all relation types. Useful for understanding what kinds of entities and relationships exist in the graph before querying.")]
    [AgentTool(ToolMode.Auto, DisableOutputTruncation = true)]
    public async Task<string> GetGraphSchema()
    {
        var result = await _plugin.GetGraphSchemaAsync();
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
