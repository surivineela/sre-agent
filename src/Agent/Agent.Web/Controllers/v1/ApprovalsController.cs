// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ApprovalsController : ControllerBase
    {
        private readonly IApprovalService _approvalService;
        private readonly ILogger<ApprovalsController> _logger;
        private readonly IThreadRepository _threadRepository;

        public ApprovalsController(
            IApprovalService approvalService,
            IThreadRepository threadRepository,
            ILogger<ApprovalsController> logger)
        {
            _approvalService = approvalService;
            _threadRepository = threadRepository;
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
            _logger.LogInternalInformation("GET approval requested for thread {ThreadId} with filter: {Filter}", threadId, filter);

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
            _logger.LogInternalInformation("GET approval requested for thread {ThreadId} with ID: {Id}", threadId, id);

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
            _logger.LogInternalInformation("Submitting approval decision for thread {ThreadId} with ID: {Id}, Status: {Status}",
                threadId, id, request.Status);

            if (!Enum.TryParse<ApprovalDecision>(request.Status, true, out var approvalStatus))
            {
                return BadRequest(new { error = $"Invalid status value: {request.Status}" });
            }

            // Get header from http request
            var authzHeader = Request.Headers["Authorization"].ToString();
            string? oboToken = null;
            string? userEmail = null;
            string? userName = null;
            string? userId = null;
            DateTime approvalTimestamp = DateTime.UtcNow;
            string approver = request.User ?? string.Empty;

            if (!string.IsNullOrEmpty(authzHeader) && authzHeader.StartsWith("Bearer "))
            {
                oboToken = authzHeader.Substring("Bearer ".Length).Trim();

                // Decode the token to get user information
                try
                {
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jsonToken = handler.ReadToken(oboToken) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;

                    if (jsonToken != null)
                    {
                        // Get user information from token claims
                        userEmail = jsonToken.Claims.FirstOrDefault(c => c.Type == "upn")?.Value
                            ?? jsonToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;

                        userId = jsonToken.Claims.FirstOrDefault(c => c.Type == "oid")?.Value;

                        var givenName = jsonToken.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
                        var familyName = jsonToken.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value;

                        if (!string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(familyName))
                        {
                            userName = $"{givenName} {familyName}";
                        }
                        else
                        {
                            userName = jsonToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
                        }

                        // Get the issued at time from the token
                        var iatClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "iat")?.Value;
                        if (long.TryParse(iatClaim, out long iatUnixTimestamp))
                        {
                            // Convert Unix timestamp to DateTime
                            approvalTimestamp = DateTimeOffset.FromUnixTimeSeconds(iatUnixTimestamp).UtcDateTime;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to decode JWT token");
                }
            }

            if (!string.IsNullOrEmpty(userEmail) &&
                !string.IsNullOrEmpty(userName))
            {
                approver = $"{userName} <{userEmail}>";
            }

            try
            {
                await _approvalService.SubmitApprovalDecision(id, approver, approvalStatus, Guid.Parse(threadId), oboToken, request.Scope);

                return Ok(new
                {
                    decisionMaker = approver,
                    decisionMakerName = userName,
                    decisionMakerId = userId,
                    decisionTimestamp = approvalTimestamp,
                    status = approvalStatus.ToString()
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot re-approve"))
            {
                // Get the approval to return additional information
                var approval = await _threadRepository.GetApprovalAsync(Guid.Parse(threadId), Guid.Parse(id));

                return Conflict(new
                {
                    error = ex.Message,
                    decisionMaker = approval?.DecisionUser?.UserId ?? approval?.DecisionUser?.DisplayName,
                    decisionMakerName = approval?.DecisionUser?.DisplayName,
                    decisionMakerId = approval?.DecisionUser?.UserId,
                    decisionTimestamp = approval?.DecisionTimestamp,
                    status = approval?.Status.ToString()
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Requested scope mismatch"))
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Approval not found") || ex.Message.Contains("OboToken not found"))
            {
                _logger.LogInternalError(ex, "Failed to process approval decision");
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to process approval decision");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Model for the approval decision request body
        /// </summary>
        public class ApprovalDecisionRequest
        {
            public string Status { get; set; }
            public string User { get; set; }
            public string? Scope { get; set; }
        }
    }
}

