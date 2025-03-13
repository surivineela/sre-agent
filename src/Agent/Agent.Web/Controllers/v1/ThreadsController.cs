using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Runtime.Communication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;
using System.Collections.Concurrent;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ThreadsController(IThreadRepository repository) : ControllerBase
    {
        private readonly IThreadRepository _repository = repository;
        // In a real implementation, you would inject repositories or services here
        private readonly UserMessageService _userMessageService;

        public ThreadsController(UserMessageService userMessageService, ICommunicationService communicationService, IThreadRepository repository)
            : this(repository)
        {
            _userMessageService = userMessageService;
        }

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
                    Author: new Author(Role.User, "TestUser", "Test User"),
                    Text: request.StartMessage.Text
                ),
                CreatedTimestamp: DateTime.UtcNow,
                ModifiedTimestamp: DateTime.UtcNow
            );

            thread = await _repository.CreateThreadAsync(thread);

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

            var userId = "TestUserId";
            if (request.UserId != null)
            {
                userId = request.UserId;
            }

            var message = new Message(
                Id: Guid.NewGuid(),
                TimeStamp: DateTime.UtcNow,
                Author: new Author(Role.User, userId, request.UserName ?? "Test User"),
                Text: request.Text
            );

            message = await _repository.AddMessageAsync(threadId, message);

            // Forward to the user message service
            string response = await _userMessageService.ProcessUserMessageAsync(new ThreadMessage
            (
                ThreadId: threadId.ToString(),
                Message: request.Text,
                UserId: userId,
                Timestamp: DateTime.UtcNow
            ));
            message = await _repository.AddMessageAsync(threadId, new Message(
                Id: Guid.NewGuid(),
                TimeStamp: DateTime.UtcNow,
                Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
                Text: request.Text
            ));

            return CreatedAtAction(
                nameof(GetMessage),
                new { threadId, messageId = message.Id },
                message
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

        private static string GenerateTitle(string message)
        {
            // In a real application, we will use LLM to generate a title
            return message.Length <= 50 ? message : message.Substring(0, 47) + "...";
        }

        private void HandleAgentMessage(AgentMessage agentMessage)
        {
            // Convert threadId from string to Guid
            if (!Guid.TryParse(agentMessage.ThreadId, out var threadId))
            {
                return;
            }

            // Create a message from the agent response
            var message = new Message(
                Id: Guid.NewGuid(),
                TimeStamp: agentMessage.Timestamp,
                Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
                Text: agentMessage.Message
            );

            _repository.AddMessageAsync(threadId, message);
        }
    }
}