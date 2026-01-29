// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.ServiceNow;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

/// <summary>
/// Handles ServiceNow-specific incident processing
/// </summary>
public class ServiceNowIncidentHandlingService : IncidentHandlingService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload, ServiceNowIncident>
{
    private readonly IServiceNowAPIClient _serviceNowAPIClient;

    protected override IncidentManagementType IncidentType => IncidentManagementType.ServiceNow;

    public ServiceNowIncidentHandlingService(
        IServiceNowAPIClient serviceNowAPIClient,
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        IIncidentStatusMetricsService incidentStatusMetricsService,
        IAgentOutboundCommunicationService agentOutboundCommunicationService,
        IIncidentAnalysisService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload, ServiceNowIncident> incidentAnalysisService,
        ILogger<ServiceNowIncidentHandlingService> logger,
        Tracer tracer,
        IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IAgentFactory<AgentContext> agentFactory,
        ExperimentalSettings experimentalSettings,
        IReasoningLoopManager reasoningLoopManager
        )
        : base(repository, inboundCommunicationService, incidentFilterManagementService, incidentManagementService, incidentHandlerManagementService, incidentStatusMetricsService, agentOutboundCommunicationService, incidentAnalysisService, logger, tracer, agentFactory, experimentalSettings, reasoningLoopManager)
    {
        _serviceNowAPIClient = serviceNowAPIClient;
    }

    /// <summary>
    /// Override priority matching to use ServiceNow-specific normalization logic
    /// </summary>
    protected override bool IsPriorityMatch(IEnumerable<string>? filterPriorities, string incidentPriority)
    {
        if (filterPriorities is null || !filterPriorities.Any())
        {
            return true;
        }
        var normalizedFilterPriorities = ServiceNowPriorityHelper.NormalizePriorityForFiltering(filterPriorities);
        var normalizedIncidentPriorities = ServiceNowPriorityHelper.NormalizePriorityForFiltering(new string[] { incidentPriority });
        return normalizedFilterPriorities.Any(fp => normalizedIncidentPriorities.Contains(fp));
    }

    protected override async Task<Thread> CreateIncidentHandlerAgentThreadAsync(
        ServiceNowIncidentDocument incidentDetails,
        IncidentHandlerDocumentPayload incidentHandler,
        ServiceNowIncidentFilterDocumentPayload incidentFilter,
        IncidentHandlingRequestModelBase request)
    {
        _logger.LogInternalInformation("[ServiceNowIncidentHandlingService] CreateIncidentHandlerAgentThreadAsync: Delegating to base implementation for IncidentId: {IncidentId}", incidentDetails.Id);

        // ServiceNow has specific additional properties
        string GetServiceNowSpecificProperties(IIncidentDocument incident)
        {
            if (incident is ServiceNowIncidentDocument serviceNowIncident && !string.IsNullOrEmpty(serviceNowIncident.IncidentSystemId))
            {
                return $"**Sys_ID:** {serviceNowIncident.IncidentSystemId}\n\n";
            }
            return string.Empty;
        }

        // Delegate to the base implementation
        return await CreateIncidentHandlerAgentThreadInternalAsync(
            incidentDetails,
            incidentHandler,
            incidentFilter,
            request,
            IncidentType.ToString(),
            GetServiceNowSpecificProperties);
    }

    protected override ServiceNowIncidentFilterDocument GetDefaultIncidentFilter(IncidentHandlingRequestModel<ServiceNowIncidentFilterDocumentPayload> request)
    {
        string filterId = $"IncidentFilter_ServiceNow";
        return new ServiceNowIncidentFilterDocument()
        {
            Id = request?.IncidentFilter?.Id ?? filterId,
            Name = request?.IncidentFilter?.Name ?? filterId,
            AlertId = request?.IncidentFilter?.AlertId ?? filterId,
            AgentMode = request?.IncidentFilter?.AgentMode ?? AgentModes.Autonomous.ToLowerInvariant(),
            ImpactedService = request?.IncidentFilter?.ImpactedService ?? "",
            Priorities = request?.IncidentFilter?.Priorities ?? [],
            IncidentType = request?.IncidentFilter?.IncidentType ?? "",
            TitleContains = request?.IncidentFilter?.TitleContains ?? "",
            CreatedAt = request?.IncidentFilter?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = request?.IncidentFilter?.UpdatedAt ?? DateTime.UtcNow
        };
    }
}
