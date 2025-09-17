// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services
{
    public interface IIncidentStatusMetricsService
    {
        Task<IncidentStatusMetrics> GetIncidentStatusMetricsAsync(DateTime? startTime, DateTime? endTime);
    }

    public class IncidentStatusMetricsService : IIncidentStatusMetricsService
    {
        private readonly IIncidentRepository _incidentRepository;
        private readonly ILogger<IncidentStatusMetricsService> _logger;

        public IncidentStatusMetricsService(
            IIncidentRepository incidentRepository,
            ILogger<IncidentStatusMetricsService> logger)
        {
            _incidentRepository = incidentRepository;
            _logger = logger;
        }

        public async Task<IncidentStatusMetrics> GetIncidentStatusMetricsAsync(DateTime? startTime, DateTime? endTime)
        {
            try
            {
                _logger.LogInternalInformation("Fetching all incidents from Cosmos DB for status metrics calculation.");

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

                return new IncidentStatusMetrics
                {
                    ActiveCount = activeCount,
                    MitigatedCount = mitigatedCount,
                    ResolvedCount = resolvedCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error occurred while calculating incident status metrics.");
                throw;
            }
        }
    }
}
