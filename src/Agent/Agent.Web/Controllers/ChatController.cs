using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Web.Services;
using Markdig;
using Microsoft.AspNetCore.Mvc;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly IThreadRepository _threadRepository;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly MarkdownPipeline _markdownPipeline;

        public ChatController(
            IChatService chatService,
            ILogger<ChatController> logger,
            IThreadRepository threadRepository,
            IAgentInboundCommunicationService agentInboundCommunicationService)
        {
            _logger = logger;
            _threadRepository = threadRepository;
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _markdownPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .DisableHtml()           // Disable HTML parsing
                .Build();
        }

        [HttpGet("GetHistory")]
        public async Task<IActionResult> GetHistory([FromQuery] string chatId)
        {
            try
            {
                if (string.IsNullOrEmpty(chatId))
                {
                    _logger.LogWarning("GetHistory called with null or empty chatId");
                    return BadRequest("ChatId is required");
                }

                // Get the agent type from the request header
                string? agentType = null;
                if (Request.Headers.TryGetValue("AgentType", out var agentTypeValues))
                {
                    agentType = agentTypeValues.FirstOrDefault();
                    _logger.LogInformation($"Getting history for thread: {chatId} with agent type: {agentType}");
                }
                else
                {
                    _logger.LogInformation($"Getting history for thread: {chatId} (no agent type specified)");
                }

                // Try to parse the chatId as a GUID for the new API
                if (Guid.TryParse(chatId, out Guid threadId))
                {
                    // Use the new API
                    var messages = await _threadRepository.GetMessagesAsync(threadId, null, null, null);

                    // Convert messages to the format expected by the current API
                    var history = messages.Select(m => new ChatMessage
                    {
                        Message = m.Text,
                        IsUser = m.Author.Role == Role.User,
                        Timestamp = m.TimeStamp
                    }).ToList();
                    foreach (var chat in history)
                    {
                        chat.Message = Markdown.ToHtml(chat.Message, _markdownPipeline);
                    }

                    _logger.LogInformation($"Found {history.Count} messages for thread ID: {threadId}");
                    return Ok(history);
                }
                else
                {
                    // Fall back to the old implementation
                    var historyStorage = HttpContext.RequestServices.GetRequiredService<IChatHistoryStorage>();
                    var history = await historyStorage.GetChatHistoryAsync(chatId, agentType);
                    foreach (var chat in history)
                    {
                        chat.Message = Markdown.ToHtml(chat.Message, _markdownPipeline);
                    }

                    _logger.LogInformation($"Found {history.Count} messages for agent type: {agentType ?? "Meta"}");
                    return Ok(history);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting history for thread {chatId}");
                return StatusCode(500, $"Error retrieving chat history: {ex.Message}");
            }
        }

        [HttpPost("SwitchAgent")]
        public async Task<IActionResult> SwitchAgent([FromBody] SwitchAgentRequest request)
        {
            try
            {
                // TODO: Implement the new API for switching orchestration for debugging
                // For now, just return OK
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error switching agent: {ex.Message}");
                return StatusCode(500, $"Error switching agent: {ex.Message}");
            }
        }

        [HttpGet("GetThreads")]
        public async Task<IActionResult> GetThreads()
        {
            try
            {
                // Use the new repository to get threads
                var threads = await _threadRepository.GetThreadsAsync(null, null, null);

                // Convert threads to the format expected by the current API
                var threadResponses = threads.Select(t => new ThreadInfo
                {
                    Id = t.Id.ToString(),
                    Title = t.Title,
                    CreatedTime = t.CreatedTimestamp
                }).ToList();

                return Ok(threadResponses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting threads: {ex.Message}");
                return StatusCode(500, $"Error getting threads: {ex.Message}");
            }
        }

        [HttpPost("CreateThread")]
        public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequest request)
        {
            if (request == null)
                return BadRequest("Request body was null.");

            try
            {
                // Create a new thread using the new API
                var startMessage = new CreateMessageRequest(
                    Text: "",
                    UserId: "web-client-user",
                    DisplayName: "Web Client User"
                );
                var threadId = Guid.NewGuid();
                var thread = new Thread(
                    Id: threadId,
                    Title: threadId.ToString(),
                    StartMessage: new Message(
                        Id: Guid.NewGuid(),
                        TimeStamp: DateTime.UtcNow,
                        Author: new Author(Role.User, startMessage.UserId, startMessage.DisplayName),
                        Text: startMessage.Text
                    ),
                    CreatedTimestamp: DateTime.UtcNow,
                    ModifiedTimestamp: DateTime.UtcNow
                );

                thread = await _threadRepository.CreateThreadAsync(thread);

                // Return the thread ID as chatId for compatibility
                return Ok(new CreateThreadResponse { ChatId = thread.Id.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating thread: {ex.Message}");
                return StatusCode(500, $"Error creating thread: {ex.Message}");
            }
        }

        [HttpPost("ProcessMessage")]
        public async Task<IActionResult> ProcessMessage([FromBody] ProcessMessageRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Message))
                {
                    return BadRequest("Message is required");
                }

                // Try to parse the chatId as a GUID for the new API
                if (Guid.TryParse(request.ChatId, out Guid threadId))
                {
                    // Process message using the new API
                    var response = await _agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage(
                        ThreadId: threadId,
                        MessageId: Guid.NewGuid(),
                        Message: request.Message,
                        UserId: "user",
                        DisplayName: "User",
                        Timestamp: DateTime.UtcNow
                    ));

                    // Get the message that was just created
                    var message = await _threadRepository.GetMessageAsync(threadId, response.MessageId);

                    // Create response in the expected format
                    var chatResponse = new ChatResponse
                    {
                        MessageId = response.MessageId.ToString(),
                        Content = message?.Text ?? "No response",
                        ThreadId = threadId.ToString()
                    };
                    chatResponse.Content = Markdown.ToHtml(chatResponse.Content, _markdownPipeline);
                    return Ok(chatResponse);
                }
                else
                {
                    return BadRequest("Invalid chatId");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing message: {ex.Message}");
                return StatusCode(500, $"Error processing message: {ex.Message}");
            }
        }

        // Define models needed for the response
        public class ThreadInfo
        {
            public string Id { get; set; } = "";
            public string Title { get; set; } = "";
            public DateTime CreatedTime { get; set; }
        }

        public class ChatResponse
        {
            public string MessageId { get; set; } = "";
            public string Content { get; set; } = "";
            public string ThreadId { get; set; } = "";
        }

        public class SetThreadRequest
        {
            public string ChatId { get; set; } = "";
        }

        public class SwitchAgentRequest
        {
            public string Path { get; set; } = "";
            public string ChatId { get; set; } = "";
        }

        public class CreateThreadRequest
        {
            public string Path { get; set; } = string.Empty;
            public string ChatId { get; set; } = string.Empty;
        }
        public class CreateThreadResponse
        {
            public string ChatId { get; set; } = string.Empty;
        }

        public class ProcessMessageRequest
        {
            public string Message { get; set; } = "";
            public string ChatId { get; set; } = "";
        }
    }
}