// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Data.DataModels;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

public class InMemoryIncidentRepository : IIncidentRepository
{
    private readonly ConcurrentDictionary<string, PagerDutyIncidentDocument> _pagerDutyIncidents = new();
    private readonly ConcurrentDictionary<string, AzMonitorAlertDocument> _azMonIncidents = new();
    private readonly ILogger<InMemoryIncidentRepository> _logger;

    public InMemoryIncidentRepository(ILogger<InMemoryIncidentRepository> logger)
    {
        _logger = logger;
    }

    public Task<List<PagerDutyIncidentDocument>> GetAllPagerDutyIncidentsAsync()
    {
        _logger.LogInternalInformation("Fetching all PagerDuty incidents from in-memory store.");

        var incidents = _pagerDutyIncidents.Values
            .OrderByDescending(doc => doc.CreatedAt)
            .ToList();

        _logger.LogInternalInformation("Fetched {Count} PagerDuty incidents from in-memory store.", incidents.Count);
        return Task.FromResult(incidents);
    }

    public Task<List<AzMonitorAlertDocument>> GetAllAzMonIncidentsAsync()
    {
        _logger.LogInternalInformation("Fetching all AzMon incidents from in-memory store.");

        var incidents = _azMonIncidents.Values
            .OrderByDescending(doc => doc.CreatedAt)
            .ToList();

        _logger.LogInternalInformation("Fetched {Count} AzMon incidents from in-memory store.", incidents.Count);
        return Task.FromResult(incidents);
    }

    // Helper methods for adding/updating incidents (useful for testing or seeding data)
    public void AddOrUpdatePagerDutyIncident(PagerDutyIncidentDocument incident)
    {
        if (incident?.Id != null)
        {
            _pagerDutyIncidents.AddOrUpdate(incident.Id, incident, (key, oldValue) => incident);
        }
    }

    public void AddOrUpdateAzMonIncident(AzMonitorAlertDocument incident)
    {
        if (incident?.Id != null)
        {
            _azMonIncidents.AddOrUpdate(incident.Id, incident, (key, oldValue) => incident);
        }
    }

    public void Clear()
    {
        _pagerDutyIncidents.Clear();
        _azMonIncidents.Clear();
    }
}
