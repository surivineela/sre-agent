// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.AgentMemory;

public interface ISearchIndexService
{
    /// <summary>
    /// Creates or updates the search index with the current schema
    /// </summary>
    Task CreateOrUpdateIndexAsync();

    /// <summary>
    /// Indexes a single piece of content
    /// </summary>
    Task<bool> IndexContentAsync(AgentMemory content);

    /// <summary>
    /// Upserts a trajectory - deletes existing if present, then indexes the new content.
    /// This is the preferred method for updating trajectories as AI Search doesn't support true upserts.
    /// </summary>
    Task<bool> UpsertTrajectoryAsync(AgentMemory trajectory);

    /// <summary>
    /// Deletes memories
    /// </summary>
    Task<bool> DeleteContentsAsync(List<AgentMemory> memories);

    /// <summary>
    /// Gets a trajectory document by its ID from the search index
    /// </summary>
    Task<AgentMemory?> GetTrajectoryByIdAsync(string trajectoryId);

    Task DeleteIndexIfExistsAsync();
}
