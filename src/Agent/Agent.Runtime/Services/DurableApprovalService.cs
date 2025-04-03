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
    public class DurableApprovalService : IApprovalService
    {
        private readonly DurableTaskClient _durableTaskClient;
        private readonly ILogger<DurableApprovalService> _logger;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;


        public DurableApprovalService(DurableTaskClient durableTaskClient, IAgentOutboundCommunicationService agentOutboundCommunicationService, ILogger<DurableApprovalService> logger)
        {
            _durableTaskClient = durableTaskClient;
            _logger = logger;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
        }

        public async Task SubmitApprovalDecision(string approvalId, string user, ApprovalDecision status, string threadId, string orchestrationId)
        {
            _logger.LogInformation($"Processing approval decision for {approvalId} with status {status}");

            if (status == ApprovalDecision.Approved)
            {
                //todo - reconcile this approval status type with the new one introduced in core/models/api
                var approvalStatus = new ApprovalStatus(
                    approvalId,
                    StartTime: DateTime.UtcNow,
                    ApprovedTime: DateTime.UtcNow,
                    DecisionMaker: user,
                    ProcessedTime: null
                    );

                await _durableTaskClient.RaiseEventAsync(approvalId, "ApprovalEvent", approvalStatus);
                // Approval Message
                string approvalMessage = $"**✅ Operation Approved**\n\n" +
                                         $"- **Operation ID:** {approvalId}\n" +
                                         $"- **User:** {user}\n" +
                                         $"- **Timestamp:** {DateTime.UtcNow}";

                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(threadId, orchestrationId, new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, approvalMessage));
            }
            else if (status == ApprovalDecision.Rejected)
            {
                string rejectionMessage = $"**❌ Operation Rejected**\n\n" +
                       $"- **Operation ID:** {approvalId}\n" +
                       $"- **User:** {user}\n" +
                       $"- **Timestamp:** {DateTime.UtcNow}";

                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(threadId, orchestrationId, new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, rejectionMessage));
                throw new NotImplementedException("How do we handle rejections?");
            }
            else
            {
                throw new ArgumentException($"Invalid approval status: {status} for approvalId: {approvalId}");
            }
        }
    }
}

