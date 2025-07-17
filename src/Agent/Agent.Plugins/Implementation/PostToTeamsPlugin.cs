// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Logging;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Connector;
using Agent.Data.Repositories;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Implementation
{
    public class PostToTeamsPlugin : IPostToTeamsPlugin
    {
        private readonly IBotFrameworkHttpAdapter _adapter;
        private readonly ILogger<PostToTeamsPlugin> _logger;
        private readonly IThreadTeamsMappingRepository _threadTeamsMappingRepository;

        private readonly string _appId;
        private readonly string _tenantId;
        private const int MaxRetries = 60;
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
            var (_, returnMessage) = await PostInitialMessageAsync(message);
            return returnMessage;
        }

        private async Task<(ThreadTeamsMapping?, string)> PostInitialMessageAsync(string message, string threadId = "")
        {
            // Get a valid Teams channel from the repository
            var defaultChannel = await _threadTeamsMappingRepository.GetFirstOrDefaultChannel();

            if (defaultChannel == null)
            {
                _logger.LogInternalError("No conversation references available in the repository. The bot hasn't registered any Teams channels yet.");
                return (null, "Error: No Teams channels available to post message. The bot needs to register at least one Teams channel first.");
            }

            // Extract the service URL and channel ID from the mapping
            var serviceUrl = defaultChannel.ServiceUrl;
            var channelId = defaultChannel.ChannelId;

            if (string.IsNullOrEmpty(serviceUrl) || string.IsNullOrEmpty(channelId))
            {
                _logger.LogInternalError($"Service URL or Channel Id in default conversation reference is empty. ServiceUrl: {serviceUrl}, Channel ID: {channelId}");
                return (null, "Error posting message to Teams.");
            }

            // Build conversation parameters for proactive thread creation.
            var conversationParameters = new ConversationParameters
            {
                IsGroup = true,
                ChannelData = new TeamsChannelData
                {
                    Channel = new ChannelInfo { Id = channelId },
                },
                TopicName = "Azure SRE Agent - Proactive Thread", // This is not working as expected to set title, see: https://github.com/microsoft/botbuilder-dotnet/issues/5041
                // This initial activity will appear as the first message in the new thread.
                Activity = MessageFactory.Text(message),
            };
            ThreadTeamsMapping? mapping = null;
            try
            {
                if (_adapter is not CloudAdapter cloudAdapter)
                {
                    throw new InvalidOperationException("_adapter must be of type CloudAdapter.");
                }

                await cloudAdapter.CreateConversationAsync(
                    botAppId: _appId,
                    serviceUrl: serviceUrl,
                    channelId: Channels.Msteams,
                    audience: null,
                    conversationParameters: conversationParameters,
                    callback: async (turnContext, cancellationToken) =>
                    {
                        await Task.Yield();
                        mapping = new ThreadTeamsMapping(
                            $"teams_{threadId}",
                            threadId,
                            turnContext.Activity.Conversation.Id,
                            channelId,
                            serviceUrl,
                            DateTime.UtcNow,
                            DateTime.UtcNow,
                            turnContext.Activity.GetConversationReference());

                        _logger.LogInternalInformation("New Teams thread created and message posted.");
                    },
                    cancellationToken: CancellationToken.None);

                return (mapping, "Message posted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error posting message to Teams.");
                throw;
            }
        }

        public async Task<bool> PostTeamsMessage(string threadId, Activity message, string messageId = "")
        {
            var mapping = await _threadTeamsMappingRepository.GetMappingByThreadIdAsync(threadId);
            if (mapping == null)
            {
                (mapping, string returnMessage) = await PostInitialMessageAsync(message.Text, threadId);
                if (mapping == null)
                {
                    _logger.LogInternalError("Failed to create Teams thread mapping.");
                    return false;
                }
                await _threadTeamsMappingRepository.AddMappingAsync(mapping);
                if (returnMessage != "Message posted successfully.")
                {
                    _logger.LogInternalError("Failed to post message to Teams.");
                    return false;
                }
                if (!string.IsNullOrEmpty(messageId))
                {
                    await _threadTeamsMappingRepository.AddPostedMessagesAsync(threadId, new List<string> { messageId });
                }
                return true;
            }

            var serviceUrl = mapping.ServiceUrl;
            string channelId = mapping.ChannelId;

            if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(serviceUrl))
            {
                _logger.LogInternalError("Missing channelId or serviceUrl in stored conversation reference, probably this is from Teams private chat or chat group, please make sure this is from teams channel.");
                return false;
            }

            var adapter = _adapter as CloudAdapter;
            if (adapter == null)
            {
                _logger.LogInternalError("Adapter is not a CloudAdapter instance.");
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
        public async Task<bool> CreateTeamsThread(string threadId, string initialMessage, string messageId = "")
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var activity = MessageFactory.Text(initialMessage);
                    var result = await PostTeamsMessage(threadId, activity, messageId);
                    if (result)
                    {
                        _logger.LogInternalInformation("Successfully posted message to Teams");
                        return true; // Success, exit method
                    }

                    _logger.LogInternalWarning("Attempt {Attempt} failed with result: {Result}", attempt, result);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Attempt {Attempt} failed with exception", attempt);
                }

                // Only delay if we're going to retry again
                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelayMs);
                }
                else
                {
                    _logger.LogInternalError("Failed to post message to Teams after {MaxRetries} attempts", MaxRetries);
                }
            }
            return false; // Failure after all attempts
        }
    }
}

