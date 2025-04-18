// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.Logging;
using Action = Agent.Core.Models.Api.v1.Action;

namespace Agent.Plugins.Mocks
{
    /// <summary>
    /// Mock implementation of IRecordActionsPlugin for testing and development
    /// </summary>
    public class MockRecordActionsPlugin : IRecordActionsPlugin
    {
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<MockRecordActionsPlugin>? _logger;
        private readonly Dictionary<Guid, Dictionary<Guid, Action>> _actionsByThread = new();
        private readonly Random _random = new Random(42);

        public MockRecordActionsPlugin(TimeProvider timeProvider, ILogger<MockRecordActionsPlugin>? logger)
        {
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>
        /// Records a new action
        /// </summary>
        public Task<Action> RecordAction(Guid threadId, string title, ActionStatus status = ActionStatus.Pending)
        {
            _logger?.LogInformation("[MOCK] Recording action for thread {ThreadId}: {Title} with status {Status}",
                threadId, title, status);

            var guidBytes = new byte[16];
            _random.NextBytes(guidBytes);

            // Create action with new ID
            var action = new Action(
                Id: new Guid(guidBytes),
                Title: title,
                TimeStamp: _timeProvider.GetUtcNow().DateTime,
                Status: status
            );

            // Store in memory
            if (!_actionsByThread.ContainsKey(threadId))
            {
                _actionsByThread[threadId] = new Dictionary<Guid, Action>();
            }

            _actionsByThread[threadId][action.Id] = action;

            return Task.FromResult(action);
        }

        /// <summary>
        /// Updates an existing action's status
        /// </summary>
        public Task<Action> UpdateActionStatus(Guid threadId, Guid actionId, ActionStatus status)
        {
            _logger?.LogInformation("[MOCK] Updating action {ActionId} status to {Status} for thread {ThreadId}",
                actionId, status, threadId);

            if (!_actionsByThread.TryGetValue(threadId, out var threadActions))
            {
                _logger?.LogWarning("[MOCK] Thread {ThreadId} not found", threadId);
                throw new KeyNotFoundException($"Thread {threadId} not found");
            }

            if (!threadActions.TryGetValue(actionId, out var existingAction))
            {
                _logger?.LogWarning("[MOCK] Action {ActionId} not found in thread {ThreadId}", actionId, threadId);
                throw new KeyNotFoundException($"Action {actionId} not found in thread {threadId}");
            }

            // Create updated action (immutable record)
            var updatedAction = existingAction with { Status = status };

            // Update in dictionary
            threadActions[actionId] = updatedAction;

            return Task.FromResult(updatedAction);
        }

        /// <summary>
        /// Retrieves a specific action
        /// </summary>
        public Task<Action> GetAction(Guid threadId, Guid actionId)
        {
            _logger?.LogInformation("[MOCK] Getting action {ActionId} for thread {ThreadId}", actionId, threadId);

            if (!_actionsByThread.TryGetValue(threadId, out var threadActions))
            {
                _logger?.LogWarning("[MOCK] Thread {ThreadId} not found", threadId);
                throw new KeyNotFoundException($"Thread {threadId} not found");
            }

            if (!threadActions.TryGetValue(actionId, out var action))
            {
                _logger?.LogWarning("[MOCK] Action {ActionId} not found in thread {ThreadId}", actionId, threadId);
                throw new KeyNotFoundException($"Action {actionId} not found in thread {threadId}");
            }

            return Task.FromResult(action);
        }

        /// <summary>
        /// Gets all actions for a thread
        /// </summary>
        public Task<IEnumerable<Action>> GetAllActions(Guid threadId)
        {
            _logger?.LogInformation("[MOCK] Getting all actions for thread {ThreadId}", threadId);

            if (!_actionsByThread.TryGetValue(threadId, out var threadActions))
            {
                return Task.FromResult(Enumerable.Empty<Action>());
            }

            return Task.FromResult(threadActions.Values.AsEnumerable());
        }

        /// <summary>
        /// Clears all actions (for testing)
        /// </summary>
        public void ClearAll()
        {
            _logger?.LogInformation("[MOCK] Clearing all actions");
            _actionsByThread.Clear();
        }
    }
}
