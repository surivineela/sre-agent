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

namespace Agent.Web.Controllers.v1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ChatHistoryController : ControllerBase
    {
        private readonly ILogger<ChatHistoryController> _logger;
        private readonly IThreadRepository _threadRepository;
        private readonly IReasoningLoopManager _reasoningLoopManager;

        public ChatHistoryController(
            ILogger<ChatHistoryController> logger,
            IThreadRepository threadRepository,
            IReasoningLoopManager reasoningLoopManager)
        {
            _logger = logger;
            _threadRepository = threadRepository;
            _reasoningLoopManager = reasoningLoopManager;
        }

        [HttpGet("agentFramework/{threadId}")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation: test api
        public async Task<IActionResult> GetAgentFrameworkChatHistory(Guid threadId)
#pragma warning restore CUSTOM004
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
    }
}

