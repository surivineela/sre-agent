// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Services;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MetricsController : ControllerBase
    {
        private readonly IThreadRepository _repository;
        private readonly IIncidentStatusMetricsService _incidentStatusMetricsService;
        private readonly ILogger<MetricsController> _logger;

        public MetricsController(
            IThreadRepository repository,
            ILogger<MetricsController> logger,
            IIncidentStatusMetricsService incidentStatusMetricsService)
        {
            _repository = repository;
            _incidentStatusMetricsService = incidentStatusMetricsService;
            _logger = logger;
        }

        [HttpGet("actionSeverity")]
        [AuthorizeArmOperation(ArmOperations.AgentThreadReadActionId)]
        public async Task<ActionResult<actionSeverityMetrics>> GetActionSeverityMetrics([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime)
        {
            _logger.LogInternalInformation("Getting action severity metrics from {StartTime} to {EndTime}", startTime, endTime);

            // Get all actions across all threads to calculate metrics
            var actions = await _repository.GetAllActionsAsync();

            if (startTime.HasValue)
            {
                actions = actions.Where(a => a.TimeStamp >= startTime.Value);
            }

            if (endTime.HasValue)
            {
                actions = actions.Where(a => a.TimeStamp <= endTime.Value);
            }

            // Filter executed actions by status,
            // for this metric we only return pending actions that users need to take action on
            var pendingActions = actions.Where(a => a.Status == ActionStatus.Pending).ToList();

            var criticalActionsCount = pendingActions.Count(a => a.Severity == ActionSeverity.Critical);
            var warningActionsCount = pendingActions.Count(a => a.Severity == ActionSeverity.Warning);

            // Construct response
            var metrics = new actionSeverityMetrics(
                CriticalActionsCount: criticalActionsCount,
                WarningActionsCount: warningActionsCount
            );

            return Ok(metrics);
        }

        [HttpGet("actionStatus")]
        [AuthorizeArmOperation(ArmOperations.AgentThreadReadActionId)]
        public async Task<ActionResult<actionStatusMetrics>> GetActionStatusMetrics([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime)
        {
            _logger.LogInternalInformation("Getting status metrics for actions");

            // Get all actions across all threads
            var actions = await _repository.GetAllActionsAsync();

            if (startTime.HasValue)
            {
                actions = actions.Where(a => a.TimeStamp >= startTime.Value);
            }

            if (endTime.HasValue)
            {
                actions = actions.Where(a => a.TimeStamp <= endTime.Value);
            }

            // Calculate metrics
            var completedCount = actions.Count(a => a.Status == ActionStatus.Completed);
            var failedCount = actions.Count(a => a.Status == ActionStatus.Failed);
            var pendingCount = actions.Count(a => a.Status == ActionStatus.Pending);
            var inProgressCount = actions.Count(a => a.Status == ActionStatus.InProgress);

            // Construct response
            var metrics = new actionStatusMetrics(
                PendingActionsCount: pendingCount,
                InProgressActionsCount: inProgressCount,
                CompletedActionsCount: completedCount,
                FailedActionsCount: failedCount
            );

            return Ok(metrics);
        }

        [HttpGet("incidentStatus")]
        [AuthorizeArmOperation(ArmOperations.AgentIncidentManagementReadActionId)]
        public async Task<IActionResult> GetIncidentStatusMetrics([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime)
        {
            try
            {
                var metrics = await _incidentStatusMetricsService.GetIncidentStatusMetricsAsync(startTime, endTime);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error occurred while fetching incident status metrics.");
                return StatusCode(500, "An error occurred while fetching incident status metrics.");
            }
        }

        public class IncidentStatusMetrics
        {
            public int ActiveCount { get; set; }
            public int MitigatedCount { get; set; }
            public int ResolvedCount { get; set; }
        }


        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum PagerDutyIncidentStatus
        {
            Triggered,
            Acknowledged,
            Resolved
        }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum AzMonIncidentStatus
        {
            New,
            Acknowledged,
            Closed
        }
    }
}
