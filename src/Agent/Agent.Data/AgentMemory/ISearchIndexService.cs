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
    Task<bool> IndexContentAsync(BaseIndexableContent content);

    /// <summary>
    /// Indexes a single piece of content
    /// </summary>
    Task<bool> IndexContentAsync(AgentMemory content);

    /// <summary>
    /// Deletes memories
    /// </summary>
    Task<bool> DeleteContentsAsync(List<AgentMemory> memories);
}
