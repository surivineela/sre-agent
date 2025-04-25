// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Action = Agent.Core.Models.Api.v1.Action;
using Agent.Core.Interfaces;
using Agent.Core.Helpers;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ActionsController : ControllerBase
    {
        private readonly IThreadRepository _repository;
        private readonly ILogger<ActionsController> _logger;

        public ActionsController(
            IThreadRepository repository,
            ILogger<ActionsController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet("severityMetrics")]
        public async Task<ActionResult<actionSeverityMetrics>> GetActionSeverityMetrics([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime)
        {
            _logger.LogInformation("Getting action severity metrics from {StartTime} to {EndTime}", startTime, endTime);
            
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

        [HttpGet("statusMetrics")]
        public async Task<ActionResult<actionStatusMetrics>> GetActionStatusMetrics([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime)
        {
            _logger.LogInformation("Getting status metrics for actions");

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
    }
} 
