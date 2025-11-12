// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.Repositories;

/// <summary>
/// In-memory implementation of message storage using thread-safe collections.
/// Stores messages organized by thread ID for fast retrieval and updates.
/// </summary>
public class InMemoryMessageRepository : IInMemoryMessageRepository
{
    // Thread-safe dictionary: ThreadId -> (MessageId -> Message)
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Message>> _storage = new();

    /// <inheritdoc />
    public Task<Message> AddMessageAsync(Guid threadId, Message message)
    {
        var threadMessages = _storage.GetOrAdd(threadId, _ => new ConcurrentDictionary<Guid, Message>());

        if (!threadMessages.TryAdd(message.Id, message))
        {
            throw new InvalidOperationException($"Message with ID {message.Id} already exists in thread {threadId}");
        }

        return Task.FromResult(message);
    }

    /// <inheritdoc />
    public Task<Message?> UpdateMessageAsync(Guid threadId, Message message)
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
    public Task ClearAllAsync()
    {
        _storage.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> DeleteStaleMessagesAsync(TimeSpan timeoutDuration)
    {
        var cutoffTime = DateTime.UtcNow - timeoutDuration;
        var deletedCount = 0;

        // Take a snapshot of thread IDs to avoid modification during enumeration
        var threadIds = _storage.Keys.ToList();

        foreach (var threadId in threadIds)
        {
            // Safely get the thread messages - may not exist if removed by another thread
            if (!_storage.TryGetValue(threadId, out var threadMessages))
            {
                continue;
            }

            // Take a snapshot of message IDs to check - thread-safe enumeration
            var messageSnapshot = threadMessages.ToArray();

            // Find and delete stale messages
            foreach (var messageKvp in messageSnapshot)
            {
                if (messageKvp.Value.TimeStamp < cutoffTime)
                {
                    // TryRemove is atomic - safe even if message was already removed
                    if (threadMessages.TryRemove(messageKvp.Key, out _))
                    {
                        deletedCount++;
                    }
                }
            }

            // Clean up empty thread storage - use safe check and remove
            // Only remove if truly empty to avoid race condition with new messages
            if (threadMessages.IsEmpty)
            {
                // Double-check inside TryRemove condition to ensure atomicity
                // If another thread adds a message between IsEmpty check and TryRemove,
                // we might remove a non-empty dictionary, so we verify after removal
                if (_storage.TryRemove(threadId, out var removed))
                {
                    // If messages were added after our IsEmpty check but before TryRemove,
                    // put it back to avoid losing messages
                    if (!removed.IsEmpty)
                    {
                        _storage.TryAdd(threadId, removed);
                    }
                }
            }
        }

        return Task.FromResult(deletedCount);
    }
}
