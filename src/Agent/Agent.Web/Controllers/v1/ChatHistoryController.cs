// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Runtime.Reasoning;
using Agent.Runtime.TeamsChatServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DurableTask.Client;

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ChatHistoryController : ControllerBase
    {
        private readonly ILogger<ChatHistoryController> _logger;
        private readonly DurableTaskClient _durableTaskClient;
        private readonly IThreadRepository _threadRepository;
        private readonly IReasoningLoopManager _reasoningLoopManager;

        public ChatHistoryController(
            ILogger<ChatHistoryController> logger,
            IThreadRepository threadRepository,
            IReasoningLoopManager reasoningLoopManager,
            DurableTaskClient durableTaskClient)
        {
            _logger = logger;
            _threadRepository = threadRepository;
            _reasoningLoopManager = reasoningLoopManager;
            _durableTaskClient = durableTaskClient;
        }

        [HttpGet("agentFramework/{threadId}")]
        public async Task<IActionResult> GetAgentFrameworkChatHistory(Guid threadId)
        {
            var contexts = await _threadRepository.GetAgentContextsForThreadAsync(threadId);
            if (contexts.Count() == 0)
            {
                return NotFound();
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var history = await _reasoningLoopManager.ExportChatHistory(contexts.First(), cts.Token);
            return Ok(history);
        }

        /// <summary>
        /// Gets chat history for all running orchestrations
        /// </summary>
        /// <returns>Dictionary of orchestration IDs to their chat histories</returns>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllOrchestrationsChatHistory()
        {
            _logger.LogInternalInformation("GET chat history requested for all running orchestrations");

            var result = new Dictionary<string, object>();

            var runningOrchestrations = await _durableTaskClient.GetAllInstancesAsync(new OrchestrationQuery
            {
                Statuses = new[] { OrchestrationRuntimeStatus.Running }
            }).ToListAsync();

            _logger.LogInternalInformation("Found {Count} running orchestrations", runningOrchestrations.Count);

            foreach (var orchestration in runningOrchestrations)
            {
                try
                {
                    var orchestrationInstance = await _durableTaskClient.GetInstanceAsync(
                        orchestration.InstanceId,
                        getInputsAndOutputs: true);

                    if (orchestrationInstance != null && !string.IsNullOrEmpty(orchestrationInstance.SerializedCustomStatus))
                    {
                        // Try to parse the custom status as JSON
                        try
                        {
                            var chatHistoryJson = orchestrationInstance.SerializedCustomStatus;
                            var chatHistory = JsonSerializer.Deserialize<JsonElement>(chatHistoryJson);
                            result.Add(orchestration.InstanceId, chatHistory);
                        }
                        catch (JsonException)
                        {
                            // If it's not valid JSON, include it as a string
                            result.Add(orchestration.InstanceId, orchestrationInstance.SerializedCustomStatus);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error retrieving chat history for orchestration ID: {Id}", orchestration.InstanceId);
                    result.Add(orchestration.InstanceId, $"Error: {ex.Message}");
                }
            }

            return Ok(result);
        }

        /// <summary>
        /// Gets the chat history for a specific orchestration
        /// </summary>
        /// <param name="orchestrationId">Orchestration ID</param>
        /// <returns>Chat history as JSON</returns>
        [HttpGet("orchestration/{orchestrationId}")]
        public async Task<IActionResult> GetOrchestrationChatHistory(string orchestrationId)
        {
            _logger.LogInternalInformation("GET chat history requested for orchestration ID: {Id}", orchestrationId);

            // Get the orchestration instance with custom status
            var orchestrationWithStatus = await _durableTaskClient.GetInstanceAsync(
                orchestrationId,
                getInputsAndOutputs: true);

            if (orchestrationWithStatus == null)
            {
                return NotFound(new { error = $"Orchestration instance {orchestrationId} not found" });
            }

            if (string.IsNullOrEmpty(orchestrationWithStatus.SerializedCustomStatus))
            {
                return NotFound(new { error = $"No chat history found for orchestration ID: {orchestrationId}" });
            }

            try
            {
                // Parse the custom status directly - it should already be JSON
                var chatHistory = orchestrationWithStatus.SerializedCustomStatus;

                // Return as JSON with proper content type
                return Content(chatHistory, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error retrieving chat history for orchestration ID: {Id}", orchestrationId);
                return StatusCode(500, new { error = "Failed to retrieve chat history" });
            }
        }
    }
}

