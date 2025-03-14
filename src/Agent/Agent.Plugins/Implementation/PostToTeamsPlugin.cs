using System;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Services;
using Agent.Plugins.Definitions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Connector;

namespace Agent.Plugins.Implementation
{
    public class PostToTeamsPlugin : IPostToTeamsPlugin
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly ILogger<PostToTeamsPlugin> _logger;

        private readonly string _appId;
        private readonly string _tenantId;
        private const int MaxRetries = 20;
        private const int RetryDelayMs = 10000;

        /// <summary>
        /// The default conversation reference should be captured from a known Teams channel
        /// (for example, during installation or from an initial proactive message).
        /// </summary>
        public PostToTeamsPlugin(
            IBotFrameworkHttpAdapter adapter,
            ILogger<PostToTeamsPlugin> logger,
            TeamsBotSettings teamsBot)
        // ConversationReference defaultConversationReference)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _appId = teamsBot.AppId;
            _tenantId = teamsBot.TenantId;
        }

        public async Task<string> PostAsync(string message)
        {
            // Check if ConversationReferences dictionary is empty
            if (TeamsBot.ConversationReferences == null || !TeamsBot.ConversationReferences.Any())
            {
                _logger.LogError("No conversation references available. The bot hasn't received any messages yet.");
                return "Error: No Teams channels available to post message. The bot needs to receive at least one message first.";
            }

            var defaultConversation = TeamsBot.ConversationReferences.Values.FirstOrDefault();
            if (defaultConversation == null)
            {
                _logger.LogError("Failed to get a valid conversation reference from available references.");
                return "Error: Failed to find a valid Teams channel to post message.";
            }

            // Extract the service URL from the stored conversation reference.
            var serviceUrl = defaultConversation.ServiceUrl;

            // Create a continuation activity to extract Teams channel data.
            var continuationActivity = defaultConversation.GetContinuationActivity();
            var teamsChannelData = continuationActivity.GetChannelData<TeamsChannelData>();

            string channelId = defaultConversation.ChannelId;

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
                Activity = MessageFactory.Text(message)
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
