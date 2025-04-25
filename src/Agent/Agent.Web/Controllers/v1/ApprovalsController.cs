// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Runtime;
using Agent.Runtime.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;
using Microsoft.Graph.Privacy.SubjectRightsRequests.Item.Approvers;
using System.Text.Json;
using System.Threading;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ApprovalsController : ControllerBase
    {
        private readonly IApprovalService _approvalService;
        private readonly ILogger<ApprovalsController> _logger;

        public ApprovalsController(
            IApprovalService approvalService, 
            ILogger<ApprovalsController> logger)
        {
            _approvalService = approvalService;
            _logger = logger;
        }

        /// <summary>
        /// Gets approvals with optional filtering
        /// </summary>
        /// <param name="filter">ODATA filter query</param>
        /// <returns>List of approvals</returns>
        [HttpGet("{threadId}")]
        public async Task<IActionResult> GetApprovals(string threadId, [FromQuery] string filter = null)
        {
            // TODO: Implement pagination and filtering logic
            _logger.LogInformation("GET approval requested for thread {ThreadId} with filter: {Filter}", threadId, filter);

            var approvals = await _approvalService.GetApprovals(Guid.Parse(threadId));

            return Ok(approvals);
        }

        /// <summary>
        /// Gets a specific approval by ID
        /// </summary>
        /// /// <param name="threadId">Thread ID</param>
        /// <param name="id">Approval ID</param>
        /// <returns>The approval if found</returns>
        [HttpGet("{threadId}/{id}")]
        public async Task<IActionResult> GetApproval(string threadId, string id)
        {
            // TODO: Implement getting a specific approval
            _logger.LogInformation("GET approval requested for thread {ThreadId} with ID: {Id}", threadId, id);
            
            var approval = await _approvalService.GetApproval(Guid.Parse(threadId), id);

            return Ok(approval);
        }

        /// <summary>
        /// Submit a decision for an approval
        /// </summary>
        /// <param name="threadId">Thread ID</param>
        /// <param name="id">Approval ID</param>
        /// <param name="request">Decision request</param>
        /// <returns>Success or error status</returns>
        [HttpPost("{threadId}/{id}/decision")]
        public async Task<IActionResult> SubmitApprovalDecision(string threadId, string id, [FromBody] ApprovalDecisionRequest request)
        {
            _logger.LogInformation("Submitting approval decision for thread {ThreadId} with ID: {Id}, Status: {Status}",
                threadId, id, request.Status);

            if (!Enum.TryParse<ApprovalDecision>(request.Status, true, out var approvalStatus))
            {
                return BadRequest(new { error = $"Invalid status value: {request.Status}" });
            }

            // Get header from http request
            var authzHeader = Request.Headers["Authorization"].ToString();
            string? oboToken = null;
            if (!string.IsNullOrEmpty(authzHeader) && authzHeader.StartsWith("Bearer "))
            {
                oboToken = authzHeader.Substring("Bearer ".Length).Trim();
            }

            await _approvalService.SubmitApprovalDecision(id, request.User, approvalStatus, Guid.Parse(threadId), oboToken);
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

