// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.CVEAgent
{
    public class FeedbackRCAScanner
    {
        private readonly ILogger<FeedbackRCAScanner> _logger;
        private readonly IChatClient _chatClient;
        private readonly SinkService _sinkService;
        private readonly IThreadRepository _threadRepository;

        public FeedbackRCAScanner(
            ILogger<FeedbackRCAScanner> logger,
            IChatClient chatClient,
            SinkService sinkService,
            IThreadRepository threadRepository)
        {
            _logger = logger;
            _chatClient = chatClient;
            _sinkService = sinkService;
            _threadRepository = threadRepository;
        }

        public async Task Scan(CancellationToken cancellationToken)
        {
            var feedbackRCAAgentThreadContexts = (await _threadRepository.GetThreadContextsAsync())
                ?.Where(x => x.AgentTypeEnum == AgentTypeEnum.FeedbackRCA && x.IsThreadActive)
                ?.ToList();

            if (feedbackRCAAgentThreadContexts != null && feedbackRCAAgentThreadContexts.Count > 0)
            {
                _logger.LogInformation("Feedback RCA thread context already exists. Skipping scan.");
                return;
            }

           var messageFeedback = await _threadRepository.GetMessageFeedbackNeedingRCAAsync();

            if (messageFeedback != null)
            {
                var feedbackRCAAgent = new FeedbackRCAAgent.FeedbackRCAAgent(
                    _chatClient,
                    _sinkService,
                    _threadRepository,
                    messageFeedback: messageFeedback);
                var response = await feedbackRCAAgent.GetStartingMessagesAsync();
                var rca = response.Where(response => response.Role == ChatRole.Assistant)
                    .Select(response => response.Text)
                    .LastOrDefault();

                if (!string.IsNullOrEmpty(rca))
                {
                    messageFeedback = messageFeedback.UpdateRootCause(rca);
                    await _threadRepository.AddOrUpdateMessageFeedbackAsync(messageFeedback.ThreadId, messageFeedback);
                }
            }
        }
    }
}

