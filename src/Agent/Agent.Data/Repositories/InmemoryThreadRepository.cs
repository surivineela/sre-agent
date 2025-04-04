// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Thread = Agent.Core.Models.Api.v1.Thread;
using Action = Agent.Core.Models.Api.v1.Action;
using System;
using System.Threading;

namespace Agent.Data.Repositories
{
    /// <summary>
    /// In-memory implementation of the IThreadRepository interface.
    /// This is primarily for testing purposes.
    /// </summary>
    public class InmemoryThreadRepository : IThreadRepository
    {
        private readonly Dictionary<Guid, Thread> _threads = new();
        private readonly Dictionary<Guid, ThreadContext> _threadContexts = new();
        private readonly Dictionary<(Guid ThreadId, Guid MessageId), Message> _messages = new();
        private readonly Dictionary<(Guid ThreadId, Guid ActionId), Action> _actions = new();
        private readonly Dictionary<string, object> _threadTeamsMappings = new();
        private readonly ILogger<InmemoryThreadRepository> _logger;

        public InmemoryThreadRepository(ILogger<InmemoryThreadRepository> logger)
        {
            _logger = logger;
        }

        #region Thread Operations

        public Task<Thread> GetThreadAsync(Guid threadId)
        {
            _logger.LogInformation("Trying to get thread: {Id}", threadId);
            _threads.TryGetValue(threadId, out var thread);
            return Task.FromResult(thread);
        }

        public Task<IEnumerable<Thread>> GetThreadsAsync(string? filter = null, int? skip = null, int? take = null)
        {
            IEnumerable<Thread> threads = _threads.Values;

            // Apply skip if specified
            if (skip.HasValue)
            {
                threads = threads.Skip(skip.Value);
            }

            // Apply take if specified
            if (take.HasValue)
            {
                threads = threads.Take(take.Value);
            }

            return Task.FromResult(threads);
        }

        public Task<Thread> CreateThreadAsync(Thread thread)
        {
            // Ensure IDs are set
            if (thread.Id == Guid.Empty)
                thread = thread with { Id = Guid.NewGuid() };

            if (thread.StartMessage.Id == Guid.Empty)
                thread = thread with { StartMessage = thread.StartMessage with { Id = Guid.NewGuid() } };

            _threads[thread.Id] = thread;

            // Also store the start message
            _messages[(thread.Id, thread.StartMessage.Id)] = thread.StartMessage;

            return Task.FromResult(thread);
        }

        public Task<bool> DeleteThreadAsync(Guid threadId)
        {
            if (!_threads.ContainsKey(threadId))
            {
                return Task.FromResult(false);
            }

            // Remove all messages for this thread
            var messagesToRemove = _messages.Keys
                .Where(key => key.ThreadId == threadId)
                .ToList();

            foreach (var key in messagesToRemove)
            {
                _messages.Remove(key);
            }

            // Remove all actions for this thread
            var actionsToRemove = _actions.Keys
                .Where(key => key.ThreadId == threadId)
                .ToList();

            foreach (var key in actionsToRemove)
            {
                _actions.Remove(key);
            }

            // Remove all teams mappings for this thread
            string threadIdStr = threadId.ToString();
            var mappingsToRemove = _threadTeamsMappings.Keys
                .Where(key => key.Contains(threadIdStr))
                .ToList();

            foreach (var key in mappingsToRemove)
            {
                _threadTeamsMappings.Remove(key);
            }

            // Remove the thread itself
            _threads.Remove(threadId);

            return Task.FromResult(true);
        }

        public Task<Thread> UpdateThreadTitleAsync(Guid threadId, string newTitle)
        {
            if (!_threads.TryGetValue(threadId, out var thread))
            {
                _logger.LogWarning("Cannot update title: Thread {ThreadId} not found", threadId);
                return Task.FromResult<Thread>(null);
            }

            // Update the title and modified timestamp
            var updatedThread = thread with
            {
                Title = newTitle,
                ModifiedTimestamp = DateTime.UtcNow
            };

            _threads[threadId] = updatedThread;

            _logger.LogInformation("Successfully updated title for thread {ThreadId}", threadId);
            return Task.FromResult(updatedThread);
        }

