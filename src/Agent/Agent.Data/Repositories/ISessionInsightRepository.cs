// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Data.DataModels;

namespace Agent.Data.Repositories;

/// <summary>
/// Repository for managing session insights in Cosmos DB
/// </summary>
public interface ISessionInsightRepository
{
    /// <summary>
    /// Gets the most recent session insight by thread ID
    /// </summary>
    Task<SessionInsightDocument?> GetSessionInsightAsync(string threadId);

    /// <summary>
    /// Gets a session insight by document ID
    /// </summary>
    Task<SessionInsightDocument?> GetSessionInsightByIdAsync(string insightId);

    /// <summary>
    /// Gets all session insights with pagination
    /// </summary>
    Task<List<SessionInsightDocument>> GetSessionInsightsAsync(int skip = 0, int take = 50);

    /// <summary>
    /// Gets all session insights for a specific thread ID
    /// </summary>
    Task<List<SessionInsightDocument>> GetSessionInsightsByThreadIdAsync(string threadId);

    /// <summary>
    /// Gets session insights that are investigation threads only
    /// </summary>
    Task<List<SessionInsightDocument>> GetInvestigationInsightsAsync(int skip = 0, int take = 50);

    /// <summary>
    /// Creates or updates a session insight
    /// </summary>
    Task<SessionInsightDocument> UpsertSessionInsightAsync(SessionInsightDocument insight);

    /// <summary>
    /// Adds feedback to a session insight by insight ID
    /// </summary>
    Task<bool> AddFeedbackToInsightAsync(string insightId, InsightFeedback feedback);

    /// <summary>
    /// Deletes a session insight by insight ID
    /// </summary>
    Task<bool> DeleteSessionInsightAsync(string insightId);

    /// <summary>
    /// Deletes all session insights for a specific thread ID
    /// </summary>
    Task<int> DeleteSessionInsightsByThreadIdAsync(string threadId);

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
