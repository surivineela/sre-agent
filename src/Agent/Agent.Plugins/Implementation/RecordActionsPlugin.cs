// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.Logging;
using Action = Agent.Core.Models.Api.v1.Action;


namespace Agent.Plugins
{
    public class RecordActionsPlugin : IRecordActionsPlugin
    {
        private readonly IThreadRepository _repository;
        private readonly ILogger<RecordActionsPlugin> _logger;

        public RecordActionsPlugin(IThreadRepository repository, ILogger<RecordActionsPlugin> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Action> RecordAction(Guid threadId, string title, string toolName = "", ActionStatus status = ActionStatus.Pending, ActionSeverity severity = ActionSeverity.Warning)
        {
            // Check if thread exists
            var thread = await _repository.GetThreadAsync(threadId);
            if (thread == null)
            {
                _logger.LogInternalWarning("Attempted to record action for non-existent thread: {ThreadId}", threadId);
                throw new ArgumentException($"Thread with ID {threadId} does not exist", nameof(threadId));
            }

            // Create new action
            var action = new Action(
                Id: Guid.NewGuid(),
                Title: title,
                ToolName: toolName,
                TimeStamp: DateTime.UtcNow,
                Status: status,
                Severity: severity
            );

            // Store the action
            await _repository.AddActionAsync(threadId, action);

            _logger.LogInternalInformation("Recorded action: {ActionId} - {Title} for thread {ThreadId}",
                action.Id, action.Title, threadId);

            return action;
        }

        public async Task<Action> GetAction(Guid threadId, Guid actionId)
        {
            var action = await _repository.GetActionAsync(threadId, actionId);
            if (action == null)
            {
                _logger.LogInternalWarning("Attempted to retrieve non-existent action: {ActionId} in thread {ThreadId}",
                    actionId, threadId);
                throw new ArgumentException($"Action with ID {actionId} does not exist in thread {threadId}");
            }

            return action;
        }
    }
}