        #endregion

        #region Message Operations

        public Task<Message> GetMessageAsync(Guid threadId, Guid messageId)
        {
            _messages.TryGetValue((threadId, messageId), out var message);
            return Task.FromResult(message);
        }

        public Task<IEnumerable<Message>> GetMessagesAsync(Guid threadId, string filter = null, int? skip = null, int? take = null)
        {
            var messages = _messages
                .Where(kvp => kvp.Key.ThreadId == threadId)
                .Select(kvp => kvp.Value)
                .OrderBy(m => m.TimeStamp)
                .AsEnumerable();

            return Task.FromResult(messages);
        }

        public Task<Message> AddMessageAsync(Guid threadId, Message message)
        {
            // Ensure ID is set
            if (message.Id == Guid.Empty)
                message = message with { Id = Guid.NewGuid() };

            if (message.Posted == null)
                message = message with { Posted = new Posted(false) };

            _messages[(threadId, message.Id)] = message;

            // Update the thread's modified timestamp
            if (_threads.TryGetValue(threadId, out var thread))
            {
                _threads[threadId] = thread with { ModifiedTimestamp = DateTime.UtcNow };
            }

            return Task.FromResult(message);
        }

        public Task<bool> DeleteMessageAsync(Guid threadId, Guid messageId)
        {
            // Check if this is a start message
            if (_threads.TryGetValue(threadId, out var thread) &&
                thread.StartMessage.Id == messageId)
            {
                // Can't delete start message without deleting thread
                return Task.FromResult(false);
            }

            return Task.FromResult(_messages.Remove((threadId, messageId)));
        }

        #endregion

        #region ThreadContext Operations

        public Task<ThreadContext> GetThreadContextAsync(Guid threadId)
        {
            _threadContexts.TryGetValue(threadId, out var action);
            return Task.FromResult(action);
        }

        public Task<IEnumerable<ThreadContext>> GetThreadContextsAsync(string? filter = null, int? skip = null, int? take = null)
        {
            IEnumerable<ThreadContext> threadContexts = _threadContexts.Values;

            // Apply skip if specified
            if (skip.HasValue)
            {
                threadContexts = threadContexts.Skip(skip.Value);
            }

            // Apply take if specified
            if (take.HasValue)
            {
                threadContexts = threadContexts.Take(take.Value);
            }

            return Task.FromResult(threadContexts);
        }

        public Task<ThreadContext> AddThreadContextAsync(ThreadContext context)
        {
            // Ensure ID is set
            if (context.ThreadId == Guid.Empty)
                context = new ThreadContext(Guid.NewGuid());

            _threadContexts[context.ThreadId] = context;

            return Task.FromResult(context);
        }

        public Task<bool> DeleteThreadContextAsync(Guid threadId)
        {
            _threadContexts.Remove(threadId);
            return Task.FromResult(true);
        }

        #endregion

        #region Action Operations

        public Task<IEnumerable<Action>> GetActionsAsync(Guid threadId, int? skip = null, int? take = null)
        {
            var actions = _actions
                .Where(kvp => kvp.Key.ThreadId == threadId)
                .Select(kvp => kvp.Value)
                .OrderByDescending(a => a.TimeStamp)
                .AsEnumerable();

            return Task.FromResult(actions);
        }

        public Task<Action> AddActionAsync(Guid threadId, Action action)
        {
            // Ensure ID is set
            if (action.Id == Guid.Empty)
                action = action with { Id = Guid.NewGuid() };

            _actions[(threadId, action.Id)] = action;

            return Task.FromResult(action);
        }

        public Task<Action> GetActionAsync(Guid threadId, Guid actionId)
        {
            try
            {
                _actions.TryGetValue((threadId, actionId), out var action);
                return Task.FromResult(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving action {ActionId} for thread {ThreadId}", actionId, threadId);
                throw;
            }
        }
        #endregion
    }
}

