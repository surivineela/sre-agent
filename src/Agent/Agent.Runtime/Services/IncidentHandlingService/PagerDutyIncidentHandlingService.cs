// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Graph.Interfaces;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using PagerDutyIncident = Agent.Graph.Interfaces.PagerDutyIncident;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

public class PagerDutyIncidentHandlingService : IncidentHandlingService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload, PagerDutyIncident>
{
    private readonly IPagerDutyService _pagerDutyService;

    protected override IncidentManagementType IncidentType => IncidentManagementType.PagerDuty;
    public PagerDutyIncidentHandlingService(
        IPagerDutyService pagerDutyService,
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        IIncidentStatusMetricsService incidentStatusMetricsService,
        IAgentOutboundCommunicationService agentOutboundCommunicationService,
        IIncidentAnalysisService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload, PagerDutyIncident> incidentAnalysisService,
        ILogger<PagerDutyIncidentHandlingService> logger,
        Tracer tracer,
        IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentFilterManagementService<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IAgentFactory<AgentContext> agentFactory,
        ExperimentalSettings experimentalSettings,
        IReasoningLoopManager reasoningLoopManager)
        : base(repository, inboundCommunicationService, incidentFilterManagementService, incidentManagementService, incidentHandlerManagementService, incidentStatusMetricsService, agentOutboundCommunicationService, incidentAnalysisService, logger, tracer, agentFactory, experimentalSettings, reasoningLoopManager)
    {
        _pagerDutyService = pagerDutyService;
    }

    protected override async Task<Thread> CreateIncidentHandlerAgentThreadAsync(
        PagerDutyIncidentDocument incidentDetails,
        IncidentHandlerDocumentPayload incidentHandler,
        PagerDutyIncidentFilterDocumentPayload incidentFilter,
        IncidentHandlingRequestModelBase request)
    {
        _logger.LogInternalInformation("[PagerDutyIncidentHandlingService] CreateIncidentHandlerAgentThreadAsync: Delegating to base implementation for IncidentId: {IncidentId}", incidentDetails.Id);

        // For PagerDuty, we don't have any specific additional properties to add
        string GetPagerDutySpecificProperties(IIncidentDocument incident) => string.Empty;

        // Delegate to the base implementation
        return await CreateIncidentHandlerAgentThreadInternalAsync(
            incidentDetails,
            incidentHandler,
            incidentFilter,
            request,
            IncidentType.ToString(),
            GetPagerDutySpecificProperties);
    }

    protected override PagerDutyIncidentFilterDocument GetDefaultIncidentFilter(IncidentHandlingRequestModel<PagerDutyIncidentFilterDocumentPayload> request)
    {
        string filterId = $"IncidentFilter_PagerDuty";
        return new PagerDutyIncidentFilterDocument()
        {
            Id = request?.IncidentFilter?.Id ?? filterId,
            Name = request?.IncidentFilter?.Name ?? filterId,
            AlertId = request?.IncidentFilter?.AlertId ?? filterId,
            AgentMode = request?.IncidentFilter?.AgentMode ?? AgentModes.Autonomous.ToLowerInvariant(),
            ImpactedService = request?.IncidentFilter?.ImpactedService ?? "",
            Priority = request?.IncidentFilter?.Priority ?? "",
            IncidentType = request?.IncidentFilter?.IncidentType ?? "",
            TitleContains = request?.IncidentFilter?.TitleContains ?? "",
            CreatedAt = request?.IncidentFilter?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = request?.IncidentFilter?.UpdatedAt ?? DateTime.UtcNow
        };
    }
}
