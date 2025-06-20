// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Logging;
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
        ThreadManagementService threadManagementService) : ControllerBase
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
            var threads = await repository.GetThreadsAsync(queryOptions, severity);

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

        [HttpPost("stream/create")]
        public async Task CreateThreadWithStreamedResponse(CreateThreadRequest request)
        {
            StreamWriter sw;

            if (!ModelState.IsValid)
            {
                Response.StatusCode = 400;
                Response.ContentType = "application/json";
                BadRequestObjectResult badRequest = new BadRequestObjectResult(ModelState);
                await using ((sw = new StreamWriter(Response.Body))
                    .ConfigureAwait(false))
                {
                    await sw.WriteLineAsync(JsonSerializer.Serialize(badRequest)).ConfigureAwait(false);
                    await sw.FlushAsync().ConfigureAwait(false);
                }
                return;
            }
            Response.StatusCode = 200;
            Response.ContentType = "text/html";
            var messageId = Guid.NewGuid();
            IAsyncEnumerable<ChatResponseUpdate> results = AsyncEnumerable.Empty<ChatResponseUpdate>();

            try
            {
                results = threadManagementService.CreateUserInitiatedThreadStream(request);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                var errorResponse = new { Message = "An unexpected error occurred. Please try again later." };
                logger.LogInternalError(ex, "Error processing user message stream");

                await using ((sw = new StreamWriter(Response.Body))
                    .ConfigureAwait(false))
                {
                    await sw.WriteLineAsync(JsonSerializer.Serialize(errorResponse)).ConfigureAwait(false);
                    await sw.FlushAsync().ConfigureAwait(false);
                }
                return;
            }

            StringBuilder completeMessage = new StringBuilder();
            await using ((sw = new StreamWriter(Response.Body))
                .ConfigureAwait(false))
            {
                await foreach (var result in results)
                {
                    completeMessage.Append(result.Text);
                    await sw.WriteLineAsync(JsonSerializer.Serialize(result)).ConfigureAwait(false);
                    await sw.FlushAsync().ConfigureAwait(false);
                }
                await sw.WriteLineAsync("[DONE]").ConfigureAwait(false);
                logger.LogInternalInformation($"Stream output complete response: {completeMessage.ToString()}");
            }
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

        [HttpPost("{threadId}/stream/messages")]
        public async Task CreateMessageWithStreamedResponse(Guid threadId, [FromBody] CreateMessageRequest request, [FromQuery] bool textOnly = false)
        {
            StreamWriter sw;

            if (!ModelState.IsValid) {
                Response.StatusCode = 400;
                Response.ContentType = "application/json";
                BadRequestObjectResult badRequest = new BadRequestObjectResult(ModelState);
                await using ((sw = new StreamWriter(Response.Body))
                    .ConfigureAwait(false))
                {
                        await sw.WriteLineAsync(JsonSerializer.Serialize(badRequest)).ConfigureAwait(false);
                        await sw.FlushAsync().ConfigureAwait(false);
                }
                return;
            }

            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null) {
                Response.StatusCode = 404;
                NotFoundObjectResult notFound = new NotFoundObjectResult($"Thread with id {threadId} not found");
                await using ((sw = new StreamWriter(Response.Body))
                    .ConfigureAwait(false))
                {
                        await sw.WriteLineAsync(JsonSerializer.Serialize(notFound)).ConfigureAwait(false);
                        await sw.FlushAsync().ConfigureAwait(false);
                }
                return;
            }

            var agentContexts = await repository.GetAgentContextsForThreadAsync(threadId);

            // pick out the meta agent context from all the agent contexts
            var agentContext = agentContexts.FirstOrDefault(c => c.AgentType == AgentTypeEnum.Meta && c.HandoffFromAgentContextId == null);
            if (agentContext == null)
            {
                Response.StatusCode = 404;
                NotFoundObjectResult notFound = new NotFoundObjectResult($"Meta Agent not found for thread {threadId}");
                await using ((sw = new StreamWriter(Response.Body))
                    .ConfigureAwait(false))
                {
                        await sw.WriteLineAsync(JsonSerializer.Serialize(notFound)).ConfigureAwait(false);
                        await sw.FlushAsync().ConfigureAwait(false);
                }
                return;
            }
            Response.StatusCode = 200;
            Response.ContentType = "text/html";
            var messageId = Guid.NewGuid();
            IAsyncEnumerable<ChatResponseUpdate> results = AsyncEnumerable.Empty<ChatResponseUpdate>();

            try {
                results = agentInboundCommunicationService.ProcessUserMessageStreamAsync(new ThreadMessage
                (
                    ThreadId: threadId,
                    AgentContextId: agentContext.Id,
                    MessageId: messageId,
                    Message: request.Text,
                    UserId: request.UserId,
                    DisplayName: request.DisplayName,
                    Timestamp: DateTime.UtcNow
                ), HttpContext.RequestAborted);
            } catch (Exception ex) {
                Response.StatusCode = 500;
                var errorResponse = new { Message = "An unexpected error occurred. Please try again later." };
                logger.LogInternalError(ex, "Error processing user message stream");

                await using ((sw = new StreamWriter(Response.Body))
                    .ConfigureAwait(false))
                {
                        await sw.WriteLineAsync(JsonSerializer.Serialize(errorResponse)).ConfigureAwait(false);
                        await sw.FlushAsync().ConfigureAwait(false);
                }
                return;
            }

            StringBuilder completeMessage = new StringBuilder();
            var userMessage = new ChatResponseUpdate
            {
                AuthorName = request.DisplayName,
                Role = ChatRole.User,
                CreatedAt = DateTime.UtcNow,
                Contents = [new TextContent(request.Text)],
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "messageId", messageId.ToString() },
                    { "threadId", threadId.ToString() },
                    { "userId", request.UserId },
                    { "actionName", nameof(CreateMessageWithStreamedResponse) }
                }
            };

            await using ((sw = new StreamWriter(Response.Body))
                .ConfigureAwait(false))
            {
                await sw.WriteLineAsync(textOnly ? request.Text : JsonSerializer.Serialize(userMessage)).ConfigureAwait(false);
                await sw.FlushAsync().ConfigureAwait(false);
                // TODO: Do we want to actually process the ChatResponseUpdate objects further or just return fragments of ChatResponseUpdate?
                await foreach(var result in results)
                {
                    completeMessage.Append(result.Text);
                    if (textOnly && result.FinishReason == ChatFinishReason.ToolCalls && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionCallContent))
                    {
                        var toolCallContent = (FunctionCallContent) result.Contents[0];
                        await sw.WriteLineAsync("Calling tool... " + toolCallContent.Name).ConfigureAwait(false);
                        await sw.WriteLineAsync("Tool call params: " + JsonSerializer.Serialize(toolCallContent.Arguments)).ConfigureAwait(false);
                        await sw.FlushAsync().ConfigureAwait(false);

                    }
                    if (textOnly && result.Role == ChatRole.Tool && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionResultContent)) {
                        await sw.WriteLineAsync("Tool call completed.").ConfigureAwait(false);
                        await sw.FlushAsync().ConfigureAwait(false);
                    }
                    await sw.WriteLineAsync(textOnly ? result.Text : JsonSerializer.Serialize(result)).ConfigureAwait(false);
                    await sw.FlushAsync().ConfigureAwait(false);
                }
                await sw.WriteLineAsync("[DONE]").ConfigureAwait(false);
            logger.LogInternalInformation($"Stream output complete response: {completeMessage.ToString()}");
            }
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

        [HttpGet("{threadId}/context")]
        public async Task<ActionResult<ThreadContext>> GetContext(Guid threadId)
        {
            var threadContext = await repository.GetThreadContextAsync(threadId);

            if (threadContext == null)
                return NotFound();

            logger.LogInternalInformation($"Get context for thread {threadId}: {JsonSerializer.Serialize(threadContext.OrchestrationState)}");

            if (threadContext.OrchestrationState != null &&
                !string.IsNullOrEmpty(threadContext.OrchestrationState.OrchestrationInstanceId) &&
                threadContext.OrchestrationState.ReasoningState != ReasoningState.OrchestrationCompleted &&
                threadContext.OrchestrationState.ReasoningState != ReasoningState.Error &&
                (DateTime.UtcNow - threadContext.OrchestrationState.TimeStamp > TimeSpan.FromSeconds(30)))
            {
                var orchestrationState = await durableTaskClient.GetInstanceAsync(threadContext.OrchestrationState.OrchestrationInstanceId);
                if (orchestrationState?.RuntimeStatus == OrchestrationRuntimeStatus.Failed || orchestrationState?.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    threadContext.OrchestrationState.ReasoningState = ReasoningState.Error;
                    // TODO(jianbosun): need to find way to get detailed DTS error here.
                    threadContext.OrchestrationState.StateMessage = $"Unexpected orchestration runtime status: {orchestrationState.RuntimeStatus} {(orchestrationState.FailureDetails != null ? orchestrationState.FailureDetails.ToString() : "")}";
                    threadContext.OrchestrationState.TimeStamp = DateTime.UtcNow;
                    await repository.UpdateThreadContextAsync(threadContext);
                }
            }
            if (threadContext.OrchestrationState != null)
            {
                // mask the StateMessage to avoid leak details
                threadContext.OrchestrationState.StateMessage = "";
            }

            return Ok(threadContext);
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
    }
}

