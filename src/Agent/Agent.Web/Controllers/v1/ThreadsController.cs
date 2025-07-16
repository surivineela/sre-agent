// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Agent.Framework;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.AI;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;
using Agent.Runtime.MetaAgent.Interfaces;
using Microsoft.DurableTask.Client;
using Agent.Web.Models.WelcomeMessage;
using Agent.Runtime.Services;
using Agent.Plugins.Interface;
using Agent.Runtime.Reasoning;
using Agent.Runtime.ThreadEvaluator;
using Agent.Plugins.Services.Interfaces;

namespace Agent.Web.Controllers.v1
{
    public record IncidentCallbackRequest(
        [Required] string Title,
        [Required] string Description,
        string? IncidentId,
        string? Severity,
        string? Source,  // e.g. "PagerDuty", "ICM"
        Dictionary<string, string>? AdditionalProperties
    );

    [ApiController]
    [Route("api/v1/[controller]")]
    public class ThreadsController(
        IAgentInboundCommunicationService agentInboundCommunicationService,
        IAgentsFactory agentsFactory,
        IThreadRepository repository,
        IChatClient chatClient,
        DurableTaskClient durableTaskClient,
        ILogger<ThreadsController> logger,
        IGraphService graphService,
        IConnectedIntegrationsPlugin connectedIntegrationsPlugin,
        IGithubIssuePlugin githubIssuePlugin,
        IReasoningLoopManager reasoningLoopManager,
        ThreadManagementService threadManagementService,
        ThreadEvaluator threadEvaluator,
        ActionSettings actionSettings,
        IAgentRuntimeModifier<AgentContext> agentRuntimeModifier) : ControllerBase
    {
        // By default, returns threads ordered by timestamp in ascending order.
        // Pagination can be achieve by using `top` and `skip` query options. https://learn.microsoft.com/en-us/odata/client/pagination#client-driven-paging
        // Example: /api/v1/threads?top=10&skip=10
        // The order by can be overridden by using the `orderby` query option.
        // Example: If one wants 10 latest threads, they can call /api/v1/threads?top=10&orderby=createdTimestamp+desc
        // This pattern applies to all the endpoints that return a PagedResponse
        // Threads can be filtered by severity using the `severity` query option.
        // Example: /api/v1/threads?severity=Critical
        [HttpGet]
        public async Task<ActionResult<PagedResponse<Thread>>> GetThreads(ODataQueryOptions<ThreadDocument> queryOptions,
        [FromQuery] ActionSeverity? severity = null)
        {
            var threads = await repository.GetThreadsAsync(queryOptions, severity, ThreadType.Prod);

            return Ok(new PagedResponse<Thread>(threads));
        }

        [HttpGet("testThreads")]
        public async Task<ActionResult<PagedResponse<Thread>>> GetTestThreads(ODataQueryOptions<ThreadDocument> queryOptions,
        [FromQuery] ActionSeverity? severity = null)
        {
            var threads = await repository.GetThreadsAsync(queryOptions, severity, ThreadType.Test);

            return Ok(new PagedResponse<Thread>(threads));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Thread>> GetThread(Guid id)
        {
            logger.LogInternalInformation("Trying to get thread: {Id}", id);
            var thread = await repository.GetThreadAsync(id);

            if (thread == null)
            {
                logger.LogInternalInformation("Thread not found: {Id}", id);
                return NotFound();
            }

            return Ok(thread);
        }

        [HttpPost]
        public async Task<ActionResult<Thread>> CreateThread(CreateThreadRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var thread = await threadManagementService.CreateUserInitiatedThread(request);
            return CreatedAtAction(nameof(GetThread), new { id = thread.Id }, thread);
        }

        [HttpGet("{threadId}/messages")]
        public async Task<ActionResult<PagedResponseWithState<Message, ContextStateEnum?>>> GetMessages(Guid threadId, ODataQueryOptions<MessageDocument> queryOptions)
        {
            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);
            var contexts = await repository.GetAgentContextsForThreadAsync(threadId);
            var ctx = contexts.FirstOrDefault();

            if (thread == null)
            {
                return NotFound();
            }

            var messages = await repository.GetMessagesAsync(threadId, queryOptions);

            return Ok(new PagedResponseWithState<Message, ContextStateEnum?>(Value: messages, State: ctx?.ContextState));
        }

        [HttpGet("{threadId}/messages/{messageId}")]
        public async Task<ActionResult<Message>> GetMessage(Guid threadId, Guid messageId)
        {
            var message = await repository.GetMessageAsync(threadId, messageId);

            if (message == null)
                return NotFound();

            return Ok(message);
        }

        [HttpPost("{threadId}/messages")]
        public async Task<ActionResult<Message>> CreateMessage(Guid threadId, CreateMessageRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await threadManagementService.CreateMessage(threadId, request);

            if (response == null)
            {
                return NotFound();
            }

            if (response.Busy)
            {
                logger.LogInternalInformation($"Thread {threadId} is busy processing a request, but user tried to send a message: {request.Text}");
                // todo: Do not block the user from sending messages for now. In case the reasoning loop's state stucks in Processing(e.g. because the agent restarts) and blocks the user indefinitely.
                // return UnprocessableEntity(new { Message = "The agent is currently busy processing your request. Please try again later." });
            }

            return CreatedAtAction(
                nameof(GetMessage),
                new { threadId, messageId = response.MessageId },
                new Message(
                    Id: response.MessageId,
                    TimeStamp: DateTime.UtcNow,
                    Author: new Author(Role.User, request.UserId, request.DisplayName),
                    Text: request.Text)
            );
        }

        [HttpPost("{threadId}/cancel")]
        public async Task<ActionResult<string>> CancelThreadExecution(Guid threadId)
        {
            logger.LogInternalInformation($"Canceling thread execution for thread {threadId}");
            var thread = await repository.GetThreadAsync(threadId);

            if (thread is null)
            {
                return NotFound();
            }

            var agentContexts = await repository.GetAgentContextsForThreadAsync(threadId);

            if (agentContexts is null || !agentContexts.Any())
            {
                return NotFound("No agent context found for the thread with id: " + threadId);
            }

            var agentContext = agentContexts.First();
            reasoningLoopManager.CancelCurrentOperation(agentContext);

            return AcceptedAtAction(nameof(CancelThreadExecution), new { threadId }, "Cancellation in progress");
        }

        [HttpGet("{threadId}/feedbacks/{messageFeedbackId}")]
        public async Task<ActionResult<MessageFeedback>> GetFeedback(Guid threadId, Guid messageFeedbackId)
        {
            var messageFeedback = await repository.GetMessageFeedbackAsync(threadId, messageFeedbackId);

            if (messageFeedback == null)
                return NotFound();

            return Ok(messageFeedback);
        }

        [HttpPost("{threadId}/feedbacks")]
        public async Task<ActionResult<MessageFeedback>> CreateFeedback(Guid threadId, FeedbackRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();

            var messageFeedbackId = Guid.NewGuid();

            logger.LogInternalInformation($"Creating feedback for thread {threadId} with messageFeedbackId {messageFeedbackId}. Request: {JsonSerializer.Serialize(request)}");

            var messageFeedback = await agentInboundCommunicationService.ProcessFeedbackAsync(new ThreadMessageFeedback
            (
                ThreadId: threadId,
                MessageFeedbackId: messageFeedbackId,
                IsPositive: request.IsPositive,
                FeedbackText: request.FeedbackText
            ));

            return CreatedAtAction(
                nameof(GetFeedback),
                new { threadId, messageFeedbackId },
                messageFeedback
            );
        }

        [HttpGet("{threadId}/actions")]
        public async Task<ActionResult<PagedResponse<Action>>> GetActions(Guid threadId, ODataQueryOptions<ActionDocument> queryOptions)
        {
            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();

            var actions = await repository.GetActionsAsync(threadId, queryOptions);

            return Ok(new PagedResponse<Action>(actions));
        }

        [HttpGet("{threadId}/welcomeMessage")]
        public async Task<ActionResult<WelcomeMessage>> GetWelcomeMessage(Guid threadId)
        {
            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);

            if (thread is null)
            {
                return NotFound();
            }

            if (thread.Source != ThreadSource.WelcomeMessage)
            {
                return BadRequest("Thread is not a welcome message thread.");
            }

            var crawlStatus = await graphService.GetGraphProgressAsync();
            var knowledgeGraphStatus = new KnowledgeGraphStatus(
                Status: crawlStatus.IsCrawling ? KnowledgeGraphStatusEnum.InProgress : KnowledgeGraphStatusEnum.Completed,
                CrawlProgress: new OverallCrawlProgress(
                    Crawled: (uint)crawlStatus.CrawledCount,
                    TotalResources: (uint)crawlStatus.TotalVisibleResources,
                    FinishedInitialCrawl: crawlStatus.HasCompletedInitialGraphCrawl
                ),
                CrawlProgressByResourceType: crawlStatus.ProgressByResourceType.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new CrawlProgressByResourceType(
                        Crawled: (uint)kvp.Value.CrawledCount,
                        TotalResources: (uint)kvp.Value.TotalResources
                    )
                )
            );

