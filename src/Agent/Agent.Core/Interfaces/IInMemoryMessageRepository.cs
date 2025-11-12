// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Core.Interfaces;

/// <summary>
/// In-memory repository for storing and managing messages within threads.
/// Provides fast access to messages without persistence.
/// </summary>
public interface IInMemoryMessageRepository
{
    /// <summary>
    /// Adds a new message to the specified thread.
    /// </summary>
    /// <param name="threadId">The thread ID to add the message to.</param>
    /// <param name="message">The message to add.</param>
    /// <returns>The added message.</returns>
    Task<Message> AddMessageAsync(Guid threadId, Message message);

    /// <summary>
    /// Updates an existing message in the specified thread.
    /// </summary>
    /// <param name="threadId">The thread ID containing the message.</param>
    /// <param name="message">The updated message.</param>
    /// <returns>The updated message, or null if not found.</returns>
    Task<Message?> UpdateMessageAsync(Guid threadId, Message message);

    /// <summary>
    /// Deletes a message from the specified thread.
    /// </summary>
    /// <param name="threadId">The thread ID containing the message.</param>
    /// <param name="messageId">The ID of the message to delete.</param>
    /// <returns>True if the message was deleted, false if not found.</returns>
    Task<bool> DeleteMessageAsync(Guid threadId, Guid messageId);

    /// <summary>
    /// Gets a specific message from a thread.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="messageId">The message ID.</param>
    /// <returns>The message if found, otherwise null.</returns>
    Task<Message?> GetMessageAsync(Guid threadId, Guid messageId);

    /// <summary>
    /// Gets all messages for a specific thread.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <returns>Collection of messages in the thread.</returns>
    Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId);

    /// <summary>
    /// Clears all messages from a specific thread.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <returns>True if messages were cleared.</returns>
    Task<bool> ClearThreadMessagesAsync(Guid threadId);

    /// <summary>
    /// Clears all messages from all threads.
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// Deletes messages that are older than the specified timeout duration.
    /// </summary>
    /// <param name="timeoutDuration">Messages older than this duration will be deleted.</param>
    /// <returns>Number of messages deleted.</returns>
    Task<int> DeleteStaleMessagesAsync(TimeSpan timeoutDuration);
}
