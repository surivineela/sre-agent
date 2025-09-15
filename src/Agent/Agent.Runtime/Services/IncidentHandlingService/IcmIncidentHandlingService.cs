using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Framework;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Agent.Runtime.Services;

public class IcmIncidentHandlingService : IncidentHandlingServiceBase<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload>
{
    private readonly IICMAPIClient _icmApiClient;
    private readonly IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> _icmIncidentManagementService;

    public IcmIncidentHandlingService(
        IICMAPIClient icmApiClient,
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        IIncidentStatusMetricsService incidentStatusMetricsService,
        IAgentOutboundCommunicationService agentOutboundCommunicationService,
        IIncidentAnalysisService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> incidentAnalysisService,
        ILogger<IcmIncidentHandlingService> logger,
        Tracer tracer,
        IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> icmIncidentManagementService,
        IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IAgentFactory<AgentContext> agentFactory,
        ExperimentalSettings experimentalSettings)
        : base(repository, inboundCommunicationService, incidentFilterManagementService, incidentHandlerManagementService, incidentStatusMetricsService, agentOutboundCommunicationService, incidentAnalysisService, logger, tracer, agentFactory, experimentalSettings)
    {
        _icmApiClient = icmApiClient;
        _icmIncidentManagementService = icmIncidentManagementService;
    }

    protected override async Task<IcmIncidentDocument> GetIncidentAsync(string incidentId)
    {
        _logger.LogInternalInformation("[IcmIncidentHandlingService] GetIncidentAsync: Invoked for IncidentId: {IncidentId}", incidentId);
        try
        {
            _logger.LogInternalInformation("[IcmIncidentHandlingService] GetIncidentAsync: Using Icm for IncidentId: {IncidentId}", incidentId);
            var icmIncidentData = await _icmIncidentManagementService.GetIncidentDetails(incidentId);
            if (icmIncidentData == null)
            {
                _logger.LogInternalWarning("[IcmIncidentHandlingService] GetIncidentAsync: No incident data found for IncidentId: {IncidentId}, fetching latest", incidentId);
                var lastestIncidentData = await _icmApiClient.GetIncidentAsync(incidentId);
                icmIncidentData = new IcmIncidentDocument(lastestIncidentData);
            }
            _logger.LogInternalInformation("[IcmIncidentHandlingService] GetIncidentAsync: Returning incident data for IncidentId: {IncidentId}", incidentId);
            return icmIncidentData;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IcmIncidentHandlingService] GetIncidentAsync: Error occurred for IncidentId: {IncidentId}", incidentId);
            throw;
        }
    }

    protected override async Task<Core.Models.Api.v1.Thread> CreateIncidentHandlerAgentThreadAsync(
        IcmIncidentDocument incidentDetails,
        IncidentHandlerDocument incidentHandler,
        IcmIncidentFilterDocument incidentFilterDocument,
        IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload> request)
    {
        _logger.LogInternalInformation("[IcmIncidentHandlingService] CreateIncidentHandlerAgentThreadAsync: Delegating to base implementation for IncidentId: {IncidentId}", incidentDetails.Id);

        // For ICM, we don't have any specific additional properties to add
        string GetIcmSpecificProperties(IcmIncidentDocument incident) => string.Empty;

        // Delegate to the base implementation
        return await CreateIncidentHandlerAgentThreadInternalAsync(
            incidentDetails,
            incidentHandler,
            incidentFilterDocument,
            request,
            "ICM",
            GetIcmSpecificProperties);
    }

    protected override IcmIncidentFilterDocument GetDefaultIncidentFilter(IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload> request)
    {
        string filterId = $"IncidentFilter_ICM";
        return new IcmIncidentFilterDocument()
        {
            Id = filterId,
            Name = request?.IncidentFilter?.Name ?? filterId,
            AlertId = request?.IncidentFilter?.AlertId ?? filterId,
            AgentMode = request?.IncidentFilter?.AgentMode ?? "",
            ImpactedService = request?.IncidentFilter?.ImpactedService ?? "",
            Priority = request?.IncidentFilter?.Priority ?? "",
            IncidentType = request?.IncidentFilter?.IncidentType ?? "",
            TitleContains = request?.IncidentFilter?.TitleContains ?? "",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}
