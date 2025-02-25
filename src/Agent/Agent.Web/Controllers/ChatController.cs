using Agent.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

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
        public async Task<IActionResult> GetHistory([FromQuery] string threadId)
        {
            try
            {
                if (string.IsNullOrEmpty(threadId))
                {
                    _logger.LogWarning("GetHistory called with null or empty threadId");
                    return BadRequest("ThreadId is required");
                }

                _logger.LogInformation($"Getting history for thread: {threadId}");
                var history = await _chatService.GetChatHistoryAsync(threadId);
                _logger.LogInformation($"Found {history.Count} messages");
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting history for thread {threadId}");
                return StatusCode(500, $"Error retrieving chat history: {ex.Message}");
            }
        }

        [HttpPost("SetThread")]
        public async Task<IActionResult> SetThread([FromBody] SetThreadRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.ThreadId))
                {
                    return BadRequest("ThreadId is required");
                }

                await _chatService.SetThreadAsync(request.ThreadId);
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

                await _chatService.SwitchAgent(request.Path, request.ThreadId ?? "");
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

            // Attempt to create a new thread using the given path and threadId
            var threadId = await _chatService.CreateThreadAsync(request.Path, request.ThreadId);
            if (string.IsNullOrEmpty(threadId))
                return BadRequest("Thread creation failed or returned an empty threadId.");

            // Return a well-formed JSON response
            return Ok(new CreateThreadResponse { ThreadId = threadId });
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

                var response = await _chatService.ProcessMessageAsync(request.Message, request.ThreadId);
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
            public string ThreadId { get; set; } = "";
        }

        public class SwitchAgentRequest
        {
            public string Path { get; set; } = "";
            public string ThreadId { get; set; } = "";
        }

        public class CreateThreadRequest
        {
            public string Path { get; set; } = string.Empty;
            public string ThreadId { get; set; } = string.Empty;
        }
        public class CreateThreadResponse
        {
            public string ThreadId { get; set; } = string.Empty;
        }

        public class ProcessMessageRequest
        {
            public string Message { get; set; } = "";
            public string ThreadId { get; set; } = "";
        }
    }
}