// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Author = Agent.Core.Models.Api.v1.Author;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

public class IncidentHandlingRequestModelBase
{
    public string? Title { get; set; }
    public string? Description { get; set; }

    [Required]
    public required string IncidentId { set; get; }
    public string? Severity { get; set; }
    public string? Source { get; set; }
    public DateTimeOffset? CreatedTime { get; set; }
    public string? ImpactedService { get; set; }

    public Dictionary<string, string>? AdditionalProperties { get; set; }
    public bool IsTest { get; set; } = false;
}

public class IncidentHandlingRequestModel<TIncidentFilterDocumentPayload> : IncidentHandlingRequestModelBase
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    public IncidentHandlerDocumentPayload? IncidentHandler { get; set; }
    public TIncidentFilterDocumentPayload? IncidentFilter { get; set; }
}

public class IncidentHandlingRequestModelWithFilterOnly<TIncidentFilterDocumentPayload> : IncidentHandlingRequestModelBase
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    public required TIncidentFilterDocumentPayload IncidentFilter { get; set; }
}

public class IncidentHandlingResponseModel
{
    public int StatusCode { get; set; }
    public string? Message { get; set; }
    public required string IncidentId { get; set; }
    public Guid? ThreadId { get; set; }
}

public interface IIncidentHandlingService<TIncidentFilterDocumentPayload> where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    /// <summary>
    /// This method handles the request from incident webhook which may includes filter/handler
    /// </summary>
    /// <param name="incidentDocument"></param>
    /// <returns></returns>
    Task<IncidentHandlingResponseModel> HandleIncidentAsync(IncidentHandlingRequestModel<TIncidentFilterDocumentPayload>? request);
    /// <summary>
    /// This method handles the request from scanner which must include filter
    /// </summary>
    /// <param name="incidentDocument"></param>
    /// <returns></returns>
    Task<IncidentHandlingResponseModel> HandleIncidentAsync(IncidentHandlingRequestModelWithFilterOnly<TIncidentFilterDocumentPayload> request);

    /// <summary>
    /// This method handles batch requests for multiple incidents
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    Task<List<IncidentHandlingResponseModel>> HandleIncidentsAsync(IEnumerable<IncidentHandlingRequestModel<TIncidentFilterDocumentPayload>> request);
}

