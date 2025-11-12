// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.Communication;

/// <summary>
/// Service for managing in-memory message storage.
/// Provides a simple wrapper over the repository for temporary message buffering during streaming.
/// </summary>
public class InMemoryMessageStorageService
{
    private readonly IInMemoryMessageRepository _repository;
    private readonly SinkService _sinkService;

    public InMemoryMessageStorageService(IInMemoryMessageRepository repository, SinkService sinkService)
    {
        _repository = repository;
        _sinkService = sinkService;
    }

    /// <summary>
    /// Deletes a message from the specified thread if messageId is not null.
    /// </summary>
    public Task<bool> DeleteMessageAsync(Guid threadId, Guid? messageId)
    {
        if (messageId == null)
        {
            return Task.FromResult(false);
        }
        return _repository.DeleteMessageAsync(threadId, messageId.Value);
    }

    /// <summary>
    /// Gets a specific message from a thread.
    /// </summary>
    public Task<Message?> GetMessageAsync(Guid threadId, Guid messageId)
    {
        return _repository.GetMessageAsync(threadId, messageId);
    }

    /// <summary>
    /// Updates a message with new content by appending to existing content.
    /// Creates a new message if one doesn't exist.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="content">The content to append.</param>
    public async Task UpdateMessageContentAsync(Guid threadId, Guid messageId, string content)
    {
        // Get existing message
        var existingMessage = await _repository.GetMessageAsync(threadId, messageId);

        if (existingMessage == null)
        {
            // Create new message if it doesn't exist
            var newMessage = new Message(
                Id: messageId,
                TimeStamp: DateTime.UtcNow,
                Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
                Text: content,
                IsComplete: false
            );
            await _repository.AddMessageAsync(threadId, newMessage);
        }
        else
        {
            // Append content to existing message
            var updatedText = existingMessage.Text + content;
            var updatedMessage = new Message(
                Id: messageId,
                TimeStamp: existingMessage.TimeStamp,
                Author: existingMessage.Author,
                Text: updatedText,
                IsComplete: false
            );
            await _repository.UpdateMessageAsync(threadId, updatedMessage);
        }
    }

    /// <summary>
    /// Gets all messages for a specific thread from in-memory storage.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <returns>Collection of messages in the thread.</returns>
    public Task<IEnumerable<Message>> GetAllMessagesForThreadAsync(Guid threadId)
    {
        return _repository.GetMessagesAsync(threadId);
    }

    /// <summary>
    /// Merges incomplete in-memory messages with the provided list of existing messages.
    /// Appends the latest incomplete in-memory message if it doesn't already exist in the provided list.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="existingMessages">The existing filtered messages from the database.</param>
    /// <returns>List of messages with the latest incomplete in-memory message merged in.</returns>
    public async Task<List<Message>> MergeIncompleteMessagesAsync(Guid threadId, List<Message> existingMessages)
    {
        var result = new List<Message>(existingMessages);

        // Get in-memory incomplete messages
        var inMemoryMessages = await GetAllMessagesForThreadAsync(threadId);
        var incomplete = inMemoryMessages?.Where(m => !m.IsComplete).OrderByDescending(m => m.TimeStamp).ToList();

        if (incomplete != null && incomplete.Count > 0)
        {
            var latest = incomplete.First();
            // Only append if this message ID doesn't already exist in the filtered messages from DB
            if (!result.Any(m => m.Id == latest.Id))
            {
                result.Insert(0, latest);
            }
        }

        return result;
    }

    /// <summary>
    /// Clears all messages from a specific thread.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <returns>True if messages were cleared.</returns>
    public Task<bool> ClearThreadMessagesAsync(Guid threadId)
    {
        return _repository.ClearThreadMessagesAsync(threadId);
    }

    /// <summary>
    /// Deletes messages that are older than the specified timeout duration.
    /// </summary>
    /// <param name="timeoutDuration">Messages older than this duration will be deleted.</param>
    /// <returns>Number of messages deleted.</returns>
    public Task<int> DeleteStaleMessagesAsync(TimeSpan timeoutDuration)
    {
        return _repository.DeleteStaleMessagesAsync(timeoutDuration);
    }

    /// <summary>
    /// Saves an in-memory message to the persistent database.
    /// Marks the message as complete before persisting, then deletes it from in-memory storage.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="messageId">The message ID.</param>
    /// <returns>The persisted message ID, or null if the message was not found.</returns>
    public async Task<Guid?> SaveMessageToDbAsync(Guid threadId, Guid messageId)
    {
        // Get the in-memory message
        var message = await _repository.GetMessageAsync(threadId, messageId);

        if (message == null)
        {
            return null;
        }

        // Persist to database using SinkService with minimal parameters
        var persistedMessageId = await _sinkService.SinkAgentMessageAsync(
            threadId: threadId,
            messageText: message.Text ?? string.Empty,
            agentResponseMessageId: messageId,
            recordedDateTime: message.TimeStamp,
            isComplete: true
        );

        // Delete the message from in-memory storage after successful persistence
        await _repository.DeleteMessageAsync(threadId, messageId);

        return persistedMessageId;
    }
}
