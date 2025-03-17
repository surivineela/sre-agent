using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Runtime.Communication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;
using System.Collections.Concurrent;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ThreadsController(
        IAgentInboundCommunicationService agentInboundCommunicationService,
        IThreadRepository repository) : ControllerBase
    {
        private readonly IThreadRepository _repository = repository;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService = agentInboundCommunicationService;

        [HttpGet]
        public async Task<ActionResult<PagedResponse<Thread>>> GetThreads(ODataQueryOptions<Thread> queryOptions)
        {
            // Extract basic filtering from OData
            string? filter = null;
            int? skip = null;
            int? take = null;

            if (queryOptions.Skip != null)
                skip = queryOptions.Skip.Value;

            if (queryOptions.Top != null)
                take = queryOptions.Top.Value;

            // In a full implementation, you would parse queryOptions.Filter into a format 
            // that your repository can understand
            var threads = await _repository.GetThreadsAsync(filter, skip, take);

            // Apply OData filtering and pagination
            return Ok(new PagedResponse<Thread>(threads));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Thread>> GetThread(Guid id)
        {
            var thread = await _repository.GetThreadAsync(id);

            if (thread == null)
                return NotFound();

            return Ok(thread);
        }

        [HttpPost]
        public async Task<ActionResult<Thread>> CreateThread(CreateThreadRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var thread = new Thread(
                Id: Guid.NewGuid(),
                Title: GenerateTitle(request.StartMessage.Text),
                StartMessage: new Message(
                    Id: Guid.NewGuid(),
                    TimeStamp: DateTime.UtcNow,
                    Author: new Author(Role.User, request.StartMessage.UserId, request.StartMessage.DisplayName),
                    Text: request.StartMessage.Text
                ),
                CreatedTimestamp: DateTime.UtcNow,
                ModifiedTimestamp: DateTime.UtcNow
            );

            thread = await _repository.CreateThreadAsync(thread);

            var response = await _agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage
            (
                ThreadId: thread.Id,
                Message: request.StartMessage.Text,
                UserId: request.StartMessage.UserId,
                DisplayName: request.StartMessage.DisplayName,
                Timestamp: DateTime.UtcNow
            ));

            return CreatedAtAction(nameof(GetThread), new { id = thread.Id }, thread);
        }

        [HttpGet("{threadId}/messages")]
        public async Task<ActionResult<PagedResponse<Message>>> GetMessages(Guid threadId, ODataQueryOptions<Message> queryOptions)
        {
            // First check if thread exists
            var thread = await _repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();

            // Extract basic filtering from OData
            string? filter = null;
            int? skip = null;
            int? take = null;

            if (queryOptions.Skip != null)
                skip = queryOptions.Skip.Value;

            if (queryOptions.Top != null)
                take = queryOptions.Top.Value;

            var messages = await _repository.GetMessagesAsync(threadId, filter, skip, take);

            return Ok(new PagedResponse<Message>(messages));
        }

        [HttpGet("{threadId}/messages/{messageId}")]
        public async Task<ActionResult<Message>> GetMessage(Guid threadId, Guid messageId)
        {
            var message = await _repository.GetMessageAsync(threadId, messageId);

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
            var thread = await _repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();


            var response = await _agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage
            (
                ThreadId: threadId,
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

        [HttpGet("{threadId}/actions")]
        public async Task<ActionResult<PagedResponse<Action>>> GetActions(Guid threadId, int? skip = null, int? top = null)
        {
            // First check if thread exists
            var thread = await _repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();

            var actions = await _repository.GetActionsAsync(threadId, skip, top);

            return Ok(new PagedResponse<Action>(actions));
        }

        [HttpDelete("{threadId}")]
        public async Task<IActionResult> DeleteThread(Guid threadId)
        {
            var thread = await _repository.GetThreadAsync(threadId);

            if (thread == null)
                return NotFound();

            await _repository.DeleteThreadAsync(threadId);

            return NoContent();
        }

        private static string GenerateTitle(string message)
        {
            // In a real application, we will use LLM to generate a title
            return message.Length <= 50 ? message : message.Substring(0, 47) + "...";
        }
    }
}