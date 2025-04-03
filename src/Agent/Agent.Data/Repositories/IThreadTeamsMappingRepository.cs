// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.Repositories;

public interface IThreadTeamsMappingRepository
{
    /// <summary>
    /// Gets a thread-teams mapping by thread ID
    /// </summary>
    /// <param name="threadId">The thread ID to look up</param>
    /// <returns>The thread-teams mapping or null if not found</returns>
    Task<ThreadTeamsMapping> GetMappingByThreadIdAsync(string threadId);

    /// <summary>
    /// Adds a new thread-teams mapping
    /// </summary>
    /// <param name="mapping">The mapping to add</param>
    /// <returns>The created mapping</returns>
    Task<ThreadTeamsMapping> AddMappingAsync(ThreadTeamsMapping mapping);

    /// <summary>
    /// Removes a thread-teams mapping by thread ID
    /// </summary>
    /// <param name="threadId">The thread ID of the mapping to remove</param>
    /// <returns>True if successful, false if not found</returns>
    Task<bool> RemoveThreadMappingAsync(string threadId);

    /// <summary>
    /// Gets a thread-teams mapping by Teams conversation ID
    /// </summary>
    /// <param name="conversationId">The Teams conversation ID to look up</param>
    /// <returns>The thread-teams mapping or null if not found</returns>
    Task<ThreadTeamsMapping> GetMappingByConversationIdAsync(string conversationId);

    /// <summary>
    /// Gets the first thread-teams mapping with a non-empty ServiceUrl and ChannelId
    /// </summary>
    /// <returns>The thread-teams mapping or null if none is found</returns>
    Task<ThreadTeamsMapping> GetFirstOrDefaultChannel();

    /// <summary>
    /// Gets a list of all active thread-teams mappings
    /// </summary>
    /// <returns>A list of active thread-teams mappings</returns>
    Task<IEnumerable<ThreadTeamsMapping>> ListActiveConversationsAsync();

    /// <summary>
    /// Adds multiple message IDs to the PostedMessages list
    /// </summary>
    /// <param name="threadId">The thread ID</param>
    /// <param name="messageIds">The message IDs to add</param>
    /// <returns>True if successful, false if thread not found</returns>
    Task<bool> AddPostedMessagesAsync(string threadId, IEnumerable<string> messageIds);

    /// <summary>
    /// Gets all posted messages for a specific thread
    /// </summary>
    /// <param name="threadId">The thread ID</param>
    /// <returns>List of posted message IDs or empty list if none found</returns>
    Task<IList<string>> GetPostedMessagesAsync(string threadId);
}

