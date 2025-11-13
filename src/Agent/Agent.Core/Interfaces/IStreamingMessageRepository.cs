// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Core.Interfaces;

/// <summary>
/// Repository for buffering streaming messages temporarily before persistence.
/// Provides fast access to incomplete messages during streaming responses.
/// </summary>
public interface IStreamingMessageRepository
{
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
    /// Updates a message with new content by appending to existing content.
    /// Creates a new message if one doesn't exist.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="content">The content to append.</param>
    Task UpdateMessageContentAsync(Guid threadId, Guid messageId, string content);
}