            var integrations = connectedIntegrationsPlugin.GetAllActiveIntegrations();

            var appGroupsWithRepo = await graphService.GetAppGroupsWithRepo();

            var githubAccessToken = await repository.GetGitHubAccessTokenAsync();

            // TODO: Fix for AzDo.
            var githubAccessTokenConfigured = githubAccessToken != null && !string.IsNullOrEmpty(githubAccessToken.AccessToken) && (githubAccessToken.ExpiresOn is null || githubAccessToken.ExpiresOn > DateTime.UtcNow);

            var loginUrl = githubIssuePlugin.GenerateLoginLink();

            var logicalApplications = appGroupsWithRepo.Select(appGroup =>
            {
                var sourceCodeLinkageStatus = (githubAccessTokenConfigured, appGroup.RepoUrl) switch
                {
                    (false, string repo) when !string.IsNullOrEmpty(repo) => new SourceCodeLinkageStatus(SourceCodeLinkageStatusEnum.RequiresAuth, repo, appGroup.LinkedTimestamp, loginUrl),
                    (true, string repo) when !string.IsNullOrEmpty(repo) => new SourceCodeLinkageStatus(SourceCodeLinkageStatusEnum.Linked, repo, appGroup.LinkedTimestamp, null),
                    _ => new SourceCodeLinkageStatus(SourceCodeLinkageStatusEnum.NotLinked, null, null, null)
                };

                return new LogicalApplication(appGroup.Name, appGroup.ResourceId, sourceCodeLinkageStatus, appGroup.Type, new AdditionalInfo(Namespace: appGroup.Namespace));
            }).ToList();

