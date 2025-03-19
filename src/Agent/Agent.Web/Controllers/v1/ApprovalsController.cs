using System;
using System.Collections.Generic;
using System.Threading.Tasks;
//using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ApprovalsController : ControllerBase
    {
        private readonly IApprovalService _approvalService;
        private readonly ILogger<ApprovalsController> _logger;
        private readonly DurableTaskClient _durableTaskClient;

        public ApprovalsController(
            IApprovalService approvalService, 
            ILogger<ApprovalsController> logger,
            DurableTaskClient durableTaskClient)
        {
            _approvalService = approvalService;
            _logger = logger;
            _durableTaskClient = durableTaskClient;
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


            var approvalOrchestrations = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running },
                InstanceIdPrefix = "approval"
            }).ToListAsync();

            var approvals = new List<Approval>();

            if (approvalOrchestrations.Count == 0)
            {
                approvals.Add(new Approval(
                    Id: Guid.NewGuid().ToString(),
                    Title: "Sample Always On configuration setting update",
                    Status: ApprovalDecision.Pending,
                    CreatedTimestamp: DateTime.UtcNow,
                    DecisionTimestamp: null,
                    decisionUserId: null
                ));
            }

            approvals.AddRange(approvalOrchestrations
                .Select(x => new Approval(x.InstanceId, "placeholder title", ApprovalDecision.Pending, x.CreatedAt.DateTime, null, null))
                .ToList());

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

            if (!Enum.TryParse<ApprovalDecision>(request.Status, true, out var approvalStatus))
            {
                return BadRequest(new { error = $"Invalid status value: {request.Status}" });
            }

            await _approvalService.SubmitApprovalDecision(id, approvalStatus);

            return Ok();
        }

        /// <summary>
        /// Model for the approval decision request body
        /// </summary>
        public class ApprovalDecisionRequest
        {
            public string Status { get; set; }
        }
    }
}
