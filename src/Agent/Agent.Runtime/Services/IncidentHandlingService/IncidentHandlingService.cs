using System.ComponentModel.DataAnnotations;
using System.Text;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Agent.Runtime.Services;


public class IncidentRequest<TIncidentFilterDocumentPayload> where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    public Dictionary<string, string>? AdditionalProperties { get; set; }
    public bool IsTest { get; set; } = false;
    public IncidentHandlerDocumentPayload? IncidentHandler { get; set; }
    public TIncidentFilterDocumentPayload? IncidentFilter { get; set; }
}

public class IncidentHandlingRequestModel<TIncidentFilterDocumentPayload> : IncidentRequest<TIncidentFilterDocumentPayload>
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    [Required]
    public required string IncidentId { set; get; }
    public string? Severity { get; set; }
    public string? Source { get; set; }
}

public class IncidentHandlingResponseModel
{
    public int StatusCode { get; set; }
    public object? Response { get; set; }
}

public interface IIncidentHandlingService<TIncidentFilterDocumentPayload> where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    Task<IncidentHandlingResponseModel> HandleIncidentAsync(IncidentHandlingRequestModel<TIncidentFilterDocumentPayload>? incidentDocument);
}

/// <summary>
/// Base service that provides common incident handling functionality
/// </summary>
public abstract class IncidentHandlingServiceBase<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload> : IIncidentHandlingService<TIncidentFilterDocumentPayload>
    where TIncidentDocument : IIncidentDocument
    where TIncidentFilterDocument : TIncidentFilterDocumentPayload, IIncidentFilterDocument, new()
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    protected readonly IThreadRepository _repository;
    protected readonly IAgentInboundCommunicationService _inboundCommunicationService;
    protected readonly IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> _incidentFilterManagementService;
    protected readonly IIncidentHandlerManagementService _incidentHandlerManagementService;
    protected readonly ILogger _logger;
    protected readonly Tracer _tracer;

    public IncidentHandlingServiceBase(
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        ILogger logger,
        Tracer tracer)
    {
        _repository = repository;
        _inboundCommunicationService = inboundCommunicationService;
        _incidentFilterManagementService = incidentFilterManagementService;
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _logger = logger;
        _tracer = tracer;
    }

    protected abstract Task<TIncidentDocument> GetIncidentAsync(string incidentId);
    protected abstract Task<Core.Models.Api.v1.Thread> CreateIncidentHandlerAgentThreadAsync(
        TIncidentDocument incidentDetails,
        IncidentHandlerDocument incidentHandler,
        TIncidentFilterDocument incidentFilterDocument,
        IncidentHandlingRequestModel<TIncidentFilterDocumentPayload> request);

    protected abstract TIncidentFilterDocument GetDefaultIncidentFilter(IncidentHandlingRequestModel<TIncidentFilterDocumentPayload> request);


    public async Task<IncidentHandlingResponseModel> HandleIncidentAsync(IncidentHandlingRequestModel<TIncidentFilterDocumentPayload>? request)
    {
        if(request is null)
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

            var (matchingFilter, matchingHandler) = await GetIncidentFilterAndHandlerAsync(request, incidentDetails);

            if (matchingHandler == null)
            {
                _logger.LogInternalWarning("[IncidentHandlingService] HandleIncidentAsync: No matching handler found for FilterId: {FilterId}, using MetaAgent", matchingFilter.Id);

                var incidentRequest = new IncidentHandlingRequestModel<TIncidentFilterDocumentPayload>
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

                var defaultThread = await CreateIncidentMetaAgentThread(incidentRequest, matchingFilter);
                _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Created MetaAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", defaultThread.Id, incidentId);

                response.StatusCode = 200;
                response.Response = new { threadId = defaultThread.Id, message = "Incident received" };
                return response;
            }

            _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Matched Handler. Creating IncidentHandlerAgent thread for IncidentId: {IncidentId}, FilterId: {FilterId} and HandlerId: {HandlerId}", incidentId, matchingFilter.Id, matchingHandler.Id);
            var thread = await CreateIncidentHandlerAgentThreadAsync(incidentDetails, matchingHandler, matchingFilter, request);
            _logger.LogInternalInformation("[IncidentHandlingService] HandleIncidentAsync: Created IncidentHandlerAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId} and HandlerId: {HandlerId}", thread.Id, incidentId, matchingHandler.Id);

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

    /// <summary>
    /// Creates a meta agent thread for handling incidents without a specific handler
    /// </summary>
    /// <param name="request">The incident request</param>
    /// <param name="incidentFilterDocument">The matching incident filter</param>
    /// <returns>The created thread</returns>
    public async Task<Core.Models.Api.v1.Thread> CreateIncidentMetaAgentThread(IncidentHandlingRequestModel<TIncidentFilterDocumentPayload> request, TIncidentFilterDocument incidentFilterDocument)
    {
        _logger.LogInternalInformation("[BaseIncidentService] CreateIncidentMetaAgentThread: Invoked for IncidentId: {IncidentId}", request.IncidentId);
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
                overrideAgentMode: incidentFilterDocument.AgentMode
            );

            _logger.LogInternalInformation("[BaseIncidentService] CreateIncidentMetaAgentThread: Created thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", thread.Id, request.IncidentId);

            var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
            await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Core.Models.Api.v1.Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

            await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                ThreadId: thread.Id,
                AgentContextId: agentContext.Id,
                MessageId: thread.StartMessage?.Id ?? new Guid(),
                Message: messageBuilder.ToString(),
                UserId: "incident-system",
                DisplayName: request.Source ?? "Incident System",
                Timestamp: DateTime.UtcNow
            ), defaultHandler: true);

            return thread;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[BaseIncidentService] CreateIncidentMetaAgentThread: Error for IncidentId: {IncidentId}", request.IncidentId);
            throw;
        }
    }

    /// <summary>
    /// Gets the incident filter that matches an incident
    /// </summary>
    /// <param name="filters">List of available filters</param>
    /// <param name="incidentDetails">The incident details</param>
    /// <returns>The matching filter</returns>
    protected virtual TIncidentFilterDocument GetIncidentFilter(List<TIncidentFilterDocument> filters, TIncidentDocument incidentDetails)
    {
        var matchingFilters = filters
            .Where(filter =>
                ((string.IsNullOrWhiteSpace(filter.ImpactedService)) || (filter.ImpactedService == incidentDetails.ImpactedServiceId || filter.ImpactedService == incidentDetails.ImpactedServiceName))
                &&
                ((string.IsNullOrWhiteSpace(filter.Priority)) || (filter.Priority == incidentDetails.Priority))
                &&
                ((string.IsNullOrWhiteSpace(filter.IncidentType)) || (filter.IncidentType == incidentDetails.IncidentType))
                &&
                (string.IsNullOrWhiteSpace(filter.TitleContains) || (incidentDetails.Title?.Contains(filter.TitleContains, StringComparison.OrdinalIgnoreCase) ?? false))
            )
            .ToList();

        _logger.LogInternalInformation("[BaseIncidentService] GetIncidentFilter: Found {MatchingFilterCount} matching filters for IncidentId: {IncidentId}", matchingFilters.Count, incidentDetails.Id);

        if (matchingFilters == null || matchingFilters.Count == 0)
        {
            throw new IncidentFilterNotFoundException();
        }

        var matchingFilter = matchingFilters.First();
        return matchingFilter;
    }

    /// <summary>
    /// For IncidentHandlingRequestModel request:
    /// filter not null, hander not null -> Validate if icm matches filter,if matches -> return handler from request
    /// filter is null, handler is null -> Return an empty filter with handler
    /// filter not null, handler is null -> Validate if icm matches filter, handler returns null
    /// filter is null, handler is null -> Get filter and handler from DB
    /// </summary>

    public async Task<(TIncidentFilterDocument, IncidentHandlerDocument?)> GetIncidentFilterAndHandlerAsync(IncidentHandlingRequestModel<TIncidentFilterDocumentPayload> request, TIncidentDocument incidentDetails)
    {
        string handlerId = $"IncidentHandler{incidentDetails.DocumentType}";
        string filterId = $"IncidentFilter{incidentDetails.DocumentType}";
        var defaultFilter = GetDefaultIncidentFilter(request);
        var defaultHandler = new IncidentHandlerDocument(
                Id: handlerId,
                DocumentType: handlerId,
                Name: request?.IncidentHandler?.Name ?? handlerId,
                Description: request?.IncidentHandler?.Description ?? "",
                IncidentFilterId: filterId,
                IncidentProcessingGuide: request?.IncidentHandler?.IncidentProcessingGuide ?? new List<string>(),
                Incidents: request?.IncidentHandler?.Incidents ?? new List<string>(),
                Tools: request?.IncidentHandler?.Tools ?? new List<string>(),
                CustomInstructions: request?.IncidentHandler?.CustomInstructions ?? "",
                CreatedAt: DateTime.UtcNow
            );

        var filters = new List<TIncidentFilterDocument>();
        var matchingFilter = defaultFilter;
        switch (request?.IncidentFilter, request?.IncidentHandler)
        {
            case (IncidentFilterDocumentPayload _, IncidentHandlerDocumentPayload _):
                _logger.LogInternalInformation("[BaseIncidentService] GetIncidentFilterAndHandlerAsync: Request has IncidentHandler and IncidentFilter, Check if incident matches given filter and return handler from request if matches");
                filters = new List<TIncidentFilterDocument>() { defaultFilter };
                matchingFilter = GetIncidentFilter(filters, incidentDetails);
                return (matchingFilter, defaultHandler);

            case (null, IncidentHandlerDocumentPayload _):
                _logger.LogInternalInformation("[BaseIncidentService] GetIncidentFilterAndHandlerAsync: Request only has IncidentHandler, no IncidentFilter, return handler for next step");
                return (defaultFilter, defaultHandler);

            case (IncidentFilterDocumentPayload _, null):
                _logger.LogInternalInformation("[BaseIncidentService] GetIncidentFilterAndHandlerAsync: Request only has IncidentFilter, no IncidentHandler, Check if incident matches given filter");
                filters = new List<TIncidentFilterDocument>() { defaultFilter };
                matchingFilter = GetIncidentFilter(filters, incidentDetails);
                return (matchingFilter, null);

            case (null, null):
            default:
                _logger.LogInternalInformation("[BaseIncidentService] GetIncidentFilterAndHandlerAsync: Fetching incident filters for IncidentId: {IncidentId}", incidentDetails.Id);
                filters = await _incidentFilterManagementService.ListIncidentFilters();
                _logger.LogInternalInformation("[BaseIncidentService] GetIncidentFilterAndHandlerAsync: Retrieved {FilterCount} filters for IncidentId: {IncidentId}", filters.Count, incidentDetails.Id);

                matchingFilter = GetIncidentFilter(filters, incidentDetails);

                _logger.LogInternalInformation("[BaseIncidentService] GetIncidentFilterAndHandlerAsync: Fetching incident handlers for FilterId: {FilterId}", matchingFilter.Id);
                var incidentHandlers = await _incidentHandlerManagementService.ListIncidentHandlers();
                _logger.LogInternalInformation("[BaseIncidentService] GetIncidentFilterAndHandlerAsync: Retrieved {HandlerCount} handlers for FilterId: {FilterId}", incidentHandlers.Count, matchingFilter.Id);
                var matchingHandler = incidentHandlers.Where(x => x.IncidentFilterId == matchingFilter.Id).FirstOrDefault();

                return (matchingFilter, matchingHandler);
        }
    }

    protected async Task<Core.Models.Api.v1.Thread> CreateIncidentHandlerAgentThreadInternalAsync(
        TIncidentDocument incidentDetails,
        IncidentHandlerDocument incidentHandler,
        TIncidentFilterDocument incidentFilterDocument,
        IncidentHandlingRequestModel<TIncidentFilterDocumentPayload> request,
        string sourceSystem,
        Func<TIncidentDocument, string>? getSourceSpecificAdditionalProperties = null)
    {
        var logPrefix = $"[BaseIncidentService]";
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
                (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
                    title: $"Incident - {title}",
                    message: alertMessage,
                    agentTypeEnum: AgentTypeEnum.Incident,
                    source: ThreadSource.Incident,
                    incidentId: incidentDetails.Id ?? string.Empty,
                    AllowedTools: incidentHandler.Tools,
                    threadType: request.IsTest ? ThreadType.Test : ThreadType.Prod,
                    overrideAgentMode: incidentFilterDocument.AgentMode
                );

                if (span != null)
                {
                    span.SetAttribute(TraceAttribute.OperationName, TraceOperationName.IncidentCreateThread);
                    span.SetAttribute(TraceAttribute.ThreadId, thread.Id.ToString());
                    span.SetAttribute(TraceAttribute.IncidentId, incidentDetails.Id);
                    span.SetAttribute(TraceAttribute.IncidentSource, incidentDetails.DocumentType);
                    span.SetAttribute(TraceAttribute.IncidentMessage, alertMessage);
                }

                _logger.LogInternalInformation($"{logPrefix} CreateIncidentHandlerAgentThreadInternalAsync: Created thread with ThreadId: {{ThreadId}} for IncidentId: {{IncidentId}}", thread.Id, incidentDetails.Id);

                var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
                await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

                await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                    ThreadId: thread.Id,
                    AgentContextId: agentContext.Id,
                    MessageId: thread.StartMessage?.Id ?? new Guid(),
                    Message: "Process the incident as per custom instructions provided",
                    UserId: "incident-system",
                    DisplayName: "Incident System",
                    Timestamp: DateTime.UtcNow
                ), defaultHandler: false);

                return thread;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"{logPrefix} CreateIncidentHandlerAgentThreadInternalAsync: Error for IncidentId: {{IncidentId}}, HandlerId: {{HandlerId}}", incidentDetails.Id, incidentHandler.Id);
                throw;
            }
        }
    }
}

internal class IncidentFilterNotFoundException : Exception
{
    public IncidentFilterNotFoundException() : base("Cannot find matching Incident Filter")
    {
    }
}
