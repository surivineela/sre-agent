// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins
{
    public class ControlFlowV2Plugin : IControlFlowV2Plugin
    {
        private readonly IThreadRepository _repository;
        private readonly ILogger<ControlFlowV2Plugin> _logger;
        private readonly Guid _agentContextId;
        private readonly Guid _threadId;
        private readonly AgentContext _agentContext;
        private readonly IAgentOutboundCommunicationService _outboundCommunicationService;

        public ControlFlowV2Plugin(
            IThreadRepository repository,
            IAgentOutboundCommunicationService outboundCommunicationService,
            AgentContext context,
            ILogger<ControlFlowV2Plugin> logger)
        {
            _repository = repository;
            _logger = logger;
            _outboundCommunicationService = outboundCommunicationService;
            _agentContextId = context.Id;
            _threadId = context.ThreadId;
            _agentContext = context;
        }

        public async Task StartWait(string waitReason, TimeSpan? waitFor = null)
        {
            _logger.LogInformation($"Starting wait state for context {_agentContextId} thread {_threadId} with reason: {waitReason}");

            var agentContext = await _repository.GetAgentContextAsync(agentContextId: _agentContextId, threadId: _threadId);
            var updatedAgentContext = agentContext with
            {
                ContextState = ContextStateEnum.Waiting,
                WaitInformation = new WaitInformation(
                    WaitUntil: !waitFor.HasValue ? null : DateTime.UtcNow.Add(waitFor.Value),
                    ResponseFromUserIsPending: false,
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

        public async Task Complete()
        {
            _logger.LogInformation($"Completing context {_agentContextId} thread {_threadId}");

            var agentContext = await _repository.GetAgentContextAsync(agentContextId: _agentContextId, threadId: _threadId);
            var updatedAgentContext = agentContext with
            {
                ContextState = ContextStateEnum.Completed,
                WaitInformation = null,
                ApprovalInformation = null
            };

            await _repository.UpdateAgentContextAsync(updatedAgentContext);
        }

        public async Task AskForUserInput(string message)
        {
            _logger.LogInformation(
                "User input needed, setting wait state for context {AgentContextId} thread {ThreadId} with agent message: {message}",
                _agentContextId, _threadId, message);

            var agentContext = await _repository.GetAgentContextAsync(agentContextId: _agentContextId, threadId: _threadId);
            var updatedAgentContext = agentContext with
            {
                ContextState = ContextStateEnum.Waiting,
                WaitInformation = new WaitInformation(
                    WaitUntil: null,
                    ResponseFromUserIsPending: true,
                    Reason: message)
            };

            await _repository.UpdateAgentContextAsync(updatedAgentContext);
        }

        public Task NotifyUser(string message)
        {
            return _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_agentContext, new(ChatRole.Assistant, message));
        }
    }
}
