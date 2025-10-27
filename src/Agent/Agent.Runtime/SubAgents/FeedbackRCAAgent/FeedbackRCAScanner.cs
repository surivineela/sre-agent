// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.FeedbackRCAAgent
{
    public class FeedbackRCAScanner
    {
        private readonly ILogger<FeedbackRCAScanner> _logger;
        private readonly IChatClientProvider _chatClientProvider;
        private readonly SinkService _sinkService;
        private readonly IThreadRepository _threadRepository;

        public FeedbackRCAScanner(
            ILogger<FeedbackRCAScanner> logger,
            IChatClientProvider chatClientProvider,
            SinkService sinkService,
            IThreadRepository threadRepository)
        {
            _logger = logger;
            _chatClientProvider = chatClientProvider;
            _sinkService = sinkService;
            _threadRepository = threadRepository;
        }

        public async Task Scan(CancellationToken cancellationToken)
        {
            var feedbackRCAAgentContexts = (await _threadRepository.GetThreadContextsAsync())
                ?.Where(x => x.AgentTypeEnum == AgentTypeEnum.FeedbackRCA && x.IsThreadActive)
                ?.ToList();

            if (feedbackRCAAgentContexts != null && feedbackRCAAgentContexts.Count > 0)
            {
                _logger.LogInternalInformation("Feedback RCA thread context already exists. Skipping scan.");
                return;
            }

            var messageFeedback = await _threadRepository.GetMessageFeedbackNeedingRCAAsync();

            if (messageFeedback != null)
            {
                var feedbackRCAAgent = new FeedbackRCAAgent(
                    _chatClientProvider,
                    _sinkService,
                    _threadRepository,
                    messageFeedback: messageFeedback);
                var response = await feedbackRCAAgent.GetStartingMessagesAsync();
                var rca = response.Where(response => response.Role == ChatRole.Assistant)
                    .Select(response => response.Text)
                    .LastOrDefault();

                _logger.LogInternalInformation($"Generated RCA for messageFeedback id {messageFeedback.Id} for thread {messageFeedback.ThreadId}: {rca}");

                if (!string.IsNullOrEmpty(rca))
                {
                    messageFeedback = messageFeedback.UpdateRootCause(rca);
                    await _threadRepository.AddOrUpdateMessageFeedbackAsync(messageFeedback.ThreadId, messageFeedback);
                }
            }
        }
    }
}
