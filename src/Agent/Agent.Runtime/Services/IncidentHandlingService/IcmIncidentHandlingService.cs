// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.Logging;
using Microsoft.SREAgent.Incidents.IcM.Model;
using OpenTelemetry.Trace;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

public class IcmIncidentHandlingService : IncidentHandlingService<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, ICMIncident>
{
    private readonly IICMAPIClient _icmApiClient;

    private readonly IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> _icmIncidentManagementService;

    protected override IncidentManagementType IncidentType => IncidentManagementType.Icm;

    public IcmIncidentHandlingService(
        IICMAPIClient icmApiClient,
        IAgentInboundCommunicationService inboundCommunicationService,
        IThreadRepository repository,
        IIncidentStatusMetricsService incidentStatusMetricsService,
        IAgentOutboundCommunicationService agentOutboundCommunicationService,
        IIncidentAnalysisService<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, ICMIncident> incidentAnalysisService,
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

    protected override async Task<IncidentHandlingResponseModel> HandleIncidentInternalAsync(IcmIncidentDocument incidentDetails, IcmIncidentFilterDocumentPayload matchingFilter, IncidentHandlerDocumentPayload? matchingHandler, IncidentHandlingRequestModelBase request)
    {
        var response = new IncidentHandlingResponseModel();
        try
        {
            if (matchingHandler is null)
            {
                _logger.LogInternalWarning("[IcmIncidentHandlingService] HandleIncidentAsync: No matching handler found for FilterId: {FilterId}, using MetaAgent", matchingFilter.Id);

                var incidentRequest = new IncidentHandlingRequestModelBase()
                {
                    Title = incidentDetails.Title ?? "New Incident",
                    Description = incidentDetails.Description ?? "Alert notification.",
                    IncidentId = incidentDetails.Id,
                    Severity = incidentDetails.Priority,
                    Source = request.Source ?? incidentDetails.DocumentType,
                    AdditionalProperties = request.AdditionalProperties,
                    IsTest = request.IsTest,
                    CreatedTime = incidentDetails.CreatedAt
                };

                // use handler id from filter to set current agent for meta agent thread
                var defaultThread = await CreateIncidentMetaAgentThread(incidentRequest, matchingFilter, matchingFilter.HandlingAgent ?? string.Empty);
                _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentAsync: Created MetaAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", defaultThread.Id, request.IncidentId);

                var incidentStatusMetrics = await _incidentStatusMetricsService.GetIncidentStatusMetricsAsync(null, DateTime.Now);
                await _agentOutboundCommunicationService.NotifyIncidentStatusMetrics(defaultThread.Id, incidentStatusMetrics);

                try
                {
                    var data = ToIncidentActivitySnapshot(matchingFilter, incidentDetails, incidentRequest, matchingHandler);
                    _incidentAnalysisService.Ingest(data);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError($"[IcmIncidentHandlingService] HandleIncidentAsync: Error logging incident handling data to Incident Analysis Service; {ex.Message}");
                }

                response.StatusCode = 200;
                response.Response = new { threadId = defaultThread.Id, message = "Incident received" };
                return response;
            }

            _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentAsync: Matched Handler. Creating IncidentHandlerAgent thread for IncidentId: {IncidentId}, FilterId: {FilterId} and HandlerId: {HandlerId}", request.IncidentId, matchingFilter.Id, matchingHandler.Id);

            Thread thread;

            // Check if YAML-based incident handling is enabled
            if (_experimentalSettings.UseYamlForIncidentHandling)
            {
                _logger.LogInternalInformation("[IcmIncidentHandlingService] Using YAML-based incident handling for IncidentId: {IncidentId}", request.IncidentId);
                thread = await CreateIncidentHandlerAgentThreadAsync(incidentDetails, matchingHandler, matchingFilter, (IncidentHandlingRequestModelBase)request);
            }
            else
            {
                _logger.LogInternalInformation("[IcmIncidentHandlingService] Using legacy incident handling for IncidentId: {IncidentId}", request.IncidentId);
                thread = await CreateIncidentHandlerAgentThreadAsync(incidentDetails, matchingHandler, matchingFilter, (IncidentHandlingRequestModelBase)request);
            }

            _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentAsync: Created IncidentHandlerAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId} and HandlerId: {HandlerId}", thread.Id, request.IncidentId, matchingHandler.Id);

            try
            {
                var data = ToIncidentActivitySnapshot(matchingFilter, incidentDetails, request, matchingHandler);
                _incidentAnalysisService.Ingest(data);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"[IcmIncidentHandlingService] HandleIncidentAsync: Error logging incident handling data to Incident Analysis Service; {ex.Message}");
            }

            response.StatusCode = 200;
            response.Response = new { threadId = thread.Id, message = "Incident received" };
            return response;
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IcmIncidentHandlingService] HandleIncidentAsync: Error processing IncidentId: {IncidentId}", request.IncidentId);
            response.StatusCode = 500;
            response.Response = "Failed to process Incident";
            return response;
        }
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

    protected override async Task<Thread> CreateIncidentHandlerAgentThreadAsync(
        IcmIncidentDocument incidentDetails,
        IncidentHandlerDocumentPayload incidentHandler,
        IcmIncidentFilterDocumentPayload incidentFilter,
        IncidentHandlingRequestModelBase request)
    {
        _logger.LogInternalInformation("[IcmIncidentHandlingService] CreateIncidentHandlerAgentThreadAsync: Delegating to base implementation for IncidentId: {IncidentId}", incidentDetails.Id);

        // For ICM, we don't have any specific additional properties to add
        string GetIcmSpecificProperties(IcmIncidentDocument incident) => string.Empty;

        // Delegate to the base implementation
        return await CreateIncidentHandlerAgentThreadInternalAsync(
            incidentDetails,
            incidentHandler,
            incidentFilter,
            request,
            IncidentType.ToString(),
            GetIcmSpecificProperties);
    }

    protected override IcmIncidentFilterDocument GetDefaultIncidentFilter(IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload> request)
    {
        string filterId = $"IncidentFilter_ICM";
        return new IcmIncidentFilterDocument()
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
    protected override IncidentAIData ToIncidentActivitySnapshot(IcmIncidentFilterDocumentPayload filter, IcmIncidentDocument incidentDetails, IncidentHandlingRequestModelBase request, IncidentHandlerDocumentPayload? handler)
    {
        IncidentAIData snapShot = new IncidentAIData
        {
            HandlerId = filter.Id ?? filter.Name ?? "no-handler",
            IncidentId = incidentDetails.Id,
            IncidentTitle = incidentDetails.Title,
            IncidentCreatedAt = incidentDetails.CreatedDate.UtcDateTime,
            IncidentUpdatedAt = incidentDetails.UpdatedAt > DateTime.MinValue.AddDays(1) ? incidentDetails.UpdatedAt : incidentDetails.CreatedDate.UtcDateTime,
            HandlerCreatedAt = filter.CreatedAt,
            HandlerUpdatedAt = filter.UpdatedAt,
            IncidentHandledAt = DateTime.UtcNow,
            MitigatedAt = null,
            Status = incidentDetails.Status.ToString(),
            Priority = incidentDetails.Priority,
            IsMitigatedByAgent = false,
            IsAssistedByAgent = incidentDetails.IsAssistedByAgent,
            RootCause = incidentDetails.AIRootCause,
            RootCauseDescription = incidentDetails.RootCauseDescription,
            Summary = incidentDetails.GeneralSummary,
            ImpactedService = incidentDetails.ImpactedServiceName,
            RunMode = !string.IsNullOrWhiteSpace(filter.AgentMode) ? filter.AgentMode : "review",
            IsHandlerCustom = !string.IsNullOrWhiteSpace(handler?.CustomInstructions) ? true : false,
            IncidentPlatform = IncidentType.ToString(),
            TimeTilMitigation = null
        };
        return snapShot;
    }

    protected override IcmIncidentFilterDocumentPayload GetIncidentFilter(List<IcmIncidentFilterDocumentPayload> filterPayloads, IcmIncidentDocument incidentDetails)
    {
        var matchingFilters = filterPayloads
            .Where(filter =>
                (string.IsNullOrWhiteSpace(filter.ImpactedService) || filter.ImpactedService == incidentDetails.ImpactedServiceId || filter.ImpactedService == incidentDetails.ImpactedServiceName)
                &&
                (string.IsNullOrWhiteSpace(filter.Priority) || IsPriorityMatch(filter.Priority, incidentDetails.Priority))
                &&
                (string.IsNullOrWhiteSpace(filter.IncidentType) || (filter.IncidentType == incidentDetails.IncidentType))
                &&
                (string.IsNullOrWhiteSpace(filter.TitleContains) || (incidentDetails.Title?.Contains(filter.TitleContains, StringComparison.OrdinalIgnoreCase) ?? false))
                &&
                (string.IsNullOrWhiteSpace(filter.OwningTeamId) || filter.OwningTeamId == incidentDetails.OwningTeamId.ToString())
            )
            .ToList();

        _logger.LogInternalInformation("[IcmIncidentHandlingService] GetIncidentFilter: Found {MatchingFilterCount} matching filters for IncidentId: {IncidentId}", matchingFilters.Count, incidentDetails.Id);

        if (matchingFilters is null || matchingFilters.Count == 0)
        {
            throw new IncidentFilterNotFoundException();
        }

        var matchingFilter = matchingFilters.First();
        return matchingFilter;
    }
}
