using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using static Agent.Data.DataModels.ServiceNowIncidentFilterDocument;

namespace Agent.Runtime.Services;

/// <summary>
/// Handles ServiceNow-specific incident processing
/// </summary>
public class ServiceNowIncidentHandlingService : IncidentHandlingServiceBase<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload>
{
    private readonly IServiceNowAPIClient _serviceNowAPIClient;
    private readonly IIncidentManagementService<ServiceNowIncidentDocument> _serviceNowIncidentManagementService;


    public ServiceNowIncidentHandlingService(
        IServiceNowAPIClient serviceNowAPIClient,
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        ILogger<ServiceNowIncidentHandlingService> logger,
        Tracer tracer,
        IIncidentManagementService<ServiceNowIncidentDocument> serviceNowIncidentManagementService,
        IIncidentFilterManagementService<ServiceNowIncidentFilterDocument> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService
        )
        : base(repository, inboundCommunicationService, incidentFilterManagementService, incidentHandlerManagementService, logger, tracer)
    {
        _serviceNowAPIClient = serviceNowAPIClient;
        _serviceNowIncidentManagementService = serviceNowIncidentManagementService;
    }

    protected override async Task<ServiceNowIncidentDocument> GetIncidentAsync(string incidentId)
    {
        _logger.LogInternalInformation("[ServiceNowIncidentHandlingService] GetIncidentAsync: Invoked for IncidentId: {IncidentId}", incidentId);
        try
        {
            _logger.LogInternalInformation("[ServiceNowIncidentHandlingService] GetIncidentAsync: Using ServiceNow for IncidentId: {IncidentId}", incidentId);
            var serviceNowIncidentData = await _serviceNowIncidentManagementService.GetIncidentDetails(incidentId);
            if (serviceNowIncidentData == null)
            {
                _logger.LogInternalWarning("[ServiceNowIncidentHandlingService] GetIncidentAsync: No incident data found for IncidentId: {IncidentId}, fetching latest", incidentId);
                var latestIncidentData = await _serviceNowAPIClient.GetIncidentAsync(incidentId);
                serviceNowIncidentData = new ServiceNowIncidentDocument(latestIncidentData);
            }
            _logger.LogInternalInformation("[ServiceNowIncidentHandlingService] GetIncidentAsync: Returning incident data for IncidentId: {IncidentId}", incidentId);
            return serviceNowIncidentData;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[ServiceNowIncidentHandlingService] GetIncidentAsync: Error occurred for IncidentId: {IncidentId}", incidentId);
            throw;
        }
    }

    protected override async Task<Core.Models.Api.v1.Thread> CreateIncidentHandlerAgentThreadAsync(
        ServiceNowIncidentDocument incidentDetails,
        IncidentHandlerDocument incidentHandler,
        ServiceNowIncidentFilterDocument incidentFilterDocument,
        IncidentHandlingRequestModel<ServiceNowIncidentFilterDocumentPayload> request)
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
            incidentFilterDocument,
            request,
            "ServiceNow",
            GetServiceNowSpecificProperties);
    }

    protected override ServiceNowIncidentFilterDocument GetDefaultIncidentFilter(IncidentHandlingRequestModel<ServiceNowIncidentFilterDocumentPayload> request)
    {
        string filterId = $"IncidentFilter_ServiceNow";
        return new ServiceNowIncidentFilterDocument(
           Id: filterId,
            DocumentType: filterId,
            Name: request?.IncidentFilter?.Name ?? filterId,
            AlertId: request?.IncidentFilter?.AlertId ?? filterId,
            AgentMode: request?.IncidentFilter?.AgentMode ?? "",
            ImpactedService: request?.IncidentFilter?.ImpactedService ?? "",
            Priority: request?.IncidentFilter?.Priority ?? "",
            IncidentType: request?.IncidentFilter?.IncidentType ?? "",
            TitleContains: request?.IncidentFilter?.TitleContains ?? "",
            CreatedAt: DateTime.UtcNow);
    }
}
