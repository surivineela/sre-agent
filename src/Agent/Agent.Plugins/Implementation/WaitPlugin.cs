// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Definitions;
using Azure;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins
{
    public class WaitPlugin : IWaitPlugin
    {
        private readonly IThreadRepository _repository;
        private readonly ILogger<WaitPlugin> _logger;
        private readonly Guid _agentContextId;
        private readonly Guid _threadId;

        public WaitPlugin(IThreadRepository repository, Guid agentContextId, Guid threadId, ILogger<WaitPlugin> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _agentContextId = agentContextId;
            _threadId = threadId;
        }

        public async Task StartWait(string waitReason, DateTime? waitUntil = null)
        {
            _logger.LogInformation($"Starting wait state for context {_agentContextId} thread {_threadId} with reason: {waitReason}");

            var agentContext = await _repository.GetAgentContextAsync(agentContextId: _agentContextId, threadId: _threadId);
            var updatedAgentContext = agentContext with
            {
                ContextState = ContextStateEnum.Waiting,
                WaitInformation = new WaitInformation(
                    WaitUntil: waitUntil,
                    Reason: waitReason)
            };

            await _repository.UpdateAgentContextAsync(updatedAgentContext);
        }

        public async Task<WaitInformation?> GetWaitState()
        {
            var agentContext = await _repository.GetAgentContextAsync(agentContextId: _agentContextId, threadId: _threadId);
            return agentContext.WaitInformation;
        }

        public async Task CancelWait()
        {
            var agentContext = await _repository.GetAgentContextAsync(agentContextId: _agentContextId, threadId: _threadId);
            var updatedAgentContext = agentContext with
            {
                ContextState = ContextStateEnum.Processing,
                WaitInformation = null
            };

            await _repository.UpdateAgentContextAsync(updatedAgentContext);
        }
    }
}
