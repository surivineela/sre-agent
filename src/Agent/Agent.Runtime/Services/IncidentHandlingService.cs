using System.ComponentModel.DataAnnotations;
using System.Text;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services
{
    public class IncidentHandlingRequestModel
    {
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        public string IncidentId { set; get; }

        public string? Severity { get; set; }

        public string? Source { get; set; }

        public Dictionary<string, string>? AdditionalProperties { get; set; }
    }

    public class IncidentHandlingResponseModel
    {
        public int StatusCode { get; set; }
        public object Response { get; set; }
    }

    public interface IIncidentHandlingService
    {
        Task<IncidentHandlingResponseModel> HandleIncidentAsync(IncidentHandlingRequestModel incidentDocument);
    }

    public class IncidentHandlingService : IIncidentHandlingService
    {
        private readonly IPagerDutyService _pagerDutyService;
        private readonly IICMAPIClient _icmApiClient;
        private readonly IAgentInboundCommunicationService _inboundCommunicationService;
        private readonly ILogger<IncidentHandlingService> _logger;
        private readonly IThreadRepository _repository;
        private readonly IIncidentFilterManagementService _incidentFilterManagementService;
        private readonly IIncidentHandlerManagementService _incidentHandlerManagementService;
        private readonly IIncidentManagementService<PagerDutyIncidentDocument> _pagerDutyincidentManagementService;
        private readonly IIncidentManagementService<IcmIncidentDocument> _icmIncidentManagementService;
        private readonly IncidentManagementSettings _incidentManagementSettings;

        public IncidentHandlingService(
            IPagerDutyService pagerDutyService,
            IICMAPIClient icmApiClient,
            IAgentInboundCommunicationService inboundCommunicationService,
            IThreadRepository repository,
            ILogger<IncidentHandlingService> logger,
            IIncidentFilterManagementService incidentFilterManagementService,
            IIncidentHandlerManagementService incidentHandlerManagementService,
            IIncidentManagementService<PagerDutyIncidentDocument> pagerDutyincidentManagementService,
            IIncidentManagementService<IcmIncidentDocument> icmIncidentManagementService,
        IncidentManagementSettings incidentManagementSettings)
        {
            _pagerDutyService = pagerDutyService;
            _icmApiClient = icmApiClient;
            _inboundCommunicationService = inboundCommunicationService;
            _repository = repository;
            _logger = logger;
            _incidentFilterManagementService = incidentFilterManagementService;
            _incidentHandlerManagementService = incidentHandlerManagementService;
            _pagerDutyincidentManagementService = pagerDutyincidentManagementService;
            _icmIncidentManagementService = icmIncidentManagementService;
            _incidentManagementSettings = incidentManagementSettings;
        }

        // Fix for CS8920: The interface 'IIncidentDocument' cannot be used as type argument. 
        // Static member 'ICosmosDocument.ContainerName' does not have a most specific implementation in the interface.

        private async Task<PagerDutyIncidentDocument> GetPagerDutyIncidentLatest(string incidentId)
        {
            _logger.LogInternalInformation("GetPagerDutyIncidentLatest: Invoked for IncidentId: {IncidentId}", incidentId);
            try
            {
                _logger.LogInternalInformation("GetPagerDutyIncidentLatest: Fetching incident for IncidentId: {IncidentId}", incidentId);
                var incidentData = await _pagerDutyService.GetPagerDutyIncidentAsync(incidentId);
                _logger.LogInternalInformation("GetPagerDutyIncidentLatest: Received incident data for IncidentId: {IncidentId}", incidentId);

                var incident = new PagerDutyIncidentDocument(
                    Id: incidentData.IncidentId,
                    HtmlUrl: incidentData.HtmlUrl,
                    Status: incidentData.Status,
                    Priority: incidentData.Priority?.Summary ?? string.Empty,
                    Urgency: incidentData.Urgency ?? string.Empty,
                    IncidentType: incidentData.IncidentType?.Name ?? string.Empty,
                    ImpactedServiceId: incidentData.ImpactedService?.Id ?? string.Empty,
                    ImpactedServiceName: incidentData.ImpactedService?.Summary ?? string.Empty,
                    CreatedAt: incidentData.CreatedAt);
                incident.Title = incidentData.Title;
                incident.Description = incidentData.Body.Details;

                _logger.LogInternalInformation("GetPagerDutyIncidentLatest: Successfully created PagerDutyIncidentDocument for IncidentId: {IncidentId}", incidentId);
                return incident;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GetPagerDutyIncidentLatest: Error occurred for IncidentId: {IncidentId}", incidentId);
                throw;
            }
        }

        private async Task<IcmIncidentDocument> GetIcmIncidentLatest(string incidentId)
        {
            _logger.LogInternalInformation("GetIcmIncidentLatest: Invoked for IncidentId: {IncidentId}", incidentId);
            try
            {
                _logger.LogInternalInformation("GetIcmIncidentLatest: Fetching Icm incident for IncidentId: {IncidentId}", incidentId);
                var incidentData = await _icmApiClient.GetIncidentAsync(incidentId);
                _logger.LogInternalInformation("GetIcmIncidentLatest: Received incident data for IncidentId: {IncidentId}", incidentId);

                var incident = new IcmIncidentDocument(incidentData);

                _logger.LogInternalInformation("GetIcmIncidentLatest: Successfully created IcmIncidentDocument for IncidentId: {IncidentId}", incidentId);
                return incident;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GetIcmIncidentLatest: Error occurred for IncidentId: {IncidentId}", incidentId);
                throw;
            }
        }

        private async Task<dynamic> GetIncidentAsync(string incidentId)
        {
            _logger.LogInternalInformation("GetIncidentAsync: Invoked for IncidentId: {IncidentId}", incidentId);
            try
            {
                switch (_incidentManagementSettings.Type)
                {
                    case IncidentManagementType.PagerDuty:
                        _logger.LogInternalInformation("GetIncidentAsync: Using PagerDuty for IncidentId: {IncidentId}", incidentId);
                        var incidentData = await _pagerDutyincidentManagementService.GetIncidentDetails(incidentId);
                        if (incidentData == null)
                        {
                            _logger.LogInternalWarning("GetIncidentAsync: No incident data found for IncidentId: {IncidentId}, fetching latest", incidentId);
                            incidentData = await GetPagerDutyIncidentLatest(incidentId);
                        }
                        _logger.LogInternalInformation("GetIncidentAsync: Returning incident data for IncidentId: {IncidentId}", incidentId);
                        return incidentData;
                    case IncidentManagementType.Icm:
                        _logger.LogInternalInformation("GetIncidentAsync: Using Icm for IncidentId: {IncidentId}", incidentId);
                        var icmIncidentData = await _icmIncidentManagementService.GetIncidentDetails(incidentId);
                        if (icmIncidentData == null)
                        {
                            _logger.LogInternalWarning("GetIncidentAsync: No incident data found for IncidentId: {IncidentId}, fetching latest", incidentId);
                            icmIncidentData = await GetIcmIncidentLatest(incidentId);
                        }
                        _logger.LogInternalInformation("GetIncidentAsync: Returning incident data for IncidentId: {IncidentId}", incidentId);
                        return icmIncidentData;
                    case IncidentManagementType.AzMonitor:
                        _logger.LogInternalWarning("GetIncidentAsync: Not implemented for IncidentManagementType: {Type}", _incidentManagementSettings.Type);
                        throw new NotImplementedException("ICM and Azure Monitor incident handling is not implemented yet.");
                    default:
                        _logger.LogInternalError(new NotSupportedException(), "GetIncidentAsync: Unsupported IncidentManagementType: {Type}", _incidentManagementSettings.Type);
                        throw new NotSupportedException($"Incident management type '{_incidentManagementSettings.Type}' is not supported.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GetIncidentAsync: Error occurred for IncidentId: {IncidentId}", incidentId);
                throw;
            }
        }

        public async Task<IncidentHandlingResponseModel> HandleIncidentAsync(IncidentHandlingRequestModel request)
        {
            _logger.LogInternalInformation("HandleIncidentAsync: Invoked for IncidentId: {IncidentId}", request.IncidentId);
            var incidentId = request.IncidentId;
            var response = new IncidentHandlingResponseModel();
            try
            {
                var incidentDetails = (IIncidentDocument)(await GetIncidentAsync(incidentId));

                _logger.LogInternalInformation("HandleIncidentAsync: Fetching incident filters for IncidentId: {IncidentId}", incidentId);
                var filters = await _incidentFilterManagementService.ListIncidentFilters();
                _logger.LogInternalInformation("HandleIncidentAsync: Retrieved {FilterCount} filters for IncidentId: {IncidentId}", filters?.Count ?? 0, incidentId);

                var matchingFilters = filters
                    .Where(filter =>
                        ((string.IsNullOrWhiteSpace(filter.ImpactedService)) || (filter.ImpactedService == incidentDetails.ImpactedServiceId || filter.ImpactedService == incidentDetails.ImpactedServiceName))
                        &&
                        ((string.IsNullOrWhiteSpace(filter.Priority)) || (filter.Priority == incidentDetails.Priority))
                        &&
                        ((string.IsNullOrWhiteSpace(filter.IncidentType)) || (filter.IncidentType == incidentDetails.IncidentType))
                        &&
                        (string.IsNullOrWhiteSpace(filter.TitleContains) || incidentDetails.Title.Contains(filter.TitleContains, StringComparison.OrdinalIgnoreCase))
                    )
                    .ToList();

                _logger.LogInternalInformation("HandleIncidentAsync: Found {MatchingFilterCount} matching filters for IncidentId: {IncidentId}", matchingFilters.Count, incidentId);

                if (matchingFilters == null || matchingFilters.Count == 0)
                {
                    _logger.LogInternalWarning("HandleIncidentAsync: No matching incident filters found for IncidentId: {IncidentId}", incidentId);
                    response.StatusCode = 404;
                    response.Response = "No matching incident filters found for this incident.";
                    return response;
                }

                var matchingFilter = matchingFilters.FirstOrDefault();

                _logger.LogInternalInformation("HandleIncidentAsync: Fetching incident handlers for FilterId: {FilterId}", matchingFilter.Id);
                var incidentHandlers = await _incidentHandlerManagementService.ListIncidentHandlers();
                _logger.LogInternalInformation("HandleIncidentAsync: Retrieved {HandlerCount} handlers for FilterId: {FilterId}", incidentHandlers?.Count ?? 0, matchingFilter.Id);

                var matchingHandler = incidentHandlers.Where(x => x.IncidentFilterId == matchingFilter.Id).FirstOrDefault();

                if (matchingHandler == null)
                {
                    _logger.LogInternalWarning("HandleIncidentAsync: No matching handler found for FilterId: {FilterId}, using MetaAgent", matchingFilter.Id);

                    var incidentRequest = new IncidentHandlingRequestModel
                    {
                        Title = incidentDetails.Title ?? "New Incident",
                        Description = incidentDetails.Description ?? "Alert notification.",
                        IncidentId = incidentDetails.Id,
                        Severity = incidentDetails.Priority,
                        Source = request.Source ?? incidentDetails.DocumentType,
                        AdditionalProperties = request.AdditionalProperties
                    };

                    var defaultThread = await CreateIncidentMetaAgentThread(incidentRequest);
                    _logger.LogInternalInformation("HandleIncidentAsync: Created MetaAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", defaultThread.Id, incidentId);

                    response.StatusCode = 200;
                    response.Response = new { threadId = defaultThread.Id, message = "Incident received" };
                    return response;
                }

                _logger.LogInternalInformation("HandleIncidentAsync: Matched Handler. Creating IncidentHandlerAgent thread for IncidentId: {IncidentId}, FilterId: {FilterId} and HandlerId: {HandlerId}", incidentId, matchingFilter.Id, matchingHandler.Id);
                var thread = await CreateIncidentHandlerAgentThread(incidentDetails, matchingHandler);
                _logger.LogInternalInformation("HandleIncidentAsync: Created IncidentHandlerAgent thread with ThreadId: {ThreadId} for IncidentId: {IncidentId} and HandlerId: {HandlerId}", thread.Id, incidentId, matchingHandler.Id);

                response.StatusCode = 200;
                response.Response = new { threadId = thread.Id, message = "Incident received" };
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "HandleIncidentAsync: Error processing IncidentId: {IncidentId}", incidentId);
                response.StatusCode = 500;
                response.Response = "Failed to process Incident";
                return response;
            }
        }

        private async Task<Core.Models.Api.v1.Thread> CreateIncidentHandlerAgentThread(IIncidentDocument incidentDetails, IncidentHandlerDocument incidentHandler)
        {
            _logger.LogInternalInformation("CreateIncidentHandlerAgentThread: Invoked for IncidentId: {IncidentId}, HandlerId: {HandlerId}", incidentDetails.Id, incidentHandler.Id);
            try
            {
                var title = incidentDetails.Title ?? "New Incident";
                var alertMessage = $"🚨 **New Incident Reported**\n\n" +
                    $"**Title:** {title}\n\n" +
                    $"**Description:** {incidentDetails.Description}\n\n" +
                    $"**Incident ID:** {incidentDetails.Id}\n\n" +
                    $"**Severity:** {incidentDetails.Priority ?? "Unknown"}\n\n" +
                    $"**Source:** {incidentDetails.DocumentType}\n\n";

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
                    AllowedTools: incidentHandler.Tools
                );

                _logger.LogInternalInformation("CreateIncidentHandlerAgentThread: Created thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", thread.Id, incidentDetails.Id);

                var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
                await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

                await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                    ThreadId: thread.Id,
                    AgentContextId: agentContext.Id,
                    MessageId: thread.StartMessage.Id,
                    Message: "Process the incident as per custom instructions provided",
                    UserId: "incident-system",
                    DisplayName: "Incident System",
                    Timestamp: DateTime.UtcNow
                ), defaultHandler: false);

                return thread;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "CreateIncidentHandlerAgentThread: Error for IncidentId: {IncidentId}, HandlerId: {HandlerId}", incidentDetails.Id, incidentHandler.Id);
                throw;
            }
        }

        private async Task<Core.Models.Api.v1.Thread> CreateIncidentMetaAgentThread(IncidentHandlingRequestModel request)
        {
            _logger.LogInternalInformation("CreateIncidentMetaAgentThread: Invoked for IncidentId: {IncidentId}", request.IncidentId);
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

                (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
                    title: $"Incident Report - {request.Title}",
                    message: incidentMessage,
                    agentTypeEnum: AgentTypeEnum.Meta,
                    source: ThreadSource.Incident,
                    incidentId: request.IncidentId ?? string.Empty
                );

                _logger.LogInternalInformation("CreateIncidentMetaAgentThread: Created thread with ThreadId: {ThreadId} for IncidentId: {IncidentId}", thread.Id, request.IncidentId);

                var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
                await _repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Core.Models.Api.v1.Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

                await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                    ThreadId: thread.Id,
                    AgentContextId: agentContext.Id,
                    MessageId: thread.StartMessage.Id,
                    Message: messageBuilder.ToString(),
                    UserId: "incident-system",
                    DisplayName: request.Source ?? "Incident System",
                    Timestamp: DateTime.UtcNow
                ), defaultHandler: true);

                return thread;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "CreateIncidentMetaAgentThread: Error for IncidentId: {IncidentId}", request.IncidentId);
                throw;
            }
        }
    }
}
