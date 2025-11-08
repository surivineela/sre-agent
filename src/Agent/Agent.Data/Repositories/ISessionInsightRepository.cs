// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Data.Repositories;

/// <summary>
/// Repository for managing session insights in Cosmos DB
/// </summary>
public interface ISessionInsightRepository
{
    /// <summary>
    /// Gets a session insight by thread ID
    /// </summary>
    Task<SessionInsightDocument?> GetSessionInsightAsync(string threadId);
    
    /// <summary>
    /// Gets all session insights with pagination
    /// </summary>
    Task<List<SessionInsightDocument>> GetSessionInsightsAsync(int skip = 0, int take = 50);
    
    /// <summary>
    /// Gets session insights generated within a time range
    /// </summary>
    Task<List<SessionInsightDocument>> GetSessionInsightsByTimeRangeAsync(
        DateTime startTime, 
        DateTime endTime, 
        int skip = 0, 
        int take = 50);
    
    /// <summary>
    /// Gets session insights that are investigation threads only
    /// </summary>
    Task<List<SessionInsightDocument>> GetInvestigationInsightsAsync(int skip = 0, int take = 50);
    
    /// <summary>
    /// Creates or updates a session insight
    /// </summary>
    Task<SessionInsightDocument> UpsertSessionInsightAsync(SessionInsightDocument insight);
    
    /// <summary>
    /// Adds feedback to a session insight
    /// </summary>
    Task<bool> AddFeedbackToInsightAsync(string threadId, InsightFeedback feedback);
    
    /// <summary>
    /// Deletes a session insight
    /// </summary>
    Task<bool> DeleteSessionInsightAsync(string threadId);
    
    /// <summary>
    /// Checks if a session insight exists for a thread
    /// </summary>
    Task<bool> SessionInsightExistsAsync(string threadId);
    
    /// <summary>
    /// Gets session insights by resource involvement
    /// </summary>
    Task<List<SessionInsightDocument>> GetSessionInsightsByResourceAsync(
        string resourceId, 
        int skip = 0, 
        int take = 50);
}
