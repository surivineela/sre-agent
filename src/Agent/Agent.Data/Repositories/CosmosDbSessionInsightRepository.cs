// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models;
using Agent.Data.AgentMemory;
using Agent.Data.DataModels;
using Agent.Data.Helpers;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

public class CosmosDbSessionInsightRepository : ISessionInsightRepository
{
    private readonly ILogger<CosmosDbSessionInsightRepository> _logger;
    private readonly string _databaseName;
    private readonly CosmosClient _client;
    private readonly ISearchIndexService _searchIndexService;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public CosmosDbSessionInsightRepository(
        CosmosClient cosmosClient,
        string databaseName,
        ILogger<CosmosDbSessionInsightRepository> logger,
        ISearchIndexService searchIndexService,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _logger = logger;
        _databaseName = databaseName;
        _client = cosmosClient;
        _searchIndexService = searchIndexService;
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task<SessionInsightDocument?> GetSessionInsightAsync(string threadId)
    {
        _logger.LogInternalInformation("Getting most recent session insight for thread: {ThreadId}", threadId);

        try
        {
            // Use QueryDefinition with SQL query instead of LINQ to ensure proper deserialization with camelCase
            // Get the most recent session insight for the thread (ordered by generatedTimestamp descending)
            var query = new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.threadId = @threadId AND c.documentType = @docType ORDER BY c.generatedTimestamp DESC")
                .WithParameter("@threadId", threadId)
                .WithParameter("@docType", "SessionInsight");

            var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
                .GetItemQueryIterator<SessionInsightDocument>(query);

            var response = await iterator.ReadNextAsync();
            var insight = response.FirstOrDefault();

            if (insight != null)
            {
                _logger.LogInternalInformation("Found session insight {InsightId} (generated: {Generated}) for thread: {ThreadId}",
                    insight.Id, insight.GeneratedTimestamp, threadId);
                return insight;
            }

            _logger.LogInternalWarning("Session insight not found for thread: {ThreadId}", threadId);
            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Session insight not found for thread: {ThreadId}", threadId);
            return null;
        }
    }

    public async Task<SessionInsightDocument?> GetSessionInsightByIdAsync(string insightId)
    {
        _logger.LogInternalInformation("Getting session insight by ID: {InsightId}", insightId);

        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", insightId);

            var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
                .GetItemQueryIterator<SessionInsightDocument>(query);

            SessionInsightDocument? insight = null;
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                insight = response.FirstOrDefault();
                if (insight != null)
                {
                    _logger.LogInternalInformation("Found session insight {InsightId} for thread: {ThreadId}",
                        insight.Id, insight.ThreadId);
                    break;
                }
            }

            if (insight == null)
            {
                _logger.LogInternalWarning("Session insight not found with ID: {InsightId}", insightId);
            }

            return insight;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Session insight not found with ID: {InsightId}", insightId);
            return null;
        }
    }

    public async Task<List<SessionInsightDocument>> GetSessionInsightsAsync(int skip = 0, int take = 50)
    {
        _logger.LogInternalInformation("Fetching session insights with skip: {Skip}, take: {Take}", skip, take);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @docType ORDER BY c.generatedTimestamp DESC OFFSET @skip LIMIT @take")
            .WithParameter("@docType", "SessionInsight")
            .WithParameter("@skip", skip)
            .WithParameter("@take", take);

        var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
            .GetItemQueryIterator<SessionInsightDocument>(query);

        var insights = new List<SessionInsightDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            insights.AddRange(response);
        }

        _logger.LogInternalInformation("Fetched {Count} session insights", insights.Count);
        return insights;
    }

    public async Task<List<SessionInsightDocument>> GetSessionInsightsByThreadIdAsync(string threadId)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.threadId = @threadId AND c.documentType = @docType ORDER BY c.generatedTimestamp DESC")
                .WithParameter("@threadId", threadId)
                .WithParameter("@docType", "SessionInsight");

            var insights = new List<SessionInsightDocument>();
            var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
                .GetItemQueryIterator<SessionInsightDocument>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                insights.AddRange(response);
            }

            _logger.LogInternalInformation("Found {Count} session insights for thread {ThreadId}", insights.Count, threadId);
            return insights;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting session insights for thread {ThreadId}", threadId);
            return new List<SessionInsightDocument>();
        }
    }

    public async Task<List<SessionInsightDocument>> GetInvestigationInsightsAsync(int skip = 0, int take = 50)
    {
        _logger.LogInternalInformation("Fetching investigation insights with skip: {Skip}, take: {Take}", skip, take);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.documentType = @docType AND c.isInvestigationThread = @isInvestigation ORDER BY c.generatedTimestamp DESC OFFSET @skip LIMIT @take")
            .WithParameter("@docType", "SessionInsight")
            .WithParameter("@isInvestigation", true)
            .WithParameter("@skip", skip)
            .WithParameter("@take", take);

        var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
            .GetItemQueryIterator<SessionInsightDocument>(query);

        var insights = new List<SessionInsightDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            insights.AddRange(response);
        }

        _logger.LogInternalInformation("Fetched {Count} investigation insights", insights.Count);
        return insights;
    }

    public async Task<SessionInsightDocument> UpsertSessionInsightAsync(SessionInsightDocument insight)
    {
        _logger.LogInternalInformation("Upserting session insight - Id: {InsightId}, ThreadId: {ThreadId}, PartitionKey: {PartitionKey}",
            insight.Id, insight.ThreadId, insight.PartitionKey);

        var response = await _client.GetContainer<SessionInsightDocument>(_databaseName)
            .UpsertItemAsync(
                insight,
                new PartitionKey(insight.PartitionKey));

        _logger.LogInternalInformation(
            "Upserted session insight - Id: {InsightId}, ThreadId: {ThreadId}, RU consumed: {RU}",
            response.Resource.Id,
            insight.ThreadId,
            response.RequestCharge);

        return response.Resource;
    }

    public async Task<bool> AddFeedbackToInsightAsync(string insightId, InsightFeedback feedback)
    {
        _logger.LogInternalInformation("Adding feedback to session insight: {InsightId}", insightId);

        try
        {
            var insight = await GetSessionInsightByIdAsync(insightId);
            if (insight == null)
            {
                _logger.LogInternalWarning("Cannot add feedback - session insight not found: {InsightId}", insightId);
                return false;
            }

            // Validate the insight has required data
            if (string.IsNullOrWhiteSpace(insight.Title) || insight.GeneratedTimestamp == default)
            {
                _logger.LogInternalWarning("Cannot add feedback - session insight {InsightId} is malformed (missing title or timestamp)", insightId);
                return false;
            }

            // Update the SessionInsight document with feedback
            var feedbackList = insight.Feedback ?? new List<InsightFeedback>();
            feedbackList.Add(feedback);
            var insightWithFeedback = insight with { Feedback = feedbackList };
            await UpsertSessionInsightAsync(insightWithFeedback);

            _logger.LogInternalInformation("Added feedback to session insight {InsightId}", insightId);

            // Process extracted feedback knowledge asynchronously to not block.
            if (feedback.ExtractedKnowledge != null)
            {
                _ = Task.Run(async () => await ProcessExtractedFeedbackAsync(insight.ThreadId, feedback));
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error adding feedback to session insight: {InsightId}", insightId);
            return false;
        }
    }

    public async Task<bool> DeleteSessionInsightAsync(string insightId)
    {
        _logger.LogInternalInformation("Deleting session insight by ID: {InsightId}", insightId);

        try
        {
            // Get the insight to retrieve its partition key
            var insight = await GetSessionInsightByIdAsync(insightId);
            if (insight == null)
            {
                _logger.LogInternalWarning("Session insight not found for deletion, ID: {InsightId}", insightId);
                return false;
            }

            await _client.GetContainer<SessionInsightDocument>(_databaseName)
                .DeleteItemAsync<SessionInsightDocument>(
                    insight.Id,
                    new PartitionKey(insight.PartitionKey));

            _logger.LogInternalInformation("Deleted session insight {InsightId} (thread: {ThreadId})", insight.Id, insight.ThreadId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Session insight not found for deletion, ID: {InsightId}", insightId);
            return false;
        }
    }

    public async Task<int> DeleteSessionInsightsByThreadIdAsync(string threadId)
    {
        _logger.LogInternalInformation("Deleting all session insights for thread: {ThreadId}", threadId);

        try
        {
            var insights = await GetSessionInsightsByThreadIdAsync(threadId);

            if (!insights.Any())
            {
                _logger.LogInternalInformation("No session insights found for thread: {ThreadId}", threadId);
                return 0;
            }

            var deleteCount = 0;
            foreach (var insight in insights)
            {
                try
                {
                    await _client.GetContainer<SessionInsightDocument>(_databaseName)
                        .DeleteItemAsync<SessionInsightDocument>(
                            insight.Id,
                            new PartitionKey(insight.PartitionKey));
                    deleteCount++;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInternalWarning("Session insight {InsightId} already deleted", insight.Id);
                }
            }

            _logger.LogInternalInformation("Deleted {Count} session insight(s) for thread: {ThreadId}", deleteCount, threadId);
            return deleteCount;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error deleting session insights for thread: {ThreadId}", threadId);
            return 0;
        }
    }

    public async Task<bool> SessionInsightExistsAsync(string threadId)
    {
        var insight = await GetSessionInsightAsync(threadId);
        return insight != null;
    }

    public async Task<List<SessionInsightDocument>> GetSessionInsightsByResourceAsync(
        string resourceId,
        int skip = 0,
        int take = 50)
    {
        _logger.LogInternalInformation("Fetching session insights involving resource: {ResourceId}", resourceId);

        // Use ARRAY_CONTAINS to search within the resourcesInvolved array
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.id = @id ORDER BY c.generatedTimestamp DESC OFFSET @skip LIMIT @take")
            .WithParameter("@id", resourceId)
            .WithParameter("@skip", skip)
            .WithParameter("@take", take);

        var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
            .GetItemQueryIterator<SessionInsightDocument>(query);

        var insights = new List<SessionInsightDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            insights.AddRange(response);
        }

        _logger.LogInternalInformation("Fetched {Count} session insights for resource: {ResourceId}", insights.Count, resourceId);
        return insights;
    }

    /// <summary>
    /// Processes extracted feedback knowledge by adding it to an existing trajectory or indexing it separately.
    /// </summary>
    private async Task ProcessExtractedFeedbackAsync(string threadId, InsightFeedback feedback)
    {
        try
        {
            if (feedback.ExtractedKnowledge == null || string.IsNullOrWhiteSpace(feedback.Comment))
            {
                _logger.LogInternalWarning("Skipping feedback processing - no extracted knowledge or comment");
                return;
            }

            _logger.LogInternalInformation(
                "Processing extracted feedback for thread {ThreadId}",
                threadId);

            // Try to get existing trajectory from AI Search first
            var trajectoryMemory = await _searchIndexService.GetTrajectoryByIdAsync(threadId);

            if (trajectoryMemory != null)
            {
                // Trajectory exists in AI Search - add feedback to it
                await AddFeedbackToExistingTrajectoryAsync(threadId, trajectoryMemory, feedback.ExtractedKnowledge);
            }
            else
            {
                // Try to get trajectory from SessionInsight document
                var sessionInsight = await GetSessionInsightAsync(threadId);
                if (sessionInsight?.TrajectoryJson != null)
                {
                    _logger.LogInternalInformation(
                        "Found trajectory in SessionInsight for thread {ThreadId}, processing feedback",
                        threadId);
                    await AddFeedbackToTrajectoryFromInsightAsync(threadId, sessionInsight.TrajectoryJson, feedback.ExtractedKnowledge);
                }
                else
                {
                    _logger.LogInternalInformation(
                        "No trajectory found for thread {ThreadId} - feedback will be indexed when trajectory is generated",
                        threadId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error processing extracted feedback for thread {ThreadId}", threadId);
        }
    }
    private async Task AddFeedbackToExistingTrajectoryAsync(
        string threadId,
        AgentMemory.AgentMemory trajectoryMemory,
        ExtractedFeedbackKnowledge extractedKnowledge)
    {
        try
        {
            // Deep copy: Deserialize the trajectory data from the existing memory
            var trajectoryData = JsonSerializer.Deserialize<ProcessedTrajectoryOutput_v3>(
                trajectoryMemory.Chunk,
                AIJsonUtilities.DefaultOptions);
            if (trajectoryData == null)
            {
                _logger.LogInternalWarning("Failed to deserialize trajectory data for thread {ThreadId}", threadId);
                return;
            }

            // Process each knowledge item and append to appropriate fields
            foreach (var item in extractedKnowledge.KnowledgeItems)
            {
                switch (item.Type)
                {
                    case FeedbackKnowledgeType.InvestigationPattern:
                        // Append Investigation Pattern to Pitfalls
                        trajectoryData.Pitfalls = string.IsNullOrWhiteSpace(trajectoryData.Pitfalls)
                            ? item.Comment
                            : $"{trajectoryData.Pitfalls}\n\n{item.Comment}";
                        break;

                    case FeedbackKnowledgeType.SystemDesignKnowledge:
                        // Append System Design Knowledge to SystemDesignKnowledge
                        trajectoryData.SystemDesignKnowledge = string.IsNullOrWhiteSpace(trajectoryData.SystemDesignKnowledge)
                            ? item.Comment
                            : $"{trajectoryData.SystemDesignKnowledge}\n\n{item.Comment}";
                        break;

                    case FeedbackKnowledgeType.Other:
                        // Other feedback is stored but not indexed in specific fields
                        break;
                }
            }

            // Re-generate embedding with the updated trajectory
            var embeddingParts = new List<string>
            {
                trajectoryData.SymptomsObserved,
                trajectoryData.SystemDesignKnowledge,
                trajectoryData.StepsFollowed,
                trajectoryData.Pitfalls
            };

            var embeddingText = string.Join("\n", embeddingParts.Where(s => !string.IsNullOrWhiteSpace(s) && !s.Equals("N/A", StringComparison.OrdinalIgnoreCase)));
            var vector = await _embeddingGenerator.GenerateVectorForAgentMemoryAsync(embeddingText, _logger);

            // Create updated AgentMemory with feedback (deep copy with new data)
            var updatedMemory = AgentMemory.AgentMemory.FromTrajectory(
                trajectoryGuid: Guid.Parse(threadId),
                trajectoryData: trajectoryData,
                embedding: [.. vector.Span]
            );

            // Upsert the trajectory (handles delete + index internally)
            var success = await _searchIndexService.UpsertTrajectoryAsync(updatedMemory);

            if (success)
            {
                _logger.LogInternalInformation(
                    "Successfully persisted updated trajectory with feedback for {ThreadId} in AI Search",
                    threadId);
            }
            else
            {
                _logger.LogInternalError(
                    "Failed to persist updated trajectory with feedback for {ThreadId} in AI Search",
                    threadId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error adding feedback to existing trajectory {ThreadId}", threadId);
        }
    }

    /// <summary>
    /// Adds feedback to a trajectory from SessionInsight JSON data.
    /// </summary>
    private async Task AddFeedbackToTrajectoryFromInsightAsync(
        string threadId,
        string trajectoryJson,
        ExtractedFeedbackKnowledge extractedKnowledge)
    {
        try
        {
            // Deep copy: Deserialize the trajectory data from SessionInsight JSON
            var trajectoryData = JsonSerializer.Deserialize<ProcessedTrajectoryOutput_v3>(
                trajectoryJson,
                AIJsonUtilities.DefaultOptions);
            if (trajectoryData == null)
            {
                _logger.LogInternalWarning("Failed to deserialize trajectory JSON for thread {ThreadId}", threadId);
                return;
            }

            // Process each knowledge item and append to appropriate fields
            foreach (var item in extractedKnowledge.KnowledgeItems)
            {
                switch (item.Type)
                {
                    case FeedbackKnowledgeType.InvestigationPattern:
                        // Append Investigation Pattern to Pitfalls
                        trajectoryData.Pitfalls = string.IsNullOrWhiteSpace(trajectoryData.Pitfalls)
                            ? item.Comment
                            : $"{trajectoryData.Pitfalls}\n\n{item.Comment}";
                        break;

                    case FeedbackKnowledgeType.SystemDesignKnowledge:
                        // Append System Design Knowledge to SystemDesignKnowledge
                        trajectoryData.SystemDesignKnowledge = string.IsNullOrWhiteSpace(trajectoryData.SystemDesignKnowledge)
                            ? item.Comment
                            : $"{trajectoryData.SystemDesignKnowledge}\n\n{item.Comment}";
                        break;

                    case FeedbackKnowledgeType.Other:
                        // Other feedback is stored but not indexed in specific fields
                        break;
                }
            }

            // Re-generate embedding with the updated trajectory
            var embeddingParts = new List<string>
            {
                trajectoryData.SymptomsObserved,
                trajectoryData.SystemDesignKnowledge,
                trajectoryData.StepsFollowed,
                trajectoryData.Pitfalls
            };

            var embeddingText = string.Join("\n", embeddingParts.Where(s => !string.IsNullOrWhiteSpace(s) && !s.Equals("N/A", StringComparison.OrdinalIgnoreCase)));
            var vector = await _embeddingGenerator.GenerateVectorForAgentMemoryAsync(embeddingText, _logger);

            // Create updated AgentMemory with feedback (deep copy with new data)
            var updatedMemory = AgentMemory.AgentMemory.FromTrajectory(
                trajectoryGuid: Guid.Parse(threadId),
                trajectoryData: trajectoryData,
                embedding: [.. vector.Span]
            );

            // Upsert the trajectory (handles delete + index internally)
            var success = await _searchIndexService.UpsertTrajectoryAsync(updatedMemory);

            if (success)
            {
                _logger.LogInternalInformation(
                    "Successfully persisted trajectory with extracted feedback for thread {ThreadId} in AI Search",
                    threadId);
            }
            else
            {
                _logger.LogInternalError(
                    "Failed to persist trajectory with extracted feedback for thread {ThreadId} in AI Search",
                    threadId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error adding feedback to trajectory from SessionInsight for thread {ThreadId}", threadId);
        }
    }
}
