// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Reasoning;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services
{
    public class ApprovalService : IApprovalService
    {
        private readonly DurableTaskClient _durableTaskClient;
        private readonly ILogger<ApprovalService> _logger;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
        private readonly IThreadRepository _threadRepository;
        private readonly CoreSettings _coreSettings;
        private readonly IReasoningLoopManager _reasoningLoopManager;

        public ApprovalService(DurableTaskClient durableTaskClient,
            IAgentOutboundCommunicationService agentOutboundCommunicationService,
            ILogger<ApprovalService> logger,
            IThreadRepository threadRepository,
            CoreSettings coreSettings,
            IReasoningLoopManager reasoningLoopManager)
        {
            _durableTaskClient = durableTaskClient;
            _logger = logger;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
            _threadRepository = threadRepository;
            _coreSettings = coreSettings;
            _reasoningLoopManager = reasoningLoopManager;
        }

        public async Task<Approval> GetApproval(Guid threadId, string approvalId)
        {
            _logger.LogInternalInformation($"Getting approval for thread {threadId} with approval id {approvalId}");

            var approval = await _threadRepository.GetApprovalAsync(threadId, Guid.Parse(approvalId));
            if (approval == null)
            {
                _logger.LogInternalError($"Approval is not found with id {approvalId}", approvalId);
                throw new InvalidOperationException($"Approval is not found");
            }
            return approval;
        }

        public async Task<IList<Approval>> GetApprovals(Guid threadId)
        {
            _logger.LogInternalInformation($"Getting approvals for thread {threadId}");

            return await _threadRepository.GetApprovalsAsync(threadId);
        }

        public async Task SubmitApprovalDecision(string approvalId,
            string user,
            ApprovalDecision status,
            Guid threadId,
            string? oboToken = null,
            string? scope = null)
        {
            _logger.LogInternalInformation($"Processing approval decision for {approvalId} with status {status} and scope {scope}");

            var approval = await _threadRepository.GetApprovalAsync(threadId, Guid.Parse(approvalId));

            if (approval == null)
            {
                throw new KeyNotFoundException("Approval not found");
            }

            if (scope != approval.OboTokenScope)
            {
                throw new InvalidOperationException($"Requested scope mismatch: expected {approval.OboTokenScope}, received {scope}");
            }
            if (approval.Status != ApprovalDecision.Pending && approval.Status != ApprovalDecision.PendingAuthorization)
            {
                // Create detailed exception with information about the previous approval
                var errorMessage = $"Cannot re-approve. This operation was already {approval.Status} by {approval.DecisionUser?.DisplayName ?? "unknown"} on {approval.DecisionTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "unknown date"}";

                // Create a custom exception or use a specific data structure for the error
                throw new InvalidOperationException(errorMessage);
            }

            if ((approval.Status == ApprovalDecision.Pending && status != ApprovalDecision.Approved && status != ApprovalDecision.Cancelled)
                || (approval.Status == ApprovalDecision.PendingAuthorization && status != ApprovalDecision.Authorized && status != ApprovalDecision.Cancelled))
            {
                throw new InvalidOperationException("Invalid approval decision for current approval status");
            }

            AgentContext? agentContext = null;

            if (approval.AgentContextId != null)
            {
                agentContext = await _threadRepository.GetAgentContextAsync(approval.AgentContextId.Value, threadId);
            }

            if (agentContext == null)
            {
                throw new InvalidOperationException("Agent Context ID is required for approval message.");
            }

            var newApproval = approval with
            {
                DecisionTimestamp = DateTime.UtcNow,
                DecisionUser = new Author(Role.User, user, user),
                Status = status,
            };

            if (status == ApprovalDecision.Authorized)
            {
                newApproval = newApproval with
                {
                    OboToken = oboToken,
                };
            }

            await _threadRepository.UpdateApprovalAsync(newApproval);

            if (_coreSettings.UseAgentFramework && agentContext != null)
            {
                await _reasoningLoopManager.NotifyApprovalDecisionAsync(agentContext, newApproval, CancellationToken.None);
            }
        }
    }
}

