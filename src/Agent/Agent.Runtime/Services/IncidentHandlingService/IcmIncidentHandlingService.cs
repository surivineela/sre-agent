// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.SREAgent.Incidents.IcM.Model;
using OpenTelemetry.Trace;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

public class IcmIncidentHandlingService : IncidentHandlingServiceBase<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, ICMIncident>
{
    private readonly IICMAPIClient _icmApiClient;

    private readonly IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> _icmIncidentManagementService;

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

    public override async Task<IncidentHandlingResponseModel> HandleIncidentAsync(IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload>? request)
    {
        if (request is null)
        {
            return new IncidentHandlingResponseModel
            {
                StatusCode = 400,
                Response = "Invalid request. IncidentHandlingRequestModel cannot be null."
            };
        }
        _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Invoked for IncidentId: {IncidentId}", request.IncidentId);
        var incidentId = request.IncidentId;
        var response = new IncidentHandlingResponseModel();
        try
        {
            var incidentDetails = await GetIncidentAsync(incidentId);

            // Check if JSON-based custom handler is mapped to the filter
            var (matchingFilter, matchingHandler) = await GetIncidentFilterAndHandlerAsync(request, incidentDetails);

            if (matchingHandler == null)
            {
                _logger.LogInternalWarning("[IncidentHandlingService] HandleIncidentAsync: No matching handler found for FilterId: {FilterId}, using MetaAgent", matchingFilter.Id);

                var incidentRequest = new IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload>
                {
                    Title = incidentDetails.Title ?? "New Incident",
                    Description = incidentDetails.Description ?? "Alert notification.",
                    IncidentId = incidentDetails.Id,
                    Severity = incidentDetails.Priority,
                    Source = request.Source ?? incidentDetails.DocumentType,
                    AdditionalProperties = request.AdditionalProperties,
                    IsTest = request.IsTest,
                    IncidentHandler = request.IncidentHandler,
                    IncidentFilter = request.IncidentFilter
                };

                // use handler id from filter to set current agent for meta agent thread
                var defaultThread = await CreateIncidentMetaAgentThread(incidentRequest, matchingFilter, matchingFilter.HandlingAgent ?? string.Empty);
                _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Created MetaAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", defaultThread.Id, incidentId);

                var incidentStatusMetrics = await _incidentStatusMetricsService.GetIncidentStatusMetricsAsync(null, DateTime.Now);
                await _agentOutboundCommunicationService.NotifyIncidentStatusMetrics(defaultThread.Id, incidentStatusMetrics);

                try
                {
                    var data = new IncidentAIData
                    {
                        HandlerId = matchingFilter.Id ?? matchingFilter.Name ?? incidentRequest.IncidentFilter?.Id ?? incidentRequest.IncidentFilter?.Name ?? $"no-handler",
                        IncidentId = incidentRequest.IncidentId,
                        IncidentTitle = incidentRequest.Title,
                        IncidentCreatedAt = incidentDetails.CreatedDate.UtcDateTime,
                        IncidentUpdatedAt = incidentDetails.UpdatedAt > DateTime.MinValue.AddDays(1) ? incidentDetails.UpdatedAt : incidentDetails.CreatedAt,
                        HandlerCreatedAt = matchingFilter.CreatedAt,
                        HandlerUpdatedAt = matchingFilter.UpdatedAt,
                        IncidentHandledAt = DateTime.UtcNow,
                        MitigatedAt = null,
                        Status = incidentDetails.Status.ToString(),
                        Priority = incidentRequest.Severity,
                        IsMitigatedByAgent = false,
                        IsAssistedByAgent = incidentDetails.IsAssistedByAgent,
                        RootCause = incidentDetails.AIRootCause,
                        RootCauseDescription = incidentDetails.RootCauseDescription,
                        Summary = incidentDetails.GeneralSummary,
                        ImpactedService = incidentDetails.ImpactedServiceName,
                        RunMode = incidentRequest.IncidentFilter?.AgentMode ?? matchingFilter?.AgentMode ?? string.Empty,
                        InstructionType = string.IsNullOrWhiteSpace(incidentRequest.IncidentHandler?.CustomInstructions) ? "Default" : "Custom",
                        IncidentPlatform = GetIncidentPlatform()
                    };
                    // Can not yet ingest data for Azure Monitor
                    _incidentAnalysisService.Ingest(data);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError($"[IncidentHandlingService] HandleIncidentAsync: Error logging incident handling data to Incident Analysis Service; {ex.Message}");
                }

                response.StatusCode = 200;
                response.Response = new { threadId = defaultThread.Id, message = "Incident received" };
                return response;
            }

            _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Matched Handler. Creating IncidentHandlerAgent thread for IncidentId: {IncidentId}, FilterId: {FilterId} and HandlerId: {HandlerId}", incidentId, matchingFilter.Id, matchingHandler.Id);

            Thread thread;

            // Check if YAML-based incident handling is enabled
            if (_experimentalSettings.UseYamlForIncidentHandling)
            {
                _logger.LogInternalInformation("[IncidentHandlingService] Using YAML-based incident handling for IncidentId: {IncidentId}", incidentId);
                thread = await CreateIncidentHandlerAgentThreadAsync(incidentDetails, matchingHandler, matchingFilter, request);
            }
            else
            {
                _logger.LogInternalInformation("[IncidentHandlingService] Using legacy incident handling for IncidentId: {IncidentId}", incidentId);
                thread = await CreateIncidentHandlerAgentThreadAsync(incidentDetails, matchingHandler, matchingFilter, request);
            }

            _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Created IncidentHandlerAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId} and HandlerId: {HandlerId}", thread.Id, incidentId, matchingHandler.Id);

            try
            {
                var data = new IncidentAIData
                {
                    HandlerId = matchingFilter.Id,
                    IncidentId = incidentDetails.Id,
                    IncidentTitle = incidentDetails.Title,
                    HandlerCreatedAt = matchingFilter.CreatedAt,
                    HandlerUpdatedAt = matchingFilter.UpdatedAt,
                    IncidentCreatedAt = incidentDetails.CreatedDate.UtcDateTime,
                    IncidentUpdatedAt = incidentDetails.UpdatedAt,
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
                    RunMode = matchingFilter?.AgentMode ?? request.IncidentFilter?.AgentMode ?? string.Empty,
                    InstructionType = string.IsNullOrWhiteSpace(matchingHandler.CustomInstructions) ? "Default" : "Custom",
                    IncidentPlatform = GetIncidentPlatform()

                };

                // Can not yet ingest data for Azure Monitor
                _incidentAnalysisService.Ingest(data);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"[IncidentHandlingService] HandleIncidentAsync: Error logging incident handling data to Incident Analysis Service; {ex.Message}");
            }

