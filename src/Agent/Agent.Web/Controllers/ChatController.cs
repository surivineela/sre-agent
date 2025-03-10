using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
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

                // Pass the agent type explicitly to the chat service
                var historyStorage = HttpContext.RequestServices.GetRequiredService<IChatHistoryStorage>();
                var history = await historyStorage.GetChatHistoryAsync(chatId, agentType);

                _logger.LogInformation($"Found {history.Count} messages for agent type: {agentType ?? "Meta"}");
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting history for thread {chatId}");
                return StatusCode(500, $"Error retrieving chat history: {ex.Message}");
            }
        }

        [HttpPost("SetThread")]
        public async Task<IActionResult> SetThread([FromBody] SetThreadRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.ChatId))
                {
                    return BadRequest("ChatID is required");
                }

                await _chatService.SetThreadAsync(request.ChatId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting thread: {ex.Message}");
                return StatusCode(500, $"Error setting thread: {ex.Message}");
            }
        }

        [HttpPost("SwitchAgent")]
        public async Task<IActionResult> SwitchAgent([FromBody] SwitchAgentRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Path))
                {
                    return BadRequest("Path is required");
                }

                await _chatService.SwitchAgent(request.Path, request.ChatId ?? "");
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
                var threads = await _chatService.GetThreadsAsync();
                return Ok(threads);
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

            // Attempt to create a new thread using the given path and chatId
            var chatId = await _chatService.StartThreadAsync(request.Path, request.ChatId);
            if (string.IsNullOrEmpty(chatId))
                return BadRequest("Thread creation failed or returned an empty chatId.");

            // Return a well-formed JSON response
            return Ok(new CreateThreadResponse { ChatId = chatId });
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

                var response = await _chatService.ProcessMessageAsync(request.Message, request.ChatId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing message: {ex.Message}");
                return StatusCode(500, $"Error processing message: {ex.Message}");
            }
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