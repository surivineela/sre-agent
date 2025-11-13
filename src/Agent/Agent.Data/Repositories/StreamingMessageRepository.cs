// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.Repositories;

/// <summary>
/// Streaming message repository implementation using thread-safe in-memory collections.
/// Buffers incomplete messages during streaming responses before they are persisted.
/// </summary>
public class StreamingMessageRepository : IStreamingMessageRepository
{
    // Thread-safe dictionary: ThreadId -> (MessageId -> Message)
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Message>> _storage = new();

    private Task<Message> AddMessageAsync(Guid threadId, Message message)
    {
        var threadMessages = _storage.GetOrAdd(threadId, _ => new ConcurrentDictionary<Guid, Message>());

        if (!threadMessages.TryAdd(message.Id, message))
        {
            throw new InvalidOperationException($"Message with ID {message.Id} already exists in thread {threadId}");
        }

        return Task.FromResult(message);
    }

    private Task<Message?> UpdateMessageAsync(Guid threadId, Message message)
    {
        if (!_storage.TryGetValue(threadId, out var threadMessages))
        {
            return Task.FromResult<Message?>(null);
        }

        // Update the message if it exists
        if (threadMessages.TryGetValue(message.Id, out var existingMessage))
        {
            threadMessages[message.Id] = message;
            return Task.FromResult<Message?>(message);
        }

        return Task.FromResult<Message?>(null);
    }

    /// <inheritdoc />
    public Task<bool> DeleteMessageAsync(Guid threadId, Guid messageId)
    {
        if (!_storage.TryGetValue(threadId, out var threadMessages))
        {
            return Task.FromResult(false);
        }

        var removed = threadMessages.TryRemove(messageId, out _);

        // Clean up empty thread storage
        if (removed && threadMessages.IsEmpty)
        {
            _storage.TryRemove(threadId, out _);
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<Message?> GetMessageAsync(Guid threadId, Guid messageId)
    {
        if (_storage.TryGetValue(threadId, out var threadMessages) &&
            threadMessages.TryGetValue(messageId, out var message))
        {
            return Task.FromResult<Message?>(message);
        }

        return Task.FromResult<Message?>(null);
    }

    /// <inheritdoc />
    public Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId)
    {
        if (_storage.TryGetValue(threadId, out var threadMessages))
        {
            // Return messages ordered by timestamp
            var messages = threadMessages.Values.OrderBy(m => m.TimeStamp).ToList();
            return Task.FromResult<IEnumerable<Message>>(messages);
        }

        return Task.FromResult<IEnumerable<Message>>(Array.Empty<Message>());
    }

    /// <inheritdoc />
    public Task<bool> ClearThreadMessagesAsync(Guid threadId)
    {
        return Task.FromResult(_storage.TryRemove(threadId, out _));
    }

    /// <inheritdoc />
    public async Task UpdateMessageContentAsync(Guid threadId, Guid messageId, string content)
    {
        // Get existing message
        var existingMessage = await GetMessageAsync(threadId, messageId);

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
            await AddMessageAsync(threadId, newMessage);
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
            await UpdateMessageAsync(threadId, updatedMessage);
        }
    }
}
