// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;
using Agent.Core.Interfaces;
using Microsoft.Extensions.AI;
using Agent.Core.Helpers;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Agent.Data.DataModels;

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
        IThreadRepository repository,
        IChatClient chatClient,
        ILogger<ThreadsController> logger) : ControllerBase
    {
        // By default, returns threads ordered by timestamp in ascending order.
        // Pagination can be achieve by using `top` and `skip` query options. https://learn.microsoft.com/en-us/odata/client/pagination#client-driven-paging
        // Example: /api/v1/threads?top=10&skip=10
        // The order by can be overridden by using the `orderby` query option.
        // Example: If one wants 10 latest threads, they can call /api/v1/threads?top=10&orderby=createdTimestamp+desc
        // This pattern applies to all the endpoints that return a PagedResponse
        [HttpGet]
        public async Task<ActionResult<PagedResponse<Thread>>> GetThreads(ODataQueryOptions<ThreadDocument> queryOptions)
        {
            var threads = await repository.GetThreadsAsync(queryOptions);

            return Ok(new PagedResponse<Thread>(threads));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Thread>> GetThread(Guid id)
        {
            logger.LogInformation("Trying to get thread: {Id}", id);
            var thread = await repository.GetThreadAsync(id);

            if (thread == null)
            {
                logger.LogInformation("Thread not found: {Id}", id);
                return NotFound();
            }

            return Ok(thread);
        }

        [HttpPost]
        public async Task<ActionResult<Thread>> CreateThread(CreateThreadRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var threadId = Guid.NewGuid();
            var messageId = Guid.NewGuid();

            string temporaryTitle = request.StartMessage.Text.Length <= 50 ? request.StartMessage.Text : request.StartMessage.Text.Substring(0, 47) + "...";

            var message = new Message(
                    Id: messageId,
                    TimeStamp: DateTime.UtcNow,
                    Author: new Author(Role.User, request.StartMessage.UserId, request.StartMessage.DisplayName),
                    Text: request.StartMessage.Text
                );
            var thread = new Thread(
                Id: threadId,
                Title: temporaryTitle,
                StartMessage: message,
                LastMessage: message,
                CreatedTimestamp: DateTime.UtcNow,
                ModifiedTimestamp: DateTime.UtcNow,
                Source: request.Source ?? ThreadSource.Conversation
            );

            thread = await repository.CreateThreadAsync(thread);

            var threadContext = new ThreadContext(thread.Id, AgentTypeEnum.MetaAgent);
            threadContext.AddMessage(thread.StartMessage);
            await repository.AddThreadContextAsync(threadContext);

            // Start the background title generation task (fire and forget)
            _ = TitleHelper.GenerateTitleAndUpdateAsync(chatClient, repository, thread.Id, request.StartMessage.Text);

            var response = await agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage
            (
                ThreadId: thread.Id,
                MessageId: thread.StartMessage.Id,
                Message: request.StartMessage.Text,
                UserId: request.StartMessage.UserId,
                DisplayName: request.StartMessage.DisplayName,
                Timestamp: DateTime.UtcNow
            ));

            return CreatedAtAction(nameof(GetThread), new { id = thread.Id }, thread);
        }

        [HttpGet("{threadId}/messages")]
        public async Task<ActionResult<PagedResponse<Message>>> GetMessages(Guid threadId, ODataQueryOptions<MessageDocument> queryOptions)
        {
            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();

            var messages = await repository.GetMessagesAsync(threadId, queryOptions);

            return Ok(new PagedResponse<Message>(messages));
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

            // First check if thread exists
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();


            var response = await agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage
            (
                ThreadId: threadId,
                MessageId: Guid.NewGuid(),
                Message: request.Text,
                UserId: request.UserId,
                DisplayName: request.DisplayName,
                Timestamp: DateTime.UtcNow
            ));

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

        [HttpDelete("{threadId}")]
        public async Task<IActionResult> DeleteThread(Guid threadId)
        {
            var thread = await repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();

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

            messageBuilder.AppendLine($"🚨 **New {(!string.IsNullOrEmpty(request.Source) ? request.Source : String.Empty)} Incident Reported**");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"**Title:** {request.Title}");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"**Description:** {request.Description}\n");
            
            if (!string.IsNullOrEmpty(request.IncidentId))
            {
                messageBuilder.AppendLine($"**Incident ID:** {request.IncidentId}\n");
            }
            
            if (!string.IsNullOrEmpty(request.Severity))
            {
                messageBuilder.AppendLine($"**Severity:** {request.Severity}\n");
            }
            
            if (!string.IsNullOrEmpty(request.Source))
            {
                messageBuilder.AppendLine($"**Source:** {request.Source}\n");
            }

            if (request.AdditionalProperties?.Count > 0)
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("**Additional Details:**");
                foreach (var prop in request.AdditionalProperties)
                {
                    messageBuilder.AppendLine($"- {prop.Key}: {prop.Value}");
                }
            }

            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"**🔎 SRE Agent**\n");
            messageBuilder.AppendLine($"I'm starting to crawl relevant resources and investigate.");

            // commenting out for now since we are not supporting the teams scenario currently
            // this throws an error when it fails to sent message to teams
            //var thread = await agentInboundCommunicationService.CreateAlertThreadWithTeams(
            //    title: request.Title,
            //    message: messageBuilder.ToString(),
            //    agentTypeEnum: AgentTypeEnum.MetaAgent,
            //    source: ThreadSource.Incident
            //);

            var thread = await agentInboundCommunicationService.CreateAgentThread(
                title: request.Title,
                message: messageBuilder.ToString(),
                agentTypeEnum: AgentTypeEnum.MetaAgent,
                source: ThreadSource.Incident
            );

            await agentInboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                ThreadId: thread.Item1.Id,
                MessageId: thread.Item1.StartMessage.Id,
                Message: messageBuilder.ToString(),
                UserId: "incident-system", // TODO: distinguish between pager duty and icm or any other tool
                DisplayName: request.Source ?? "Incident System",
                Timestamp: DateTime.UtcNow
            ));

            return CreatedAtAction(nameof(GetThread), new { id = thread.Item1.Id }, thread);
        }
    }
}

