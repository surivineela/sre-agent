// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Action = Agent.Core.Models.Api.v1.Action;
using Agent.Core.Interfaces;
using Agent.Core.Helpers;
using Agent.Data.Repositories;
using System.Text.Json.Serialization;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MetricsController : ControllerBase
    {
        private readonly IThreadRepository _repository;
        private readonly IIncidentRepository _incidentRepository;
        private readonly ILogger<MetricsController> _logger;

        public MetricsController(
            IThreadRepository repository,
            ILogger<MetricsController> logger,
            IIncidentRepository incidentRepository)
        {
            _repository = repository;
            _incidentRepository = incidentRepository;
            _logger = logger;
            _incidentRepository = incidentRepository;
        }

        [HttpGet("actionSeverity")]
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
        public async Task<IActionResult> GetIncidentStatusMetrics([FromQuery] DateTime? startTime, [FromQuery] DateTime? endTime)
        {
            try
            {
                _logger.LogInternalInformation("Fetching all incidents from Cosmos DB.");

                // Get all pager duty incidents
                var pagerDutyIncidents = await _incidentRepository.GetAllPagerDutyIncidentsAsync();
                var azMonIncidents = await _incidentRepository.GetAllAzMonIncidentsAsync();

                var activeCount = 0;
                var mitigatedCount = 0;
                var resolvedCount = 0;

                if (startTime.HasValue)
                {
                    pagerDutyIncidents = pagerDutyIncidents.Where(i => i.CreatedAt >= startTime.Value).ToList();
                    azMonIncidents = azMonIncidents.Where(i => i.CreatedAt >= startTime.Value).ToList();
                }

                if (endTime.HasValue)
                {
                    pagerDutyIncidents = pagerDutyIncidents.Where(i => i.CreatedAt <= endTime.Value).ToList();
                    azMonIncidents = azMonIncidents.Where(i => i.CreatedAt <= endTime.Value).ToList();
                }

                if (pagerDutyIncidents != null)
                {
                    // pager duty incident status: triggered, acknowledged, resolved (no mitigated)
                    activeCount += pagerDutyIncidents.Count(i =>
                        Enum.TryParse<PagerDutyIncidentStatus>(i.Status, true, out var status) &&
                        (status == PagerDutyIncidentStatus.Triggered || status == PagerDutyIncidentStatus.Acknowledged));
                    resolvedCount += pagerDutyIncidents.Count(i =>
                        Enum.TryParse<PagerDutyIncidentStatus>(i.Status, true, out var status) &&
                        status == PagerDutyIncidentStatus.Resolved); // No explicit "mitigated" status
                }

                if (azMonIncidents != null)
                {
                    // az monitor incident status: new, acknowledged, closed (no mitigated)
                    activeCount += azMonIncidents.Count(i =>
                        Enum.TryParse<AzMonIncidentStatus>(i.Status, true, out var status) &&
                        (status == AzMonIncidentStatus.New || status == AzMonIncidentStatus.Acknowledged));
                    resolvedCount += azMonIncidents.Count(i =>
                        Enum.TryParse<AzMonIncidentStatus>(i.Status, true, out var status) &&
                        status == AzMonIncidentStatus.Closed);
                }

                _logger.LogInternalInformation("Successfully calculated incident status metrics.");

                // Return the metrics
                return Ok(new IncidentStatusMetrics
                {
                    ActiveCount = activeCount,
                    MitigatedCount = mitigatedCount,
                    ResolvedCount = resolvedCount
                });
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
