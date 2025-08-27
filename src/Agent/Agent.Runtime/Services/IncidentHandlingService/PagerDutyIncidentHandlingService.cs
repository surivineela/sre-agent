using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Graph.Interfaces;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Agent.Core.Configuration;

namespace Agent.Runtime.Services;

public class PagerDutyIncidentHandlingService : IncidentHandlingServiceBase<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload>
{
    private readonly IPagerDutyService _pagerDutyService;
    private readonly IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload> _pagerDutyincidentManagementService;

    public PagerDutyIncidentHandlingService(
        IPagerDutyService pagerDutyService,
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        IIncidentStatusMetricsService incidentStatusMetricsService,
        IAgentOutboundCommunicationService agentOutboundCommunicationService,
        ILogger<PagerDutyIncidentHandlingService> logger,
        Tracer tracer,
        IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload> pagerDutyincidentManagementService,
        IIncidentFilterManagementService<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IAgentFactory<AgentContext> agentFactory,
        ExperimentalSettings experimentalSettings)
        : base(repository, inboundCommunicationService, incidentFilterManagementService, incidentHandlerManagementService, incidentStatusMetricsService, agentOutboundCommunicationService, logger, tracer, agentFactory, experimentalSettings)
    {
        _pagerDutyService = pagerDutyService;
        _pagerDutyincidentManagementService = pagerDutyincidentManagementService;
    }

    protected override async Task<PagerDutyIncidentDocument> GetIncidentAsync(string incidentId)
    {
        _logger.LogInternalInformation("[PagerDutyIncidentHandlingService] GetIncidentAsync: Invoked for IncidentId: {IncidentId}", incidentId);
        try
        {
            _logger.LogInternalInformation("[PagerDutyIncidentHandlingService] GetIncidentAsync: Using PagerDuty for IncidentId: {IncidentId}", incidentId);
            var incidentData = await _pagerDutyincidentManagementService.GetIncidentDetails(incidentId);
            if (incidentData == null)
            {
                _logger.LogInternalWarning("[PagerDutyIncidentHandlingService] GetIncidentAsync: No incident data found for IncidentId: {IncidentId}, fetching latest", incidentId);
                var lastedIncidentData = await _pagerDutyService.GetPagerDutyIncidentAsync(incidentId);
                incidentData = new PagerDutyIncidentDocument(
                Id: lastedIncidentData.IncidentId,
                HtmlUrl: lastedIncidentData.HtmlUrl,
                Status: lastedIncidentData.Status,
                Priority: lastedIncidentData.Priority?.Summary ?? string.Empty,
                Urgency: lastedIncidentData.Urgency ?? string.Empty,
                IncidentType: lastedIncidentData.IncidentType?.Name ?? string.Empty,
                ImpactedServiceId: lastedIncidentData.ImpactedService?.Id ?? string.Empty,
                ImpactedServiceName: lastedIncidentData.ImpactedService?.Summary ?? string.Empty,
                CreatedAt: lastedIncidentData.CreatedAt);
                incidentData.Title = lastedIncidentData.Title;
                incidentData.Description = lastedIncidentData.Body?.Details ?? string.Empty;
            }
            _logger.LogInternalInformation("[PagerDutyIncidentHandlingService] GetIncidentAsync: Returning incident data for IncidentId: {IncidentId}", incidentId);
            return incidentData;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[PagerDutyIncidentHandlingService] GetIncidentAsync: Error occurred for IncidentId: {IncidentId}", incidentId);
            throw;
        }
    }

    protected override async Task<Core.Models.Api.v1.Thread> CreateIncidentHandlerAgentThreadAsync(
        PagerDutyIncidentDocument incidentDetails,
        IncidentHandlerDocument incidentHandler,
        PagerDutyIncidentFilterDocument incidentFilterDocument,
        IncidentHandlingRequestModel<PagerDutyIncidentFilterDocumentPayload> request)
    {
        _logger.LogInternalInformation("[PagerDutyIncidentHandlingService] CreateIncidentHandlerAgentThreadAsync: Delegating to base implementation for IncidentId: {IncidentId}", incidentDetails.Id);

        // For PagerDuty, we don't have any specific additional properties to add
        string GetPagerDutySpecificProperties(IIncidentDocument incident) => string.Empty;

        // Delegate to the base implementation
        return await CreateIncidentHandlerAgentThreadInternalAsync(
            incidentDetails,
            incidentHandler,
            incidentFilterDocument,
            request,
            "PagerDuty",
            GetPagerDutySpecificProperties);
    }

    protected override PagerDutyIncidentFilterDocument GetDefaultIncidentFilter(IncidentHandlingRequestModel<PagerDutyIncidentFilterDocumentPayload> request)
    {
        string filterId = $"IncidentFilter_PagerDuty";
        return new PagerDutyIncidentFilterDocument()
        {
            Id = filterId,
            Name = request?.IncidentFilter?.Name ?? filterId,
            AlertId = request?.IncidentFilter?.AlertId ?? filterId,
            AgentMode = request?.IncidentFilter?.AgentMode ?? "",
            ImpactedService = request?.IncidentFilter?.ImpactedService ?? "",
            Priority = request?.IncidentFilter?.Priority ?? "",
            IncidentType = request?.IncidentFilter?.IncidentType ?? "",
            TitleContains = request?.IncidentFilter?.TitleContains ?? "",
            UpdatedAt = DateTime.UtcNow
        };
    }
}
