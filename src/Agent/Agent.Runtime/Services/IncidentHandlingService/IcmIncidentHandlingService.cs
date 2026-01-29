// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services.IncidentTriggerDetection;
using Microsoft.Extensions.Logging;
using Microsoft.SREAgent.Incidents.IcM.Model;
using OpenTelemetry.Trace;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

public class IcmIncidentHandlingService : IncidentHandlingService<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, ICMIncident>
{
    private readonly IICMAPIClient _icmApiClient;
    private readonly IIncidentThreadLookupService _incidentThreadLookupService;

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
        IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IAgentFactory<AgentContext> agentFactory,
        ExperimentalSettings experimentalSettings,
        IReasoningLoopManager reasoningLoopManager,
        IIncidentThreadLookupService incidentThreadLookupService)
        : base(repository, inboundCommunicationService, incidentFilterManagementService, incidentManagementService, incidentHandlerManagementService, incidentStatusMetricsService, agentOutboundCommunicationService, incidentAnalysisService, logger, tracer, agentFactory, experimentalSettings, reasoningLoopManager)
    {
        _icmApiClient = icmApiClient;
        _incidentThreadLookupService = incidentThreadLookupService;
    }

    protected override async Task<IncidentHandlingResponseModel> HandleIncidentInternalAsync(IcmIncidentDocument incidentDetails, IcmIncidentFilterDocumentPayload matchingFilter, IncidentHandlerDocumentPayload? matchingHandler, IncidentHandlingRequestModelBase request)
    {
        Thread? thread = null;
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
                thread = await CreateIncidentMetaAgentThread(incidentRequest, matchingFilter, matchingFilter.HandlingAgent ?? string.Empty, incidentDetails.Status?.ToLowerInvariant());
                _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentAsync: Created MetaAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", thread.Id, request.IncidentId);

                var incidentStatusMetrics = await _incidentStatusMetricsService.GetIncidentStatusMetricsAsync(null, DateTime.Now);
                await _agentOutboundCommunicationService.NotifyIncidentStatusMetrics(thread.Id, incidentStatusMetrics);

                try
                {
                    var data = ToIncidentActivitySnapshot(matchingFilter, incidentDetails, incidentRequest, matchingHandler);
                    _incidentAnalysisService.Ingest(data);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError($"[IcmIncidentHandlingService] HandleIncidentAsync: Error logging incident handling data to Incident Analysis Service; {ex.Message}");
                }

                return new IncidentHandlingResponseModel
                {
                    StatusCode = 200,
                    Message = "Incident received",
                    IncidentId = request.IncidentId,
                    ThreadId = thread.Id
                };
            }

            _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentAsync: Matched Handler. Creating IncidentHandlerAgent thread for IncidentId: {IncidentId}, FilterId: {FilterId} and HandlerId: {HandlerId}", request.IncidentId, matchingFilter.Id, matchingHandler.Id);

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

            return new IncidentHandlingResponseModel
            {
                StatusCode = 200,
                Message = "Incident received",
                IncidentId = request.IncidentId,
                ThreadId = thread.Id
            };

        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IcmIncidentHandlingService] HandleIncidentAsync: Error processing IncidentId: {IncidentId}", request.IncidentId);
            return new IncidentHandlingResponseModel
            {
                StatusCode = 500,
                Message = "Failed to process Incident",
                IncidentId = request.IncidentId,
                ThreadId = thread?.Id
            };
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
            Priorities = request?.IncidentFilter?.Priorities ?? new List<string>(),
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
                IsPriorityMatch(filter.Priorities, incidentDetails.Priority)
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

    public override async Task<List<IncidentHandlingResponseModel>> HandleIncidentsAsync(IEnumerable<IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload>> request)
    {
        var allResponses = new List<IncidentHandlingResponseModel>();

        foreach (var incidentId in request.Select(r => r.IncidentId).Distinct())
        {
            List<IncidentHandlingResponseModel> responses;

            // ICM: Use the new multi-agent aware method
            responses = await HandleIncidentForManualTriggerAsync(incidentId);
            allResponses.AddRange(responses);
        }

        return allResponses;
    }

    /// <summary>
    /// Handles manual trigger for an incident by creating threads for all matching handlingAgents
    /// with the CreatedOrTransferred trigger enabled.
    /// </summary>
    /// <param name="incidentId">The incident ID to process</param>
    /// <returns>List of responses, one per handling agent</returns>
    private async Task<List<IncidentHandlingResponseModel>> HandleIncidentForManualTriggerAsync(string incidentId, bool ignoreExistingThreads = true)
    {
        var responses = new List<IncidentHandlingResponseModel>();

        try
        {
            _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: Processing incident {IncidentId}", incidentId);

            // 1. Fetch incident from DB
            var incident = await _incidentManagementService.GetIncidentAsync(incidentId);
            if (incident is null)
            {
                _logger.LogInternalWarning("[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: Incident not found for IncidentId: {IncidentId}", incidentId);
                return new List<IncidentHandlingResponseModel>
                {
                    new IncidentHandlingResponseModel
                    {
                        StatusCode = 404,
                        Message = "Incident not found",
                        IncidentId = incidentId
                    }
                };
            }

            // 2. List all filters from DB
            var allFilters = await _incidentFilterManagementService.ListIncidentFilters(false);
            var filterPayloads = allFilters.Select(f => (IcmIncidentFilterDocumentPayload)f).ToList();

            // 3. Filter by incident match criteria
            var matchingFilters = filterPayloads
                .Where(filter =>
                    (string.IsNullOrWhiteSpace(filter.ImpactedService) || filter.ImpactedService == incident.ImpactedServiceId || filter.ImpactedService == incident.ImpactedServiceName)
                    &&
                    IsPriorityMatch(filter.Priorities, incident.Priority)
                    &&
                    (string.IsNullOrWhiteSpace(filter.IncidentType) || (filter.IncidentType == incident.IncidentType))
                    &&
                    (string.IsNullOrWhiteSpace(filter.TitleContains) || (incident.Title?.Contains(filter.TitleContains, StringComparison.OrdinalIgnoreCase) ?? false))
                    &&
                    (string.IsNullOrWhiteSpace(filter.OwningTeamId) || filter.OwningTeamId == incident.OwningTeamId.ToString())
                )
                .ToList();

            _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: Found {MatchingFilterCount} matching filters for IncidentId: {IncidentId}", matchingFilters.Count, incidentId);

            // 4. Further filter by trigger enablement (CreatedOrTransferred)
            var filtersWithTriggerEnabled = matchingFilters
                .Where(f => f is IcmIncidentFilterDocument icmFilter && icmFilter.IsTriggerEnabled(IcmIncidentTriggerEvent.IncidentCreatedOrTransferred))
                .Cast<IcmIncidentFilterDocument>()
                .ToList();

            _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: {FilterCount} filters have CreatedOrTransferred trigger enabled for IncidentId: {IncidentId}", filtersWithTriggerEnabled.Count, incidentId);

            // 5. If no matching filters with trigger enabled, fallback to existing behavior
            if (filtersWithTriggerEnabled.Count == 0)
            {
                _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: No filters with CreatedOrTransferred trigger, falling back to standard handling for IncidentId: {IncidentId}", incidentId);
                var fallbackResponse = await HandleIncidentAsync(new IncidentHandlingRequestModel<IcmIncidentFilterDocumentPayload> { IncidentId = incidentId });
                return new List<IncidentHandlingResponseModel> { fallbackResponse };
            }

            // 6. Collect ALL unique handlingAgents across all matching filters
            var allAgents = filtersWithTriggerEnabled
                .SelectMany(f => f.GetEffectiveHandlingAgents())
                .Distinct()
                .ToList();

            if (allAgents.Count == 0)
            {
                // Backward compatibility: empty string = meta_agent fallback
                allAgents = new List<string> { string.Empty };
            }

            _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: Found {AgentCount} unique handling agents for IncidentId: {IncidentId}", allAgents.Count, incidentId);

            // 7. Get existing threads for deduplication
            var existingThreads = ignoreExistingThreads ? new List<ThreadDocument>() : await _incidentThreadLookupService.FindAllThreadsForIncidentAsync(incidentId);
            var existingHandlerIds = existingThreads
                .Where(t => t.IncidentDetails?.HandlerId != null)
                .Select(t => t.IncidentDetails!.HandlerId!)
                .ToHashSet();

            _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: Found {ExistingCount} existing threads for IncidentId: {IncidentId}", existingThreads.Count, incidentId);

            // 8. For agents WITH existing threads, add "already exists" responses
            foreach (var agent in allAgents.Where(a => existingHandlerIds.Contains(a)))
            {
                var existingThread = existingThreads.First(t => t.IncidentDetails?.HandlerId == agent);
                var agentDisplayName = string.IsNullOrEmpty(agent) ? "meta_agent" : agent;
                Guid.TryParse(existingThread.Id, out var threadGuid);
                responses.Add(new IncidentHandlingResponseModel
                {
                    StatusCode = 200,
                    Message = $"Thread already exists for agent: {agentDisplayName}",
                    IncidentId = incidentId,
                    ThreadId = threadGuid
                });
                _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: Thread already exists for agent {Agent} on IncidentId: {IncidentId}", agentDisplayName, incidentId);
            }

            // 9. For agents NEEDING new threads, create them
            var agentsNeedingThreads = allAgents.Where(a => !existingHandlerIds.Contains(a)).ToList();

            foreach (var agent in agentsNeedingThreads)
            {
                // Pick first filter that has this agent (for AgentMode, FilterId, etc.)
                var filter = filtersWithTriggerEnabled.FirstOrDefault(f =>
                    f.GetEffectiveHandlingAgents().Contains(agent)) ?? filtersWithTriggerEnabled.First();

                // Temporarily set HandlingAgent (like IcmScanner does)
                var originalHandlingAgent = filter.HandlingAgent;
                filter.HandlingAgent = agent;

                try
                {
                    var request = new IncidentHandlingRequestModelWithFilterOnly<IcmIncidentFilterDocumentPayload>
                    {
                        IncidentId = incidentId,
                        Title = incident.Title,
                        Description = incident.Description,
                        Severity = incident.Priority,
                        CreatedTime = incident.CreatedAt,
                        ImpactedService = incident.ImpactedServiceName,
                        IncidentFilter = filter,
                        TriggerEvent = IcmIncidentTriggerEvent.IncidentCreatedOrTransferred
                    };

                    var response = await HandleIncidentAsync(request);
                    var agentDisplayName = string.IsNullOrEmpty(agent) ? "meta_agent" : agent;
                    response.Message = $"Thread created for agent: {agentDisplayName}";
                    responses.Add(response);

                    _logger.LogInternalInformation("[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: Created thread {ThreadId} for agent {Agent} on IncidentId: {IncidentId}", response.ThreadId, agentDisplayName, incidentId);
                }
                finally
                {
                    // Restore original HandlingAgent
                    filter.HandlingAgent = originalHandlingAgent;
                }
            }

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IcmIncidentHandlingService] HandleIncidentForManualTriggerAsync: Error processing IncidentId: {IncidentId}", incidentId);
            return new List<IncidentHandlingResponseModel>
            {
                new IncidentHandlingResponseModel
                {
                    StatusCode = 500,
                    Message = $"Failed to process incident: {ex.Message}",
                    IncidentId = incidentId
                }
            };
        }
    }
}