            var welcomeMessage = new WelcomeMessage(
                KnowledgeGraphStatus: knowledgeGraphStatus,
                Integrations: integrations,
                LogicalApplications: logicalApplications
            );

            return Ok(welcomeMessage);
        }

        [HttpDelete("{threadId}")]
        public async Task<IActionResult> DeleteThread(Guid threadId)
        {
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();

            var agentContexts = await repository.GetAgentContextsForThreadAsync(threadId);
            foreach (var agentContext in agentContexts)
            {
                await repository.DeleteAgentContextAsync(agentContextId: agentContext.Id, threadId: threadId);

                var agentChatHistory = await repository.GetAgentChatHistoryAsync(agentContextId: agentContext.Id);
                if (agentChatHistory != null)
                {
                    foreach (var reasoningMessageId in agentChatHistory.ReasoningMessageIds)
                    {
                        await repository.DeleteReasoningMessageAsync(reasoningMessageId: reasoningMessageId, agentContextId: agentContext.Id);
                    }

                    await repository.DeleteAgentChatHistoryAsync(agentContextId: agentContext.Id);
                }
            }

            await repository.DeleteThreadAsync(threadId);

            return NoContent();
        }

        [HttpPost("incidents")]
        public async Task<ActionResult<Thread>> CreateIncidentThread([FromBody] IncidentCallbackRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var messageBuilder = new StringBuilder();

            // (stacy zeng) todo:
            // check if request has existing thread in case we send same request twice or incident reactivations

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

            // commenting out for now since we are not supporting the teams scenario currently
            // this throws an error when it fails to sent message to teams
            //var thread = await agentInboundCommunicationService.CreateAlertThreadWithTeams(
            //    title: request.Title,
            //    message: messageBuilder.ToString(),
            //    agentTypeEnum: AgentTypeEnum.MetaAgent,
            //    source: ThreadSource.Incident
            //);

            (var thread, var agentContext) = await agentInboundCommunicationService.CreateAgentThread(
                title: $"Incident Report - {request.Title}",
                message: incidentMessage,
                agentTypeEnum: AgentTypeEnum.Meta,
                source: ThreadSource.Incident,
                incidentId: request.IncidentId ?? string.Empty
            );

            var agentMessage = $"**Acknowledging the incident**. I'm starting to investigate and see how I can help.";
            await repository.AddMessageAsync(thread.Id, new Message(Guid.NewGuid(), DateTime.UtcNow, new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"), agentMessage));

            await agentInboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                ThreadId: thread.Id,
                AgentContextId: agentContext.Id,
                MessageId: thread.StartMessage.Id,
                Message: messageBuilder.ToString(),
                UserId: "incident-system", // TODO: distinguish between pager duty and icm or any other tool
                DisplayName: request.Source ?? "Incident System",
                Timestamp: DateTime.UtcNow
            ));

            return CreatedAtAction(nameof(GetThread), new { id = thread.Id }, thread);
        }

        /// <summary>
        /// Marks a thread as read and updates the LastReadTime to the current timestamp
        /// </summary>
        /// <param name="threadId">Thread ID to mark as read</param>
        /// <returns>Updated Thread object</returns>
        [HttpPost("{threadId}/markRead")]
        public async Task<ActionResult<Thread>> MarkThreadAsRead(Guid threadId)
        {
            logger.LogInternalInformation("Marking thread as read: {Id}", threadId);

            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null)
            {
                logger.LogInternalInformation("Thread not found: {Id}", threadId);
                return NotFound();
            }

            // Update thread in repository
            var updatedThread = await repository.UpdateThreadReadMarkAsync(threadId, DateTime.UtcNow);

            return Ok(updatedThread);
        }

        /// <summary>
        /// Gets the count of unread messages in a thread (messages created after LastReadTime)
        /// </summary>
        /// <param name="threadId">Thread ID to check for unread messages</param>
        /// <returns>Count of unread messages</returns>
        [HttpPost("{threadId}/getUnreadCount")]
        public async Task<ActionResult<int>> GetUnreadMessageCount(Guid threadId)
        {
            logger.LogInternalInformation("Getting unread message count for thread: {Id}", threadId);

            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null)
            {
                logger.LogInternalInformation("Thread not found: {Id}", threadId);
                return NotFound();
            }

            // Use the optimized repository method to get unread message count directly from database
            var count = await repository.GetUnreadMessagesCountAsync(threadId, thread.LastReadTime);

            // Return the count
            return Ok(count);
        }

        /// <summary>
        /// Updates the agent mode configuration for a thread
        /// </summary>
        /// <param name="threadId">Thread ID to update agent mode for</param>
        /// <param name="request">Request containing the new agent mode</param>
        /// <returns>Updated Thread object</returns>
        [HttpPost("{threadId}/agentMode")]
        public async Task<ActionResult<Thread>> UpdateThreadAgentMode(Guid threadId, [FromBody] UpdateAgentModeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            logger.LogInternalInformation("Updating agent mode for thread: {Id} to {AgentMode}", threadId, request.AgentMode);

            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null)
            {
                logger.LogInternalInformation("Thread not found: {Id}", threadId);
                return NotFound();
            }

            // Get the effective current mode (thread-specific or global default)
            var defaultMode = actionSettings.Mode?.ToString() ?? AgentModes.Review;
            var currentEffectiveMode = string.IsNullOrEmpty(thread.AgentMode) ? defaultMode : thread.AgentMode;

            // If request.AgentMode is null/empty, it means reset to default
            var requestedMode = string.IsNullOrEmpty(request.AgentMode) ? defaultMode : request.AgentMode;

            // Check if the requested mode is the same as the current effective mode
            if (string.Equals(requestedMode, currentEffectiveMode, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInternalInformation("Thread {ThreadId} agent mode is already effectively set to requested mode {AgentMode}", threadId, requestedMode);
                return Ok(thread); // No change needed
            }

            // Get the global default agent mode from configuration
            var globalDefaultMode = actionSettings.Mode?.ToString() ?? AgentModes.Review;

            // Validate the requested agent mode against the global restrictions
            var validationResult = ValidateAgentModeChange(globalDefaultMode, request.AgentMode);
            if (!validationResult.IsValid)
            {
                logger.LogInternalWarning("Invalid agent mode change attempted: {RequestedMode} with global default {GlobalMode}. Reason: {Reason}",
                    request.AgentMode, globalDefaultMode, validationResult.ErrorMessage);
                return BadRequest(new { error = validationResult.ErrorMessage });
            }

            // Update thread agent mode in repository
            var updatedThread = await repository.UpdateThreadAgentModeAsync(threadId, request.AgentMode);

            if (updatedThread == null)
            {
                logger.LogInternalError("Failed to update agent mode for thread: {Id}", threadId);
                return StatusCode(500, "Failed to update thread agent mode");
            }

            // Notify the agent runtime about the mode change to affect running agents
            // Get the agent context for this thread to pass to the runtime modifier
            var agentContexts = await repository.GetAgentContextsForThreadAsync(threadId);
            var agentContext = agentContexts?.FirstOrDefault();

            if (agentContext != null)
            {
                await agentRuntimeModifier.SetAgentMode(agentContext, request.AgentMode);
                logger.LogInternalInformation("Notified agent runtime modifier about mode change for thread: {Id} to {AgentMode}", threadId, request.AgentMode);
            }
            else
            {
                logger.LogInternalInformation("No agent context found for thread {ThreadId}, skipping runtime modifier notification", threadId);
            }

            return Ok(updatedThread);
        }

        /// <summary>
        /// Validates whether the requested agent mode change is allowed based on the global default configuration
        /// </summary>
        /// <param name="globalDefaultMode">Global default agent mode from configuration</param>
        /// <param name="requestedMode">Requested agent mode for the thread</param>
        /// <returns>Validation result with IsValid flag and error message if invalid</returns>
        private (bool IsValid, string? ErrorMessage) ValidateAgentModeChange(string globalDefaultMode, string requestedMode)
        {
            if (AgentModes.IsValidModeChange(globalDefaultMode, requestedMode))
            {
                return (true, null);
            }

            var errorMessage = AgentModes.GetValidationErrorMessage(globalDefaultMode);
            return (false, errorMessage);
        }

        /// <summary>
        /// Gets the available agent modes based on the global default configuration
        /// </summary>
        /// <returns>Array of available agent mode strings</returns>
        [HttpGet("agentModes")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string[]))]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<string[]> GetAvailableAgentModes()
        {
            try
            {
                var globalDefaultMode = actionSettings.Mode?.ToString() ?? AgentModes.Review;
                var availableModes = AgentModes.GetAvailableModesFor(globalDefaultMode);

                return Ok(availableModes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving available agent modes: {ex.Message}");
            }
        }
    }
}

