using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;
using System.Text.Json;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ApprovalsController : ControllerBase
    {
        private readonly IApprovalService _approvalService;
        private readonly ILogger<ApprovalsController> _logger;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

        public ApprovalsController(
            IApprovalService approvalService, 
            ILogger<ApprovalsController> logger,
            IAgentOutboundCommunicationService agentOutboundCommunicationService,
            DurableTaskClient durableTaskClient)
        {
            _approvalService = approvalService;
            _logger = logger;
            _durableTaskClient = durableTaskClient;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
        }

        /// <summary>
        /// Gets approvals with optional filtering
        /// </summary>
        /// <param name="filter">ODATA filter query</param>
        /// <returns>List of approvals</returns>
        [HttpGet]
        public async Task<IActionResult> GetApprovals([FromQuery] string filter = null)
        {
            // TODO: Implement pagination and filtering logic
            _logger.LogInformation("GET approvals requested with filter: {Filter}", filter);

            var approvals = new List<Approval>();
            await foreach (var orchestrationMetadata in _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running },
                InstanceIdPrefix = "approval"
            }))
            {
                var approvalOrchestration = await _durableTaskClient.GetInstanceAsync(orchestrationMetadata.InstanceId, true, CancellationToken.None);
                approvals.Add(new Approval(approvalOrchestration.InstanceId, approvalOrchestration.ReadInputAs<ApprovalInput>()?.OperationName ?? "Approval", ApprovalDecision.Pending, approvalOrchestration.CreatedAt.DateTime, null, null));
            }

            return Ok(approvals);
        }

        /// <summary>
        /// Gets a specific approval by ID
        /// </summary>
        /// <param name="id">Approval ID</param>
        /// <returns>The approval if found</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetApproval(string id)
        {
            // TODO: Implement getting a specific approval
            _logger.LogInformation("GET approval requested with ID: {Id}", id);
            
            // Stub implementation - Return sample data
            var approval = new Approval(
                Id: id,
                Title: "Sample approval",
                Status: ApprovalDecision.Pending,
                CreatedTimestamp: DateTime.UtcNow.AddDays(-1),
                DecisionTimestamp: null,
                decisionUserId: null);
            
            return Ok(approval);
        }

        /// <summary>
        /// Submit a decision for an approval
        /// </summary>
        /// <param name="id">Approval ID</param>
        /// <param name="request">Decision request</param>
        /// <returns>Success or error status</returns>
        [HttpPost("{id}/decision")]
        public async Task<IActionResult> SubmitApprovalDecision(string id, [FromBody] ApprovalDecisionRequest request)
        {
            _logger.LogInformation("Submitting approval decision for ID: {Id}, Status: {Status}", 
                id, request.Status);

            var runningApprovalOrchestrations = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running },
                InstanceIdPrefix = "approval"
            }).ToListAsync();

            var threadId = "";
            var orchestrationId = "";
            foreach (var orchestration in runningApprovalOrchestrations)
            {
                var orchestrationInstance = await _durableTaskClient.GetInstanceAsync(orchestration.InstanceId, getInputsAndOutputs: true);
                if (orchestrationInstance is not null)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(orchestrationInstance.SerializedInput))
                        {
                            ApprovalInput approvalInput = JsonSerializer.Deserialize<ApprovalInput>(orchestrationInstance.SerializedInput);

                            if (approvalInput != null && approvalInput.ApprovalId.Equals(id, StringComparison.OrdinalIgnoreCase))
                            {
                                threadId = approvalInput.ThreadId;
                                orchestrationId = approvalInput.ParentInstanceId;
                                break;
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Orchestration with empty input", orchestration.InstanceId);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize input for instance {InstanceId}", orchestration.InstanceId);
                    }
                }
            }

            if (!Enum.TryParse<ApprovalDecision>(request.Status, true, out var approvalStatus))
            {
                return BadRequest(new { error = $"Invalid status value: {request.Status}" });
            }

            await _approvalService.SubmitApprovalDecision(id, request.User, approvalStatus, threadId, orchestrationId);
            return Ok();
        }

        /// <summary>
        /// Model for the approval decision request body
        /// </summary>
        public class ApprovalDecisionRequest
        {
            public string Status { get; set; }
            public string User { get; set; }
        }
    }
}
