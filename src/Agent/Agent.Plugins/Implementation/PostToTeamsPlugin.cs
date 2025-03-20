using System;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Plugins.Definitions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Connector;
using Agent.Data.Repositories;

namespace Agent.Plugins.Implementation
{
    public class PostToTeamsPlugin : IPostToTeamsPlugin
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly ILogger<PostToTeamsPlugin> _logger;
        private readonly IThreadTeamsMappingRepository _threadTeamsMappingRepository;

        private readonly string _appId;
        private readonly string _tenantId;
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 10000;

        /// <summary>
        /// The default conversation reference should be captured from a known Teams channel
        /// (for example, during installation or from an initial proactive message).
        /// </summary>
        public PostToTeamsPlugin(
            IBotFrameworkHttpAdapter adapter,
            ILogger<PostToTeamsPlugin> logger,
            TeamsBotSettings teamsBot,
            IThreadTeamsMappingRepository threadTeamsMappingRepository)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _threadTeamsMappingRepository = threadTeamsMappingRepository ?? throw new ArgumentNullException(nameof(threadTeamsMappingRepository));
            _appId = teamsBot.AppId;
            _tenantId = teamsBot.TenantId;
        }

        public async Task<string> PostAsync(string message)
        {
            // Get a valid Teams channel from the repository
            var defaultChannel = await _threadTeamsMappingRepository.GetFirstOrDefaultChannel();

            if (defaultChannel == null)
            {
                _logger.LogError("No conversation references available in the repository. The bot hasn't registered any Teams channels yet.");
                return "Error: No Teams channels available to post message. The bot needs to register at least one Teams channel first.";
            }

            // Extract the service URL and channel ID from the mapping
            var serviceUrl = defaultChannel.ServiceUrl;
            var channelId = defaultChannel.ChannelId;

            if (string.IsNullOrEmpty(serviceUrl) || string.IsNullOrEmpty(channelId))
            {
                _logger.LogError($"Service URL or Channel Id in default conversation reference is empty. ServiceUrl: {serviceUrl}, Channel ID: {channelId}");
                return "Error posting message to Teams.";
            }

            // Build conversation parameters for proactive thread creation.
            var conversationParameters = new ConversationParameters
            {
                IsGroup = true,
                ChannelData = new TeamsChannelData
                {
                    Channel = new ChannelInfo { Id = channelId }
                },
                // This initial activity will appear as the first message in the new thread.
                Activity = MessageFactory.Text(message),
                TopicName = "New Thread",
            };

            try
            {
                var cloudAdapter = _adapter as CloudAdapter;
                await cloudAdapter.CreateConversationAsync(
                    botAppId: _appId,
                    serviceUrl: serviceUrl,
                    channelId: Channels.Msteams,
                    audience: null,
                    conversationParameters: conversationParameters,
                    callback: async (turnContext, cancellationToken) =>
                    {
                        _logger.LogInformation("New Teams thread created and message posted.");
                    },
                    cancellationToken: CancellationToken.None);

                return "Message posted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting message to Teams.");
                throw;
            }
        }

        public async Task<bool> PostTeamsMessage(string threadId, Activity message)
        {

            var mapping = await _threadTeamsMappingRepository.GetMappingByThreadIdAsync(threadId);
            if (mapping == null)
            {
                _logger.LogError($"Failed to post message to Teams post due to thread {threadId} don't have teams conversation exists");
                return false;
            }

            var serviceUrl = mapping.ServiceUrl;
            string channelId = mapping.ChannelId;

            if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(serviceUrl))
            {
                _logger.LogError("Missing channelId or serviceUrl in stored conversation reference, probably this is from Teams private chat or chat group, please make sure this is from teams channel.");
                return false;
            }

            var adapter = _adapter as CloudAdapter;
            if (adapter == null)
            {
                _logger.LogError("Adapter is not a CloudAdapter instance.");
                return false;
            }
            var newThreadId = Guid.NewGuid();

            // Send the message using ContinueConversationAsync
            await adapter.ContinueConversationAsync(
                _appId,
                mapping.Reference,
                async (turnContext, ct) =>
                {
                    await turnContext.SendActivityAsync(message, ct);
                },
                CancellationToken.None);

            return true;
        }

        /// <summary>
        /// Posts a message to Teams with retry logic
        /// </summary>
        public async Task<bool> PostToTeamsWithRetry(string message)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var result = await PostAsync(message);
                    if (result == "Message posted successfully.")
                    {
                        _logger.LogInformation("Successfully posted message to Teams");
                        return true; // Success, exit method
                    }

                    _logger.LogWarning("Attempt {Attempt} failed with result: {Result}", attempt, result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Attempt {Attempt} failed with exception", attempt);
                }

                // Only delay if we're going to retry again
                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelayMs);
                }
                else
                {
                    _logger.LogError("Failed to post message to Teams after {MaxRetries} attempts", MaxRetries);
                }
            }
            return false; // Failure after all attempts
        }
    }
}