public abstract class IncidentHandlingServiceBase<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload> : IIncidentHandlingService<TIncidentFilterDocumentPayload>
    where TIncidentDocument : IIncidentDocument
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
    where TIncidentFilterDocument : TIncidentFilterDocumentPayload, IIncidentFilterDocument, new()
{
    protected readonly IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> _incidentFilterManagementService;
    protected readonly IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload> _incidentManagementService;
    protected readonly IIncidentHandlerManagementService _incidentHandlerManagementService;
    protected readonly ILogger _logger;
    protected readonly IAgentFactory<AgentContext> _agentFactory;

    public IncidentHandlingServiceBase(
        IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IAgentFactory<AgentContext> agentFactory,
        ILogger logger)
    {
        _incidentFilterManagementService = incidentFilterManagementService;
        _incidentManagementService = incidentManagementService;
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _logger = logger;
        _agentFactory = agentFactory;
    }


    protected abstract Task<IncidentHandlingResponseModel> HandleIncidentInternalAsync(TIncidentDocument incidentDetails, TIncidentFilterDocumentPayload filter, IncidentHandlerDocumentPayload? handler, IncidentHandlingRequestModelBase baseRequest);
    protected abstract TIncidentFilterDocumentPayload GetDefaultIncidentFilter(IncidentHandlingRequestModel<TIncidentFilterDocumentPayload> request);

    public async Task<IncidentHandlingResponseModel> HandleIncidentAsync(IncidentHandlingRequestModel<TIncidentFilterDocumentPayload>? request)
    {
        if (request is null)
        {
            _logger.LogInternalWarning("[IncidentHandlingServiceBase] HandleIncidentAsync: Received null request");
            return new IncidentHandlingResponseModel
            {
                StatusCode = 400,
                Message = "Invalid request. IncidentHandlingRequestModel cannot be null.",
                IncidentId = string.Empty
            };
        }

        try
        {
            var incident = await _incidentManagementService.GetIncidentAsync(request.IncidentId);

            if (incident is null)
            {
                _logger.LogInternalWarning("[IncidentHandlingServiceBase] HandleIncidentAsync: Incident not found for IncidentId: {IncidentId}", request.IncidentId);
                return new IncidentHandlingResponseModel
                {
                    StatusCode = 404,
                    Message = "Incident not found.",
                    IncidentId = request.IncidentId
                };
            }

            var (filter, handler) = await GetIncidentFilterAndHandlerAsync(request, incident);

            var response = await HandleIncidentInternalAsync(incident, filter, handler, request);
            return response;
        }
        catch (Exception ex) when (ex is IncidentFilterNotFoundException)
        {
            _logger.LogInternalWarning("[IncidentHandlingServiceBase] HandleIncidentAsync: No matching incident filters found for IncidentId: {IncidentId}", request.IncidentId);
            return new IncidentHandlingResponseModel
            {
                StatusCode = 404,
                Message = "No matching incident filters found for this incident.",
                IncidentId = request.IncidentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentHandlingServiceBase] HandleIncidentAsync: Error processing IncidentId: {IncidentId}", request.IncidentId);
            return new IncidentHandlingResponseModel
            {
                StatusCode = 500,
                Message = "Failed to process Incident",
                IncidentId = request.IncidentId
            };
        }
    }

    public async Task<IncidentHandlingResponseModel> HandleIncidentAsync(IncidentHandlingRequestModelWithFilterOnly<TIncidentFilterDocumentPayload> request)
    {
        if (request is null)
        {
            _logger.LogInternalWarning("[IncidentHandlingServiceBase] HandleIncidentAsync: Received null request");
            return new IncidentHandlingResponseModel
            {
                StatusCode = 400,
                Message = "Invalid request. IncidentHandlingRequestModel cannot be null.",
                IncidentId = string.Empty
            };
        }

        try
        {
            var (filter, handler) = await GetIncidentFilterAndHandlerAsync(request);
            var incident = await _incidentManagementService.GetIncidentAsync(request.IncidentId);
            if (incident is null)
            {
                _logger.LogInternalWarning("[IncidentHandlingServiceBase] HandleIncidentAsync: Incident not found for IncidentId: {IncidentId}", request.IncidentId);
                return new IncidentHandlingResponseModel
                {
                    StatusCode = 404,
                    Message = "Incident not found.",
                    IncidentId = request.IncidentId
                };
            }
            var response = await HandleIncidentInternalAsync(incident, filter, handler, request);
            return response;
        }
        catch (Exception ex)
        {

            _logger.LogInternalError(ex, "[IncidentHandlingServiceBase] HandleIncidentAsync: Error processing IncidentId: {IncidentId}", request.IncidentId);
            return new IncidentHandlingResponseModel
            {
                StatusCode = 500,
                Message = "Failed to process Incident",
                IncidentId = request.IncidentId
            };
        }
    }

    public async Task<List<IncidentHandlingResponseModel>> HandleIncidentsAsync(IEnumerable<IncidentHandlingRequestModel<TIncidentFilterDocumentPayload>> request)
    {
        if (request is null || !request.Any())
        {
            return new List<IncidentHandlingResponseModel>()
            {
                new IncidentHandlingResponseModel
                {
                    StatusCode = 400,
                    Message = "Invalid request. IncidentHandlingRequestModel list cannot be null or empty.",
                    IncidentId = string.Empty
                }
            };
        }

        request = request.DistinctBy(r => r.IncidentId);

        // Process each incident handling request
        var tasks = request.Select(r => HandleIncidentAsync(r));
        var results = await Task.WhenAll(tasks);

        return results.ToList();
    }



    public async Task<(TIncidentFilterDocumentPayload, IncidentHandlerDocument?)> GetIncidentFilterAndHandlerAsync(IncidentHandlingRequestModelWithFilterOnly<TIncidentFilterDocumentPayload> request)
    {
        var matchingFilter = request.IncidentFilter;
        var incidentHandlers = await _incidentHandlerManagementService.ListIncidentHandlers();
        _logger.LogInternalInformation("[IncidentHandlingServiceBase] GetIncidentFilterAndHandlerAsync: Retrieved {HandlerCount} handlers for FilterId: {FilterId}", incidentHandlers.Count, matchingFilter.Id);

        var matchingHandlers = incidentHandlers.Where(x => x.IncidentFilterId == matchingFilter.Id);
        if (!matchingHandlers.Any())
        {
            _logger.LogInternalWarning("[IncidentHandlingServiceBase] GetIncidentFilterAndHandlerAsync: No matching handler found for FilterId: {FilterId}", matchingFilter.Id);
        }
        return (matchingFilter, matchingHandlers.FirstOrDefault());
    }

    /// <summary>
    /// For IncidentHandlingRequestModel request:
    /// filter not null, hander not null -> Validate if icm matches filter,if matches -> return handler from request
    /// filter is null, handler not is null -> Return an empty filter with handler from payload
    /// filter not null, handler is null -> Validate if icm matches filter, handler returns null
    /// filter is null, handler is null -> Get filter and handler from DB
    /// </summary>

    public async Task<(TIncidentFilterDocumentPayload, IncidentHandlerDocumentPayload?)> GetIncidentFilterAndHandlerAsync(IncidentHandlingRequestModel<TIncidentFilterDocumentPayload> request, TIncidentDocument incidentDetails)
    {
        string handlerId = $"IncidentHandler{incidentDetails.DocumentType}";
        string filterId = $"IncidentFilter{incidentDetails.DocumentType}";
        var defaultFilter = GetDefaultIncidentFilter(request);
        var defaultHandler = new IncidentHandlerDocumentPayload()
        {
            Id = request?.IncidentHandler?.Name ?? handlerId,
            Name = request?.IncidentHandler?.Name ?? handlerId,
            Description = request?.IncidentHandler?.Description ?? "",
            IncidentFilterId = filterId,
            IncidentProcessingGuide = request?.IncidentHandler?.IncidentProcessingGuide ?? [],
            Incidents = request?.IncidentHandler?.Incidents ?? [],
            Tools = request?.IncidentHandler?.Tools ?? [],
            CustomInstructions = request?.IncidentHandler?.CustomInstructions ?? "",
        };

        var filters = new List<TIncidentFilterDocumentPayload>();
        var matchingFilter = defaultFilter;
        switch (request?.IncidentFilter, request?.IncidentHandler)
        {
            case (IncidentFilterDocumentPayload _, IncidentHandlerDocumentPayload _):
                _logger.LogInternalInformation("[IncidentHandlingServiceBase] GetIncidentFilterAndHandlerAsync: Request has IncidentHandler and IncidentFilter, Check if incident matches given filter and return handler from request if matches");
                filters = [defaultFilter];
                matchingFilter = GetIncidentFilter(filters, incidentDetails);
                return (matchingFilter, defaultHandler);

            case (null, IncidentHandlerDocumentPayload _):
                _logger.LogInternalInformation("[IncidentHandlingServiceBase] GetIncidentFilterAndHandlerAsync: Request only has IncidentHandler, no IncidentFilter, return handler for next step");
                return (defaultFilter, defaultHandler);

            case (IncidentFilterDocumentPayload _, null):
                _logger.LogInternalInformation("[IncidentHandlingServiceBase] GetIncidentFilterAndHandlerAsync: Request only has IncidentFilter, no IncidentHandler, Check if incident matches given filter");
                filters = [defaultFilter];
                matchingFilter = GetIncidentFilter(filters, incidentDetails);
                return (matchingFilter, null);

            case (null, null):
            default:
                _logger.LogInternalInformation("[IncidentHandlingServiceBase] GetIncidentFilterAndHandlerAsync: Fetching incident filters for IncidentId: {IncidentId}", incidentDetails.Id);
                var incidentFilterDocs = await _incidentFilterManagementService.ListIncidentFilters(false);
                filters = [.. incidentFilterDocs.Select(f => (TIncidentFilterDocumentPayload)f)];
                _logger.LogInternalInformation("[BaseIncidentService] GetIncidentFilterAndHandlerAsync: Retrieved {FilterCount} filters for IncidentId: {IncidentId}", filters.Count, incidentDetails.Id);

                matchingFilter = GetIncidentFilter(filters, incidentDetails);

                _logger.LogInternalInformation("[IncidentHandlingServiceBase] GetIncidentFilterAndHandlerAsync: Fetching incident handlers for FilterId: {FilterId}", matchingFilter.Id);
                var incidentHandlers = await _incidentHandlerManagementService.ListIncidentHandlers();
                _logger.LogInternalInformation("[IncidentHandlingServiceBase] GetIncidentFilterAndHandlerAsync: Retrieved {HandlerCount} handlers for FilterId: {FilterId}", incidentHandlers.Count, matchingFilter.Id);
                var matchingHandler = incidentHandlers.Where(x => x.IncidentFilterId == matchingFilter.Id).FirstOrDefault();

                return (matchingFilter, matchingHandler);
        }
    }

    /// <summary>
    /// Gets the incident filter that matches an incident
    /// </summary>
    /// <param name="filters">List of available filters</param>
    /// <param name="incidentDetails">The incident details</param>
    /// <returns>The matching filter</returns>
    protected virtual TIncidentFilterDocumentPayload GetIncidentFilter(List<TIncidentFilterDocumentPayload> filters, TIncidentDocument incidentDetails)
    {
        var matchingFilters = filters
            .Where(filter =>
                (string.IsNullOrWhiteSpace(filter.ImpactedService) || filter.ImpactedService == incidentDetails.ImpactedServiceId || filter.ImpactedService == incidentDetails.ImpactedServiceName)
                &&
                (string.IsNullOrWhiteSpace(filter.Priority) || IsPriorityMatch(filter.Priority, incidentDetails.Priority))
                &&
                (string.IsNullOrWhiteSpace(filter.IncidentType) || (filter.IncidentType == incidentDetails.IncidentType))
                &&
                (string.IsNullOrWhiteSpace(filter.TitleContains) || (incidentDetails.Title?.Contains(filter.TitleContains, StringComparison.OrdinalIgnoreCase) ?? false))
            )
            .ToList();

        _logger.LogInternalInformation("[IncidentHandlingServiceBase] GetIncidentFilter: Found {MatchingFilterCount} matching filters for IncidentId: {IncidentId}", matchingFilters.Count, incidentDetails.Id);

        if (matchingFilters is null || matchingFilters.Count == 0)
        {
            throw new IncidentFilterNotFoundException();
        }

        var matchingFilter = matchingFilters.First();
        return matchingFilter;
    }

    /// <summary>
    /// Checks if the filter priority matches the incident priority.
    /// Uses simple string comparison as the default implementation.
    /// Override in derived classes to implement custom priority matching logic.
    /// </summary>
    /// <param name="filterPriority">The priority from the filter</param>
    /// <param name="incidentPriority">The priority from the incident</param>
    /// <returns>True if priorities match, false otherwise</returns>
    protected virtual bool IsPriorityMatch(string filterPriority, string incidentPriority)
    {
        // Default implementation: simple string comparison
        // Override in specific implementations (e.g., ServiceNow) for custom logic
        return string.Equals(filterPriority, incidentPriority, StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Registers a dynamic YAML agent with the agent factory
    /// </summary>
    /// <param name="descriptor">The YAML agent descriptor</param>
    /// <returns>Task</returns>
    protected virtual Task RegisterDynamicYamlAgent(YamlAgentDescriptor descriptor)
    {
        // Ensure the agent has the status tool
        if (!descriptor.Tools.Contains("NotifyUser"))
        {
            descriptor.Tools.Insert(0, "NotifyUser");
        }

        // Register with agent factory
        _agentFactory.LoadAgentFromDescriptor(descriptor, isCustomAgent: true);

        // Update handoffs to include this agent in meta_agent if needed
        _agentFactory.UpdateHandoffs();

        return Task.CompletedTask;
    }
}

/// <summary>
/// Base service that provides common incident handling functionality
/// </summary>
public abstract class IncidentHandlingService<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload, TIncident> : IncidentHandlingServiceBase<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload>
    where TIncidentDocument : IIncidentDocument
    where TIncidentFilterDocument : TIncidentFilterDocumentPayload, IIncidentFilterDocument, new()
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
    where TIncident : class
{
    protected readonly IThreadRepository _repository;
    protected readonly IAgentInboundCommunicationService _inboundCommunicationService;
    protected readonly IIncidentStatusMetricsService _incidentStatusMetricsService;
    protected readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
    protected readonly IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload, TIncident> _incidentAnalysisService;
    protected readonly Tracer _tracer;
    protected readonly ExperimentalSettings _experimentalSettings;

    public IncidentHandlingService(
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IIncidentStatusMetricsService incidentStatusMetricsService,
        IAgentOutboundCommunicationService agentOutboundCommunicationService,
        IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload, TIncident> incidentAnalysisService,
        ILogger logger,
        Tracer tracer,
        IAgentFactory<AgentContext> agentFactory,
        ExperimentalSettings experimentalSettings) : base(incidentFilterManagementService, incidentManagementService, incidentHandlerManagementService, agentFactory, logger)
    {
        _repository = repository;
        _inboundCommunicationService = inboundCommunicationService;
        _incidentStatusMetricsService = incidentStatusMetricsService;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
        _incidentAnalysisService = incidentAnalysisService;
        _tracer = tracer;
        _experimentalSettings = experimentalSettings;
    }

    protected abstract IncidentManagementType IncidentType { get; }

    protected abstract Task<Thread> CreateIncidentHandlerAgentThreadAsync(
        TIncidentDocument incidentDetails,
        IncidentHandlerDocumentPayload incidentHandler,
        TIncidentFilterDocumentPayload incidentFilter,
        IncidentHandlingRequestModelBase request);

    /// <summary>
    /// Builds a system prompt from the incident handler configuration
    /// </summary>
    /// <param name="handler">The incident handler document</param>
    /// <param name="incident">The incident document</param>
    /// <returns>The formatted system prompt</returns>
    protected virtual string BuildSystemPromptFromHandler(
        IncidentHandlerDocumentPayload handler,
        TIncidentDocument incident)
    {
        var promptBuilder = new StringBuilder();

        // Base instruction
        promptBuilder.AppendLine($"You are an incident handler agent for {handler.Name}.");
        promptBuilder.AppendLine($"You are handling incident: {incident.Title}");
        promptBuilder.AppendLine();

        // Add instructions for status updates using the tool
        promptBuilder.AppendLine("IMPORTANT: Use the 'NotifyUser' tool to provide status updates as you work through the incident. Do not provide repetitive updates. Only send updates about new steps that are being taken.");
        promptBuilder.AppendLine("Send status updates for major steps like:");
        promptBuilder.AppendLine("- Starting investigation");
        promptBuilder.AppendLine("- Analyzing metrics or logs");
        promptBuilder.AppendLine("- Identifying root cause");
        promptBuilder.AppendLine("- Applying remediation");
        promptBuilder.AppendLine();

        // Add processing guide as instructions
        if (handler.IncidentProcessingGuide?.Count > 0)
        {
            promptBuilder.AppendLine("Follow these incident processing guidelines:");
            foreach (var guideline in handler.IncidentProcessingGuide)
            {
                promptBuilder.AppendLine($"- {guideline}");
            }
            promptBuilder.AppendLine();
        }

        // Add custom instructions
        if (!string.IsNullOrEmpty(handler.CustomInstructions))
        {
            promptBuilder.AppendLine("Additional Instructions:");
            promptBuilder.AppendLine(handler.CustomInstructions);
        }

        return promptBuilder.ToString();
    }



    protected override async Task<IncidentHandlingResponseModel> HandleIncidentInternalAsync(TIncidentDocument incidentDetails, TIncidentFilterDocumentPayload matchingFilter, IncidentHandlerDocumentPayload? matchingHandler, IncidentHandlingRequestModelBase request)
    {
        Thread? thread = null;
        try
        {
            _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Invoked for IncidentId: {IncidentId}", incidentDetails.Id);
            if (matchingHandler is null)
            {
                _logger.LogInternalWarning("[IncidentHandlingService] HandleIncidentAsync: No matching handler found for FilterId: {FilterId}, using MetaAgent", matchingFilter.Id);

                var incidentRequest = new IncidentHandlingRequestModelBase
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
                thread = await CreateIncidentMetaAgentThread(incidentRequest, matchingFilter, matchingFilter.HandlingAgent ?? string.Empty);
                _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Created MetaAgent thread for Incident '{IncidentId}' matched Filter '{FilterId}' with HandlingAgent '{HandlingAgent}' (no Handler), created Thread '{ThreadId}'",
                    request.IncidentId,
                    matchingFilter.Id,
                    matchingFilter.HandlingAgent ?? "meta_agent",
                    thread.Id);
                _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Created MetaAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", thread.Id, request.IncidentId);

                var incidentStatusMetrics = await _incidentStatusMetricsService.GetIncidentStatusMetricsAsync(null, DateTime.Now);
                await _agentOutboundCommunicationService.NotifyIncidentStatusMetrics(thread.Id, incidentStatusMetrics);

                try
                {
                    var data = ToIncidentActivitySnapshot(matchingFilter, incidentDetails, incidentRequest, matchingHandler);
                    _incidentAnalysisService.Ingest(data);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError($"[IncidentHandlingService] HandleIncidentAsync: Error logging incident handling data to Incident Analysis Service; {ex.Message}");
                }

                return new IncidentHandlingResponseModel
                {
                    StatusCode = 200,
                    Message = "Incident received",
                    IncidentId = request.IncidentId,
                    ThreadId = thread.Id
                };
            }

            _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Matched Handler. Creating IncidentHandlerAgent thread for IncidentId: {IncidentId}, FilterId: {FilterId} and HandlerId: {HandlerId}", request.IncidentId, matchingFilter.Id, matchingHandler.Id);

            // Check if YAML-based incident handling is enabled
            if (_experimentalSettings.UseYamlForIncidentHandling)
            {
                _logger.LogInternalInformation("[IncidentHandlingService] Using YAML-based incident handling for IncidentId: {IncidentId}", request.IncidentId);
                thread = await CreateIncidentHandlerAgentThreadAsync(incidentDetails, matchingHandler, matchingFilter, request);
            }
            else
            {
                _logger.LogInternalInformation("[IncidentHandlingService] Using legacy incident handling for IncidentId: {IncidentId}", request.IncidentId);
                thread = await CreateIncidentHandlerAgentThreadAsync(incidentDetails, matchingHandler, matchingFilter, request);
            }

            _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync:Created IncidentHandlerAgent thread for Incident '{IncidentId}' matched Filter '{FilterId}' with HandlingAgent '{HandlingAgent}' and Handler '{HandlerId}', created Thread '{ThreadId}'",
                request.IncidentId,
                matchingFilter.Id,
                matchingFilter.HandlingAgent ?? "none",
                matchingHandler.Id,
                thread.Id);
            _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Created IncidentHandlerAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId} and HandlerId: {HandlerId}", thread.Id, request.IncidentId, matchingHandler.Id);

            try
            {
                var data = ToIncidentActivitySnapshot(matchingFilter, incidentDetails, request, matchingHandler);
                _incidentAnalysisService.Ingest(data);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"[IncidentHandlingService] HandleIncidentAsync: Error logging incident handling data to Incident Analysis Service; {ex.Message}");
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
            _logger.LogInternalError(ex, "[IncidentHandlingService] HandleIncidentAsync: Error processing IncidentId: {IncidentId}", request.IncidentId);
            return new IncidentHandlingResponseModel
            {
                StatusCode = 500,
                Message = "Failed to process Incident",
                IncidentId = request.IncidentId,
                ThreadId = thread?.Id
            };
        }
    }

    /// <summary>
    /// Creates a meta agent thread for handling incidents without a specific handler
    /// </summary>
    /// <param name="request">The incident request</param>
    /// <param name="incidentFilter">The matching incident filter</param>
    /// <returns>The created thread</returns>
    public async Task<Thread> CreateIncidentMetaAgentThread(IncidentHandlingRequestModelBase request, TIncidentFilterDocumentPayload incidentFilter, string currentAgent)
    {
        _logger.LogInternalInformation("[IncidentHandlingService] CreateIncidentMetaAgentThread: Invoked for IncidentId: {IncidentId}", request.IncidentId);
        try
        {
            var messageBuilder = new StringBuilder();

            var incidentMessage = $"🚨 **New {(!string.IsNullOrEmpty(request.Source) ? request.Source : String.Empty)} Incident Reported**\n\n" +
                $"**Title:** {request.Title}\n\n" +
                $"**Description:** {request.Description}\n\n";

            if (!string.IsNullOrEmpty(request.IncidentId))
            {
                incidentMessage += $"**Incident ID:** {request.IncidentId}\n\n";
            }
            if (!string.IsNullOrEmpty(request.Severity))
            {
                incidentMessage += $"**Severity:** {request.Severity}\n\n";
            }
            if (!string.IsNullOrEmpty(request.Source))
            {
                incidentMessage += $"**Source:** {request.Source}\n\n";
            }
            if (request.AdditionalProperties?.Count > 0)
            {
                incidentMessage += "**Additional Details:**\n";
                foreach (var prop in request.AdditionalProperties)
                {
                    incidentMessage += $"- {prop.Key}: {prop.Value}\n";
                }
                incidentMessage += "\n";
            }

            bool isTest = request.IsTest;
            (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
                title: $"Incident Report - {request.Title}",
                message: incidentMessage,
                agentTypeEnum: AgentTypeEnum.Meta,
                source: ThreadSource.Incident,
                incidentId: request.IncidentId ?? string.Empty,
                threadType: isTest ? ThreadType.Test : ThreadType.Prod,
                overrideAgentMode: incidentFilter.AgentMode,
                incidentDetails: new IncidentDetails(
                    request.Title ?? String.Empty,
                    request.CreatedTime ?? new DateTimeOffset(),
                    request.Severity ?? String.Empty,
                    request.ImpactedService ?? String.Empty,
                    incidentFilter.Id ?? String.Empty,
                    String.Empty,
                    InvestigationStatus.InProgress)
            );

            if (!string.IsNullOrEmpty(currentAgent))
            {
                // Update agent context to use specified current agent
                agentContext = agentContext with { CurrentAgent = currentAgent };
                await _repository.UpdateAgentContextAsync(agentContext);
            }

            _logger.LogInternalInformation("[IncidentHandlingService] CreateIncidentMetaAgentThread: Created thread with ThreadId: {ThreadId} for IncidentId: {IncidentId} with CurrentAgent: {CurrentAgent}", thread.Id, request.IncidentId, currentAgent);

            // Emit agent action telemetry for meta thread creation with incident source
            try
            {
                var param = JsonSerializer.Serialize(new { IncidentSource = IncidentType.ToString() ?? string.Empty, IncidentId = request.IncidentId ?? string.Empty, HandlerId = incidentFilter.Id ?? string.Empty });
                _logger.LogAgentAction(
                    action: AgentActionEvents.CreateThread,
                    parameter: param,
                    status: AgentActionStatus.Success,
                    duration: 0,
                    threadId: thread.Id.ToString(),
                    subAgentName: "",
                    threadSource: thread.Source.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "[IncidentHandlingService] CreateIncidentMetaAgentThread: Failed to emit LogAgentAction for CreateThread");
            }

            var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
            await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

            // Determine conversation modifier based on filter
            ConversationModifierEnum? conversationModifier = null;
            if (incidentFilter.DeepInvestigationEnabled)
            {
                conversationModifier = ConversationModifierEnum.DeepInvestigation;
                _logger.LogInternalInformation(
                    "[IncidentHandlingService] Deep Investigation enabled for incident {IncidentId} via filter {FilterId}",
                    request.IncidentId,
                    incidentFilter.Id);
            }

            await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                ThreadId: thread.Id,
                AgentContextId: agentContext.Id,
                MessageId: thread.StartMessage?.Id ?? new Guid(),
                Message: messageBuilder.ToString(),
                UserId: "incident-system",
                DisplayName: request.Source ?? "Incident System",
                Timestamp: DateTime.UtcNow,
                ConversationModifier: conversationModifier
            ), defaultHandler: true);

            return thread;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentHandlingService] CreateIncidentMetaAgentThread: Error for IncidentId: {IncidentId}", request.IncidentId);
            throw;
        }
    }

    protected async Task<Thread> CreateIncidentHandlerAgentThreadInternalAsync(
        TIncidentDocument incidentDetails,
        IncidentHandlerDocumentPayload incidentHandler,
        TIncidentFilterDocumentPayload incidentFilterPayload,
        IncidentHandlingRequestModelBase request,
        string sourceSystem,
        Func<TIncidentDocument, string>? getSourceSpecificAdditionalProperties = null)
    {
        var logPrefix = $"[IncidentHandlingService]";
        var span = _tracer != null ?
            _tracer.StartSpan(TraceOperationName.IncidentCreateThread, SpanKind.Internal) :
            null;

        using (span)
        {
            _logger.LogInternalInformation($"{logPrefix} CreateIncidentHandlerAgentThreadInternalAsync: Invoked for IncidentId: {{IncidentId}}, HandlerId: {{HandlerId}}", incidentDetails.Id, incidentHandler.Id);
            try
            {
                var title = incidentDetails.Title ?? "New Incident";
                var alertMessage = $"🚨 **New {sourceSystem} Incident Reported**\n\n" +
                    $"**Title:** {title}\n\n" +
                    $"**Description:** {incidentDetails.Description}\n\n" +
                    $"**Incident ID:** {incidentDetails.Id}\n\n" +
                    $"**Severity:** {incidentDetails.Priority ?? "Unknown"}\n\n" +
                    $"**Source:** {incidentDetails.DocumentType}\n\n";

                // Add source-specific properties if provided
                if (getSourceSpecificAdditionalProperties != null)
                {
                    var additionalProps = getSourceSpecificAdditionalProperties(incidentDetails);
                    if (!string.IsNullOrEmpty(additionalProps))
                    {
                        alertMessage += additionalProps;
                    }
                }

                var customInstructionsForAlert = incidentHandler.IncidentProcessingGuide != null && incidentHandler.IncidentProcessingGuide.Count > 0 ?
                    string.Join("\n", incidentHandler.IncidentProcessingGuide.Select(x => $"* {x}")) :
                    "No custom instructions provided for this incident type.";

                alertMessage =
                    $"{alertMessage}\n\n" +
                    $"**Custom Instructions for Incident Processing:**\n" +
                    $"{customInstructionsForAlert}\n\n" +
                    $"**Incident Handler:** {incidentHandler.Name}";

                // NEW: Create dynamic YAML agent from incident handler
                var dynamicAgentName = $"incident_handler_{incidentHandler.Id}";

                // Build system prompt from incident processing guide
                var systemPrompt = BuildSystemPromptFromHandler(incidentHandler, incidentDetails);

                // Create YAML agent descriptor
                var yamlDescriptor = new YamlAgentDescriptor
                {
                    Name = dynamicAgentName,
                    Instructions = systemPrompt,
                    Tools = incidentHandler.Tools ?? [],
                    Handoffs = [], // Can be extended if needed
                    AllowParallelToolCalls = false,
                    Temperature = 0.7f
                };

                // Register the agent dynamically
                await RegisterDynamicYamlAgent(yamlDescriptor);

                (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
                    title: $"Incident - {title}",
                    message: alertMessage,
                    agentTypeEnum: _experimentalSettings.UseYamlForIncidentHandling ? AgentTypeEnum.Meta : AgentTypeEnum.Incident, // Use Meta to trigger YAML agent framework
                    source: ThreadSource.Incident,
                    incidentId: incidentDetails.Id ?? string.Empty,
                    AllowedTools: incidentHandler.Tools,
                    threadType: request.IsTest ? ThreadType.Test : ThreadType.Prod,
                    overrideAgentMode: incidentFilterPayload.AgentMode,
                    incidentDetails: new IncidentDetails(
                        title,
                        incidentDetails.CreatedAt,
                        incidentDetails.Priority ?? String.Empty,
                        incidentDetails.ImpactedServiceName,
                        incidentHandler.IncidentFilterId,
                        incidentHandler.Id,
                        InvestigationStatus.InProgress)
                );

                if (_experimentalSettings.UseYamlForIncidentHandling)
                {
                    // Update agent context to use our dynamic agent
                    agentContext = agentContext with { CurrentAgent = dynamicAgentName };
                    await _repository.UpdateAgentContextAsync(agentContext);
                }

                if (span != null)
                {
                    span.SetAttribute(TraceAttribute.OperationName, TraceOperationName.IncidentCreateThread);
                    span.SetAttribute(TraceAttribute.ThreadId, thread.Id.ToString());
                    span.SetAttribute(TraceAttribute.IncidentId, incidentDetails.Id);
                    span.SetAttribute(TraceAttribute.IncidentSource, incidentDetails.DocumentType);
                    span.SetAttribute(TraceAttribute.IncidentMessage, alertMessage);
                    span.SetAttribute(TraceAttribute.IncidentHandler, incidentHandler.Name);
                }

                _logger.LogInternalInformation($"{logPrefix} CreateIncidentHandlerAgentThreadInternalAsync: Created thread with ThreadId: {{ThreadId}} for IncidentId: {{IncidentId}} HandlerId: {{HandlerId}}", thread.Id, incidentDetails.Id, incidentHandler.Id);

                // Emit agent action telemetry for thread creation with incident source
                try
                {
                    var param = JsonSerializer.Serialize(new { IncidentSource = IncidentType.ToString() ?? string.Empty, HandlerId = incidentHandler.Id ?? string.Empty, IncidentId = request.IncidentId ?? string.Empty });
                    _logger.LogAgentAction(
                        action: AgentActionEvents.CreateThread,
                        parameter: param,
                        status: AgentActionStatus.Success,
                        duration: 0,
                        threadId: thread.Id.ToString(),
                        subAgentName: "",
                        threadSource: thread.Source.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, $"{logPrefix} CreateIncidentHandlerAgentThreadInternalAsync: Failed to emit LogAgentAction for CreateThread");
                }

                var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
                await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

                // Determine conversation modifier based on filter
                ConversationModifierEnum? conversationModifier = null;
                if (incidentFilterPayload.DeepInvestigationEnabled)
                {
                    conversationModifier = ConversationModifierEnum.DeepInvestigation;
                    _logger.LogInternalInformation(
                        $"{logPrefix}Deep Investigation enabled for incident {request.IncidentId} via filter {incidentFilterPayload.Id}",
                        incidentDetails.Id,
                        incidentFilterPayload.Id);
                }

                await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                    ThreadId: thread.Id,
                    AgentContextId: agentContext.Id,
                    MessageId: thread.StartMessage?.Id ?? new Guid(),
                    Message: "Process the incident as per custom instructions provided",
                    UserId: "incident-system",
                    DisplayName: "Incident System",
                    Timestamp: DateTime.UtcNow,
                    ConversationModifier: conversationModifier
                ), defaultHandler: _experimentalSettings.UseYamlForIncidentHandling);

                return thread;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"{logPrefix} CreateIncidentHandlerAgentThreadInternalAsync: Error for IncidentId: {{IncidentId}}, HandlerId: {{HandlerId}}", incidentDetails.Id, incidentHandler.Id);
                throw;
            }
        }
    }

    protected virtual IncidentAIData ToIncidentActivitySnapshot(TIncidentFilterDocumentPayload filter, TIncidentDocument incidentDetails, IncidentHandlingRequestModelBase request, IncidentHandlerDocumentPayload? handler)
    {
        IncidentAIData snapShot = new IncidentAIData
        {
            HandlerId = filter.Id ?? filter.Name ?? "no-handler",
            IncidentId = incidentDetails.Id,
            IncidentTitle = incidentDetails.Title,
            IncidentCreatedAt = !IsMinDateTime(incidentDetails.CreatedAt) ? incidentDetails.CreatedAt : DateTime.UtcNow,
            IncidentUpdatedAt = !IsMinDateTime(incidentDetails.UpdatedAt) ? incidentDetails.UpdatedAt : !IsMinDateTime(incidentDetails.CreatedAt) ? incidentDetails.CreatedAt : DateTime.UtcNow,
            HandlerCreatedAt = !IsMinDateTime(filter.CreatedAt) ? filter.CreatedAt : DateTime.UtcNow,
            HandlerUpdatedAt = !IsMinDateTime(filter.UpdatedAt) ? filter.UpdatedAt : DateTime.UtcNow,
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

    protected bool IsMinDateTime(DateTime? time)
    {
        if (time == null)
        {
            return true;
        }
        return time <= DateTime.MinValue.AddDays(1);
    }
}

internal class IncidentFilterNotFoundException : Exception
{
    public IncidentFilterNotFoundException() : base("Cannot find matching Incident Filter")
    {
    }
}
