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

