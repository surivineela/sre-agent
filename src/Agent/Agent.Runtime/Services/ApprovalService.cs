// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services
{
    public class ApprovalService : IApprovalService
    {
        private readonly DurableTaskClient _durableTaskClient;
        private readonly ILogger<ApprovalService> _logger;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
        private readonly IThreadRepository _threadRepository;


        public ApprovalService(DurableTaskClient durableTaskClient,
            IAgentOutboundCommunicationService agentOutboundCommunicationService,
            ILogger<ApprovalService> logger,
            IThreadRepository threadRepository)
        {
            _durableTaskClient = durableTaskClient;
            _logger = logger;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
            _threadRepository = threadRepository;
        }

        public async Task<Approval> GetApproval(Guid threadId, string approvalId)
        {
            _logger.LogInformation($"Getting approval for thread {threadId} with approval id {approvalId}");

            return await _threadRepository.GetApprovalAsync(threadId, Guid.Parse(approvalId));
        }

        public async Task<IList<Approval>> GetApprovals(Guid threadId)
        {
            _logger.LogInformation($"Getting approvals for thread {threadId}");

            return await _threadRepository.GetApprovalsAsync(threadId);
        }

        public async Task SubmitApprovalDecision(string approvalId,
            string user,
            ApprovalDecision status,
            Guid threadId,
            string? oboToken = null)
        {
            _logger.LogInformation($"Processing approval decision for {approvalId} with status {status}");

            var approval = await _threadRepository.GetApprovalAsync(threadId, Guid.Parse(approvalId));

            if (approval == null)
            {
                throw new KeyNotFoundException("Approval not found");
            }
            if (approval.Status != ApprovalDecision.Pending)
            {
                // Create detailed exception with information about the previous approval
                var errorMessage = $"Cannot re-approve. This operation was already {approval.Status} by {approval.DecisionUser?.DisplayName ?? "unknown"} on {approval.DecisionTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "unknown date"}";

                // Create a custom exception or use a specific data structure for the error
                throw new InvalidOperationException(errorMessage);
            }

            AgentContext? subAgentV2Context = null;
            string? orchestrationId = approval.OrchestrationId;

            if (approval.AgentContextId != null)
            {
                subAgentV2Context = await _threadRepository.GetAgentContextAsync(approval.AgentContextId.Value, threadId);
            }
            else
            {
                orchestrationId = approval.OrchestrationId;
            }

            if (subAgentV2Context == null && orchestrationId == null)
            {
                throw new InvalidOperationException("Orchestration ID or Agent Context ID is required for approval message.");
            }

            if (status == ApprovalDecision.Approved)
            {
                // Approval Message
                string approvalMessage = $"**✅ Operation Approved**\n\n" +
                                         $"- **Operation ID:** {approvalId}\n" +
                                         $"- **User:** {user}\n" +
                                         $"- **Timestamp:** {DateTime.UtcNow}";

                if (subAgentV2Context != null)
                {
                    await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(subAgentV2Context, new ChatMessage(ChatRole.Assistant, approvalMessage));
                }
                else if (orchestrationId != null)
                {
                    await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(threadId, orchestrationId, new ChatMessage(ChatRole.Assistant, approvalMessage));
                }
            }
            else if (status == ApprovalDecision.Rejected)
            {
                string rejectionMessage = $"**❌ Operation Rejected**\n\n" +
                       $"- **Operation ID:** {approvalId}\n" +
                       $"- **User:** {user}\n" +
                       $"- **Timestamp:** {DateTime.UtcNow}";

                if (subAgentV2Context != null)
                {
                    await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(subAgentV2Context, new ChatMessage(ChatRole.Assistant, rejectionMessage));
                }
                else if (orchestrationId != null)
                {
                    await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(threadId, orchestrationId, new ChatMessage(ChatRole.Assistant, rejectionMessage));
                }
            }
            else
            {
                throw new ArgumentException($"Invalid approval status: {status} for approvalId: {approvalId}");
            }


            if (!string.IsNullOrEmpty(orchestrationId))
            {
                //todo - reconcile this approval status type with the new one introduced in core/models/api
                var approvalStatus = new ApprovalStatus(
                    approvalId,
                    StartTime: DateTime.UtcNow,
                    ApprovedTime: DateTime.UtcNow,
                    DecisionMaker: user,
                    ProcessedTime: null,
                    OboToken: status == ApprovalDecision.Approved ? oboToken : null
                    );

                await _durableTaskClient.RaiseEventAsync(orchestrationId, "ApprovalEvent", approvalStatus);
            }

            var newApproval = approval with
            {
                DecisionTimestamp = DateTime.UtcNow,
                DecisionUser = new Author(Role.User, user, user),
                Status = status,
                OboToken = oboToken,
            };

            await _threadRepository.UpdateApprovalAsync(newApproval);
        }
    }
}

