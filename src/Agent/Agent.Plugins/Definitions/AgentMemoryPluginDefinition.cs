// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.DataConnectors;
using Agent.Core.Models.Api.v1;
using Agent.Data.AgentMemory;
using Agent.Plugins.DataConnectors.Documentation;
using Agent.Plugins.DataConnectors.TSG;
using Agent.Core.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class AgentMemoryPluginDefinition(IAgentMemoryClient agentMemoryClient, AgentMemorySettings agentMemorySettings, DataConnectorIndex dataConnectorindex, ILogger<AgentMemoryPluginDefinition> logger, IAgentOutboundCommunicationService agentOutboundCommunicationService)
{
    public const string NoRelevantResultsMessage = "No relevant memories, documents, or past incidents found for the current symptoms";
    public const string NoSameResourceIncidentsMessage = "No past incidents found on the same resource";
    public const string NoSimilarSymptomsMessage = "No past incidents found with similar symptoms";
    public const string NoUserMemoriesMessage = "No relevant user memories found";
    public const string NoDocumentsMessage = "No relevant documents found";

    [Description(@"Retrieves knowledge from past memories to assist with current incident resolution")]
    public async Task<string> SearchMemoryAsync(
        [Description("The Azure resource id of the affected resource experiencing the incident")] string resourceId,
        [Description("A detailed description of the current incident symptoms, error messages, or observed behaviors")] string symptoms
    )
    {
        if (!agentMemorySettings.Enabled)
        {
            return "Agent memory is disabled, disregard this method call from the agent's context";
        }

        var trajectorySearch = SearchTrajectoriesAsync(resourceId, symptoms);
        var userMemorySearch = SearchUserMemoryAsync(symptoms);
        var documentSearch = SearchDocumentAsync(symptoms);
        await Task.WhenAll(trajectorySearch, userMemorySearch, documentSearch);
        var trajectories = await trajectorySearch;
        var userMemories = await userMemorySearch;
        var documents = await documentSearch;

        var result = BuildMemoryResponse(
            documents: documents,
            userMemories: userMemories,
            trajectories: trajectories
        );

        // Push the memory search results to the agent chat interface
        var displayMessage = new ChatMessage(ChatRole.Tool, result);
        await PushMemoryResultToChat(displayMessage, documents, userMemories, trajectories, resourceId, symptoms);

        return result;
    }

    private string BuildMemoryResponse(
        List<string> documents,
        List<string> userMemories,
        TrajectorySearchResult trajectories)
    {
        var threadId = ToolStatic.AsyncLocalThreadId.Value;
        var sb = new StringBuilder();

        if (trajectories.SameResourceTrajectories.Count > 0)
        {
            logger.LogInternalInformation("[Thread {ThreadId}] Found {Count} past incidents on the same resource",
                threadId, trajectories.SameResourceTrajectories.Count);
            sb.AppendLine("## Similar Past Incidents on the exact Same Resource, which has a high likelihood of helping with the current incident resolution.");
            sb.AppendLine();
            foreach (var trajectory in trajectories.SameResourceTrajectories)
            {
                sb.AppendLine($"### {trajectory.Title}");
                sb.AppendLine($"- **Symptoms:** {trajectory.SymptomsObserved}");
                sb.AppendLine($"- **Steps followed for resolution:** {trajectory.StepsFollowed}");
                sb.AppendLine($"- **Root Cause:** {trajectory.RootCause}");
                sb.AppendLine($"- **Pitfalls to avoid:** {trajectory.Pitfalls}");
                sb.AppendLine();
            }
        }
        else
        {
            logger.LogInternalInformation($"[Thread {{ThreadId}}] {NoSameResourceIncidentsMessage}", threadId);
        }

        if (trajectories.SimilarSymptomsTrajectories.Count > 0)
        {
            logger.LogInternalInformation("[Thread {ThreadId}] Found {Count} past incidents with similar symptoms",
                threadId, trajectories.SimilarSymptomsTrajectories.Count);
            sb.AppendLine("## Past Incidents with Similar Symptoms, which may provide insights into the current incident resolution.");
            sb.AppendLine();
            foreach (var trajectory in trajectories.SimilarSymptomsTrajectories)
            {
                sb.AppendLine($"### {trajectory.Title}");
                sb.AppendLine($"- **Symptoms:** {trajectory.SymptomsObserved}");
                sb.AppendLine($"- **Steps followed for resolution:** {trajectory.StepsFollowed}");
                sb.AppendLine($"- **Root Cause:** {trajectory.RootCause}");
                sb.AppendLine($"- **Pitfalls to avoid:** {trajectory.Pitfalls}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }
        else
        {
            logger.LogInternalInformation($"[Thread {{ThreadId}}] {NoSimilarSymptomsMessage}", threadId);
        }

        if (userMemories.Count > 0)
        {
            logger.LogInternalInformation("[Thread {ThreadId}] Found {Count} relevant user memories",
                threadId, userMemories.Count);
            sb.AppendLine("## Related User Memories");
            sb.AppendLine();
            for (int i = 0; i < userMemories.Count; i++)
            {
                var memory = userMemories[i];
                var truncatedMemory = TruncateText(memory, 300);
                sb.AppendLine($"**Memory {i + 1}:**");
                sb.AppendLine($"> {truncatedMemory}");
                sb.AppendLine();
            }
        }
        else
        {
            logger.LogInternalInformation($"[Thread {{ThreadId}}] {NoUserMemoriesMessage}", threadId);
        }

        if (documents.Count > 0)
        {
            logger.LogInternalInformation("[Thread {ThreadId}] Found {Count} relevant documents", threadId, documents.Count);
            sb.AppendLine("## Relevant Documentation");
            sb.AppendLine();
            for (int i = 0; i < documents.Count; i++)
            {
                var doc = documents[i];
                var truncatedDoc = TruncateText(doc, 400);
                sb.AppendLine($"**Document {i + 1}:**");
                sb.AppendLine("```");
                sb.AppendLine(truncatedDoc);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }
        else
        {
            logger.LogInternalInformation($"[Thread {{ThreadId}}] {NoDocumentsMessage}", threadId);
        }

        if (sb.Length == 0)
        {
            return NoRelevantResultsMessage;
        }

        logger.LogInternalInformation("[Thread {ThreadId}] Memory response built successfully", threadId);
        return sb.ToString();
    }

    private async Task<List<string>> SearchDocumentAsync(string symptoms)
    {
        if (!agentMemorySettings.DocumentRetrievalEnabled)
        {
            logger.LogInternalInformation("Document retrieval is disabled, skipping search.");
            return [];
        }

        var threadId = ToolStatic.AsyncLocalThreadId.Value;
        var allDocuments = new List<string>();

        // Apply vector similarity threshold directly in the search query for efficiency
        var userDocuments = dataConnectorindex.SearchAsync<UserDocument>(
            query: symptoms,
            filter: string.Empty,
            max: 10,
            vectorSimilarityThreshold: agentMemorySettings.DocumentVectorSimilarityThreshold);

        // Search TSG Documents with the same parameters
        var tsgDocuments = dataConnectorindex.SearchAsync<TsgDocumentMetadata>(
            query: symptoms,
            filter: string.Empty,
            max: 10,
            vectorSimilarityThreshold: agentMemorySettings.DocumentVectorSimilarityThreshold);

        // Collect all UserDocument results first for filtering
        var allUserDocuments = await userDocuments.ToListAsync();

        // Apply additional filtering based on reranker score
        var filteredUserDocuments = allUserDocuments
            .Where(d => d.SearchResult.Document.RerankerScore.HasValue && d.SearchResult.Document.RerankerScore >= agentMemorySettings.MinimumRerankerScoreThreshold)
            .Select(d => d.SearchResult.Document.Chunk)
            .ToList();

        var userDocFilteredCount = allUserDocuments.Count - filteredUserDocuments.Count;
        if (userDocFilteredCount > 0)
        {
            logger.LogInternalInformation(
                "[Thread {ThreadId}] Filtered out {FilteredCount} UserDocuments due to low scores (RerankerScore < {RerankerThreshold})",
                threadId, userDocFilteredCount, agentMemorySettings.MinimumRerankerScoreThreshold);
        }

        allDocuments.AddRange(filteredUserDocuments);

        // Collect all TsgDocumentMetadata results for filtering
        var allTsgDocuments = await tsgDocuments.ToListAsync();

        // Apply additional filtering based on reranker score
        var filteredTsgDocuments = allTsgDocuments
            .Where(d => d.SearchResult.Document.RerankerScore.HasValue && d.SearchResult.Document.RerankerScore >= agentMemorySettings.MinimumRerankerScoreThreshold)
            .Select(d => d.SearchResult.Document.Chunk)
            .ToList();

        var tsgDocFilteredCount = allTsgDocuments.Count - filteredTsgDocuments.Count;
        if (tsgDocFilteredCount > 0)
        {
            logger.LogInternalInformation(
                "[Thread {ThreadId}] Filtered out {FilteredCount} TsgDocuments due to low scores (RerankerScore < {RerankerThreshold})",
                threadId, tsgDocFilteredCount, agentMemorySettings.MinimumRerankerScoreThreshold);
        }

        allDocuments.AddRange(filteredTsgDocuments);

        logger.LogInternalInformation(
            "[Thread {ThreadId}] Retrieved {Count} total documents ({UserDocCount} UserDocuments + {TsgDocCount} TsgDocuments) after all filtering (VectorSimilarity >= {VectorThreshold}, RerankerScore >= {RerankerThreshold})",
            threadId, allDocuments.Count, filteredUserDocuments.Count, filteredTsgDocuments.Count, agentMemorySettings.DocumentVectorSimilarityThreshold, agentMemorySettings.MinimumRerankerScoreThreshold);

        return allDocuments;
    }

    private async Task<List<string>> SearchUserMemoryAsync(string symptoms)
    {
        if (!agentMemorySettings.UserMemoryRetrievalEnabled)
        {
            logger.LogInternalInformation("User memory retrieval is disabled, skipping search.");
            return [];
        }

        var memories = await agentMemoryClient.SearchUserMemoriesAsync(new SearchParams(
            Query: symptoms, K: 5, EnableHybridSearch: true, EnableSemanticSearch: true, ExhaustiveKnn: true, VectorSimilarityThreshold: agentMemorySettings.UserMemoryVectorSimilarityThreshold));
        if (memories.Count == 0)
        {
            return [];
        }

        return memories.Select(m => m.Chunk).ToList();
    }

    private record Trajectory(
        string Id,
        string Title,
        string InitialSymptoms,
        string SymptomsObserved,
        string StepsFollowed,
        string RootCause,
        string Pitfalls
    );

    private record TrajectorySearchResult(
        List<Trajectory> SameResourceTrajectories,
        List<Trajectory> SimilarSymptomsTrajectories
    );

    private async Task<TrajectorySearchResult> SearchTrajectoriesAsync(
        string resourceId,
        string symptoms)
    {
        if (!agentMemorySettings.TrajectoryRetrievalEnabled)
        {
            logger.LogInternalInformation("Trajectory retrieval is disabled, skipping search.");
            return new TrajectorySearchResult([], []);
        }

        // Search with vector similarity thresholds to filter out irrelevant results
        var similarSymptoms = agentMemoryClient.SearchTrajectoriesAsync(new SearchParams(
            Query: symptoms,
            K: 5,
            EnableHybridSearch: true,
            EnableSemanticSearch: true,
            VectorSimilarityThreshold: agentMemorySettings.TrajectoryVectorSimilarityThreshold));
        var pastIncidents = agentMemoryClient.SearchTrajectoriesAsync(new SearchParams(
            Query: resourceId,
            K: 5,
            EnableHybridSearch: true,
            EnableSemanticSearch: true,
            Filter: "resource_ids/any(id: id eq '" + resourceId + "')", // todo: use case insensitive comparison if possible
            VectorSimilarityThreshold: agentMemorySettings.TrajectoryVectorSimilarityThresholdForSameResource
        ));

        var retrieved = await Task.WhenAll(similarSymptoms, pastIncidents);
        var threadId = ToolStatic.AsyncLocalThreadId.Value;

        // Log initial retrieval counts
        logger.LogInternalInformation(
            "[Thread {ThreadId}] Retrieved {SimilarCount} similar symptom trajectories and {SameResourceCount} same resource trajectories before threshold filtering",
            threadId, retrieved[0].Count, retrieved[1].Count);

        // todo: rerank the results, e.g. using Reciprocal Rank Fusion
        if (retrieved[0].Count == 0 && retrieved[1].Count == 0)
        {
            return new TrajectorySearchResult([], []);
        }

        // Additional filtering based on reranker scores
        var sameResourceTrajectories = retrieved[1]
            .Where(x => x.RerankerScore >= agentMemorySettings.MinimumRerankerScoreThreshold)
            .Select(x => new Trajectory(
                Id: x.Id,
                Title: x.Title,
                InitialSymptoms: x.InitialSymptoms,
                SymptomsObserved: x.SymptomsObserved,
                StepsFollowed: x.StepsFollowed,
                RootCause: x.RootCause,
                Pitfalls: x.Pitfalls))
            .ToList();

        // Log filtering results for same resource trajectories
        var sameResourceFiltered = retrieved[1].Count - sameResourceTrajectories.Count;
        if (sameResourceFiltered > 0)
        {
            logger.LogInternalInformation(
                "[Thread {ThreadId}] Filtered out {FilteredCount} same resource trajectories due to low scores (RerankerScore < {RerankerThreshold})",
                threadId, sameResourceFiltered, agentMemorySettings.MinimumRerankerScoreThreshold);
        }

        var similarSymptomsTrajectories = retrieved[0]
            .Where(x => !sameResourceTrajectories.Any(t => t.Id == x.Id)) // avoid duplicates
            .Where(x => x.RerankerScore >= agentMemorySettings.MinimumRerankerScoreThreshold)
            .Select(x => new Trajectory(
                Id: x.Id,
                Title: x.Title,
                InitialSymptoms: x.InitialSymptoms,
                SymptomsObserved: x.SymptomsObserved,
                StepsFollowed: x.StepsFollowed,
                RootCause: x.RootCause,
                Pitfalls: x.Pitfalls))
            .ToList();

        // Log filtering results for similar symptoms trajectories
        var duplicatesRemoved = retrieved[0].Count(x => sameResourceTrajectories.Any(t => t.Id == x.Id));
        var scoreFiltered = retrieved[0].Count - duplicatesRemoved - similarSymptomsTrajectories.Count;
        if (duplicatesRemoved > 0 || scoreFiltered > 0)
        {
            logger.LogInternalInformation(
                "[Thread {ThreadId}] Filtered out {DuplicateCount} duplicate trajectories and {ScoreFilteredCount} low-score trajectories from similar symptoms",
                threadId, duplicatesRemoved, scoreFiltered);
        }

        return new TrajectorySearchResult(
            SameResourceTrajectories: sameResourceTrajectories,
            SimilarSymptomsTrajectories: similarSymptomsTrajectories
        );
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        // Find the last space before the max length to avoid cutting words
        var truncateIndex = text.LastIndexOf(' ', maxLength);
        if (truncateIndex == -1)
            truncateIndex = maxLength;

        return text.Substring(0, truncateIndex).TrimEnd() + "...";
    }

    /// <summary>
    /// Pushes memory search results to the agent chat interface using streaming pattern.
    /// Creates MemorySearchResult and streams it to frontend for real-time rendering.
    /// </summary>
    private async Task PushMemoryResultToChat(ChatMessage msg, List<string> documents, List<string> userMemories, TrajectorySearchResult trajectories, string resourceId, string symptoms)
    {
        var agentTaskId = ToolStatic.AsyncLocalAgentTaskId.Value;
        var threadId = ToolStatic.AsyncLocalThreadId.Value;

        if (agentTaskId.HasValue)
        {
            // Agent task context - use dedicated handler with same content as chat message
            await agentOutboundCommunicationService.HandleAgentTaskMemoryResult(
                threadId,
                msg.Text ?? string.Empty);
        }
        else
        {
            // Normal chat flow - use streaming pattern
            var memorySearchResult = new MemorySearchResult(
                ResourceId: resourceId,
                Symptoms: symptoms,
                SameResourceTrajectories: trajectories.SameResourceTrajectories.Select(t => new TrajectoryResult(
                    Id: t.Id,
                    Title: t.Title,
                    InitialSymptoms: t.InitialSymptoms,
                    SymptomsObserved: t.SymptomsObserved,
                    StepsFollowed: t.StepsFollowed,
                    RootCause: t.RootCause,
                    Pitfalls: t.Pitfalls
                )).ToList(),
                SimilarSymptomsTrajectories: trajectories.SimilarSymptomsTrajectories.Select(t => new TrajectoryResult(
                    Id: t.Id,
                    Title: t.Title,
                    InitialSymptoms: t.InitialSymptoms,
                    SymptomsObserved: t.SymptomsObserved,
                    StepsFollowed: t.StepsFollowed,
                    RootCause: t.RootCause,
                    Pitfalls: t.Pitfalls
                )).ToList(),
                UserMemories: userMemories,
                Documents: documents,
                Timestamp: DateTime.UtcNow,
                TotalResults: trajectories.SameResourceTrajectories.Count + trajectories.SimilarSymptomsTrajectories.Count + userMemories.Count + documents.Count
            );

            // Stream to frontend using same pattern as AzCli - serialize and stream with StreamMessageType
            var jsonString = System.Text.Json.JsonSerializer.Serialize(memorySearchResult);
            await agentOutboundCommunicationService.AppendAgentStreamMessage(
                threadId,
                jsonString,
                Agent.Core.Models.Api.v1.StreamMessageType.MemorySearch
            );
        }
    }
}
