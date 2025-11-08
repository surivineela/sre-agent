// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Data.Helpers;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

public class CosmosDbSessionInsightRepository : ISessionInsightRepository
{
    private readonly ILogger<CosmosDbSessionInsightRepository> _logger;
    private readonly string _databaseName;
    private readonly CosmosClient _client;

    public CosmosDbSessionInsightRepository(
        CosmosClient cosmosClient, 
        string databaseName, 
        ILogger<CosmosDbSessionInsightRepository> logger)
    {
        _logger = logger;
        _databaseName = databaseName;
        _client = cosmosClient;
    }

    public async Task<SessionInsightDocument?> GetSessionInsightAsync(string threadId)
    {
        _logger.LogInternalInformation("Getting session insight for thread: {ThreadId}", threadId);

        try
        {
            var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
                .GetItemLinqQueryable<SessionInsightDocument>(requestOptions: new QueryRequestOptions { MaxItemCount = -1 })
                .Where(x => x.ThreadId == threadId)
                .ToFeedIterator();

            SessionInsightDocument? insight = null;
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                insight = response.FirstOrDefault();
                if (insight != null)
                    break;
            }
                
            if (insight != null)
            {
                _logger.LogInternalInformation("Found session insight for thread: {ThreadId}", threadId);
            }
            else
            {
                _logger.LogInternalWarning("Session insight not found for thread: {ThreadId}", threadId);
            }
            
            return insight;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Session insight not found for thread: {ThreadId}", threadId);
            return null;
        }
    }

    public async Task<List<SessionInsightDocument>> GetSessionInsightsAsync(int skip = 0, int take = 50)
    {
        _logger.LogInternalInformation("Fetching session insights with skip: {Skip}, take: {Take}", skip, take);

        var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
            .GetItemLinqQueryable<SessionInsightDocument>(requestOptions: new QueryRequestOptions 
            { 
                MaxItemCount = take 
            })
            .Where(doc => doc.DocumentType == "SessionInsight")
            .OrderByDescending(doc => doc.GeneratedTimestamp)
            .Skip(skip)
            .Take(take)
            .ToFeedIterator();

        var insights = new List<SessionInsightDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            insights.AddRange(response);
        }

        _logger.LogInternalInformation("Fetched {Count} session insights", insights.Count);
        return insights;
    }

    public async Task<List<SessionInsightDocument>> GetSessionInsightsByTimeRangeAsync(
        DateTime startTime, 
        DateTime endTime, 
        int skip = 0, 
        int take = 50)
    {
        _logger.LogInternalInformation(
            "Fetching session insights between {StartTime} and {EndTime}", 
            startTime, 
            endTime);

        var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
            .GetItemLinqQueryable<SessionInsightDocument>(requestOptions: new QueryRequestOptions 
            { 
                MaxItemCount = take 
            })
            .Where(doc => 
                doc.DocumentType == "SessionInsight" &&
                doc.GeneratedTimestamp >= startTime &&
                doc.GeneratedTimestamp <= endTime)
            .OrderByDescending(doc => doc.GeneratedTimestamp)
            .Skip(skip)
            .Take(take)
            .ToFeedIterator();

        var insights = new List<SessionInsightDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            insights.AddRange(response);
        }

        _logger.LogInternalInformation("Fetched {Count} session insights in time range", insights.Count);
        return insights;
    }

    public async Task<List<SessionInsightDocument>> GetInvestigationInsightsAsync(int skip = 0, int take = 50)
    {
        _logger.LogInternalInformation("Fetching investigation insights with skip: {Skip}, take: {Take}", skip, take);

        var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
            .GetItemLinqQueryable<SessionInsightDocument>(requestOptions: new QueryRequestOptions 
            { 
                MaxItemCount = take 
            })
            .Where(doc => 
                doc.DocumentType == "SessionInsight" && 
                doc.IsInvestigationThread)
            .OrderByDescending(doc => doc.GeneratedTimestamp)
            .Skip(skip)
            .Take(take)
            .ToFeedIterator();

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
        _logger.LogInternalInformation("Upserting session insight for thread: {ThreadId}", insight.ThreadId);

        var response = await _client.GetContainer<SessionInsightDocument>(_databaseName)
            .UpsertItemAsync(
                insight,
                new PartitionKey(insight.PartitionKey));

        _logger.LogInternalInformation(
            "Upserted session insight for thread: {ThreadId}, RU consumed: {RU}", 
            insight.ThreadId, 
            response.RequestCharge);
            
        return response.Resource;
    }

    public async Task<bool> AddFeedbackToInsightAsync(string threadId, InsightFeedback feedback)
    {
        _logger.LogInternalInformation("Adding feedback to session insight for thread: {ThreadId}", threadId);

        try
        {
            var insight = await GetSessionInsightAsync(threadId);
            if (insight == null)
            {
                _logger.LogInternalWarning("Cannot add feedback - session insight not found for thread: {ThreadId}", threadId);
                return false;
            }

            // Initialize feedback list if null
            insight.Feedback ??= new List<InsightFeedback>();
            
            // Add new feedback
            insight.Feedback.Add(feedback);

            // Update the document
            await UpsertSessionInsightAsync(insight);

            _logger.LogInternalInformation("Successfully added feedback to session insight for thread: {ThreadId}", threadId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error adding feedback to session insight for thread: {ThreadId}", threadId);
            return false;
        }
    }

    public async Task<bool> DeleteSessionInsightAsync(string threadId)
    {
        _logger.LogInternalInformation("Deleting session insight for thread: {ThreadId}", threadId);

        try
        {
            await _client.GetContainer<SessionInsightDocument>(_databaseName)
                .DeleteItemAsync<SessionInsightDocument>(
                    threadId,
                    new PartitionKey(threadId));
                
            _logger.LogInternalInformation("Deleted session insight for thread: {ThreadId}", threadId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("Session insight not found for deletion, thread: {ThreadId}", threadId);
            return false;
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

        // Note: This requires that ResourcesInvolved contains the full resource ID or a searchable portion
        var iterator = _client.GetContainer<SessionInsightDocument>(_databaseName)
            .GetItemLinqQueryable<SessionInsightDocument>(requestOptions: new QueryRequestOptions 
            { 
                MaxItemCount = take 
            })
            .Where(doc => 
                doc.DocumentType == "SessionInsight" &&
                doc.ResourcesInvolved != null)
            .OrderByDescending(doc => doc.GeneratedTimestamp)
            .ToFeedIterator();

        var insights = new List<SessionInsightDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            
            // Filter in memory for resource involvement
            var filtered = response.Where(insight => 
                insight.ResourcesInvolved != null && 
                insight.ResourcesInvolved.Any(r => r.Contains(resourceId, StringComparison.OrdinalIgnoreCase)));
                
            insights.AddRange(filtered);
            
            if (insights.Count >= take + skip)
                break;
        }

        var result = insights.Skip(skip).Take(take).ToList();
        _logger.LogInternalInformation("Fetched {Count} session insights for resource: {ResourceId}", result.Count, resourceId);
        return result;
    }
}