            response.StatusCode = 200;
            response.Response = new { threadId = thread.Id, message = "Incident received" };
            return response;
        }
        catch (Exception ex) when (ex is IncidentFilterNotFoundException)
        {
            _logger.LogInternalWarning("[IncidentHandlingService] HandleIncidentAsync: No matching incident filters found for IncidentId: {IncidentId}", incidentId);
            response.StatusCode = 404;
            response.Response = "No matching incident filters found for this incident.";
            return response;
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentHandlingService] HandleIncidentAsync: Error processing IncidentId: {IncidentId}", incidentId);
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
                icmIncidentData =  new IcmIncidentDocument(lastestIncidentData);
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
            GetIncidentSource(),
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
            AgentMode = request?.IncidentFilter?.AgentMode ?? "",
            ImpactedService = request?.IncidentFilter?.ImpactedService ?? "",
            Priority = request?.IncidentFilter?.Priority ?? "",
            IncidentType = request?.IncidentFilter?.IncidentType ?? "",
            TitleContains = request?.IncidentFilter?.TitleContains ?? "",
            CreatedAt = request?.IncidentFilter?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = request?.IncidentFilter?.UpdatedAt ?? DateTime.UtcNow
        };
    }

    public override string GetIncidentSource()
    {
        return "ICM";
    }

    protected override string GetIncidentPlatform()
    {
        return IncidentManagementType.Icm.ToString();
    }
}
