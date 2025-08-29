// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Runtime.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ApprovalsV2Controller : ControllerBase
    {
        private readonly IApprovalService _approvalService;
        private readonly ILogger<ApprovalsController> _logger;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
        private readonly IThreadRepository _threadRepository;

        public ApprovalsV2Controller(
            IApprovalService approvalService,
            ILogger<ApprovalsController> logger,
            IAgentOutboundCommunicationService agentOutboundCommunicationService,
            IThreadRepository threadRepository)
        {
            _approvalService = approvalService;
            _logger = logger;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
            _threadRepository = threadRepository;
        }

        /// <summary>
        /// Gets approvals with optional filtering
        /// </summary>
        /// <param name="filter">ODATA filter query</param>
        /// <returns>List of approvals</returns>
        [HttpGet]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: This controller is not used
        public async Task<IActionResult> GetApprovals()
#pragma warning restore CUSTOM004
        {
            // TODO: Implement pagination
            _logger.LogInternalInformation("GET approvals requested");

            var approvals = await _threadRepository.GetAllApprovalV2sAsync();
            return Ok(approvals);
        }

        /// <summary>
        /// Gets a specific approval by ID
        /// </summary>
        /// <param name="id">Approval ID</param>
        /// <returns>The approval if found</returns>
        [HttpGet("{id}")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: This controller is not used
        public async Task<IActionResult> GetApproval(string id, [FromQuery] string agentContextId)
#pragma warning restore CUSTOM004
        {
            // TODO: Implement getting a specific approval
            _logger.LogInternalInformation("GET approval requested with ID: {Id}, AgentContextId: {agentContextId}", id, agentContextId);

            // Stub implementation - Return sample data
            var approval = await _threadRepository.GetApprovalV2Async(approvalIdV2: Guid.Parse(id), agentContextId: Guid.Parse(agentContextId));

            return Ok(approval);
        }

        /// <summary>
        /// Submit a decision for an approval
        /// </summary>
        /// <param name="id">Approval ID</param>
        /// <param name="request">Decision request</param>
        /// <returns>Success or error status</returns>
        [HttpPost("{id}/decision")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: This controller is not used
        public async Task<IActionResult> SubmitApprovalDecision(string id, [FromQuery] string agentContextId, [FromBody] ApprovalDecisionRequest request)
#pragma warning restore CUSTOM004
        {
            _logger.LogInternalInformation("Submitting approval decision for ID: {Id}, Status: {Status}",
                id, request.Status);

            var existingApprovalV2 = await _threadRepository.GetApprovalV2Async(approvalIdV2: Guid.Parse(id), agentContextId: Guid.Parse(agentContextId));
            if (existingApprovalV2 == null)
            {
                return NotFound();
            }

            if (!Enum.TryParse<ApprovalDecision>(request.Status, true, out var approvalStatus))
            {
                return BadRequest(new { error = $"Invalid status value: {request.Status}" });
            }

            if (approvalStatus == ApprovalDecision.Approved)
            {
                //todo - reconcile this approval status type with the new one introduced in core/models/api
                existingApprovalV2 = existingApprovalV2 with
                {
                    Status = ApprovalDecision.Approved,
                    DecisionTimestamp = existingApprovalV2.DecisionTimestamp,
                    DecisionUserId = request.User
                };

                await _threadRepository.UpdateApprovalV2Async(existingApprovalV2);

                var existingAgentContext = await _threadRepository.GetAgentContextAsync(agentContextId: existingApprovalV2.AgentContextId, threadId: existingApprovalV2.ThreadId);
                if (existingAgentContext == null)
                {
                    throw new Exception($"Agent context not found for AgentContextId: {existingApprovalV2.AgentContextId} and ThreadId: {existingApprovalV2.ThreadId}");
                }
                existingAgentContext = existingAgentContext with
                {
                    ContextState = ContextStateEnum.Idle
                };

                await _threadRepository.UpdateAgentContextAsync(existingAgentContext);

                // Approval Message
                string approvalMessage = $"**✅ Operation Approved**\n\n" +
                                         $"- **Operation ID:** {id}\n" +
                                         $"- **User:** {request.User}\n" +
                $"- **Timestamp:** {DateTime.UtcNow}";

                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(existingApprovalV2.ThreadId, orchestrationInstanceId: string.Empty, new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, approvalMessage));
            }
            else if (approvalStatus == ApprovalDecision.Cancelled)
            {
                //todo - reconcile this approval status type with the new one introduced in core/models/api
                existingApprovalV2 = existingApprovalV2 with
                {
                    Status = ApprovalDecision.Cancelled,
                    DecisionTimestamp = existingApprovalV2.DecisionTimestamp,
                    DecisionUserId = request.User
                };

                await _threadRepository.UpdateApprovalV2Async(existingApprovalV2);

                var existingAgentContext = await _threadRepository.GetAgentContextAsync(agentContextId: existingApprovalV2.AgentContextId, threadId: existingApprovalV2.ThreadId);
                if (existingAgentContext == null)
                {
                    throw new Exception($"Agent context not found for AgentContextId: {existingApprovalV2.AgentContextId} and ThreadId: {existingApprovalV2.ThreadId}");
                }
                existingAgentContext = existingAgentContext with
                {
                    ContextState = ContextStateEnum.Failed
                };

                await _threadRepository.UpdateAgentContextAsync(existingAgentContext);

                string rejectionMessage = $"**❌ Operation Rejected**\n\n" +
                       $"- **Operation ID:** {id}\n" +
                       $"- **User:** {request.User}\n" +
                $"- **Timestamp:** {DateTime.UtcNow}";

                await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(existingApprovalV2.ThreadId, orchestrationInstanceId: string.Empty, new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, rejectionMessage));
            }
            else
            {
                throw new ArgumentException($"Invalid approval status: {approvalStatus} for approvalId: {id}");
            }

            return Ok();
        }

        /// <summary>
        /// Model for the approval decision request body
        /// </summary>
        public class ApprovalDecisionRequest
        {
            public required string Status { get; set; }
            public required string User { get; set; }
        }
    }
}

