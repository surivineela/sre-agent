using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Xml;
using Agent.Core.Models.Streaming;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Activity = Microsoft.Bot.Schema.Activity;

namespace Agent.Core.Services;

public class TeamsBot : TeamsActivityHandler
{
    private readonly IChatService _chatService;
    private readonly ILogger<TeamsBot> _logger;
    private readonly Dictionary<string, string> _conversationThreadMapping;
    public static Dictionary<string, ConversationReference> ConversationReferences = new();
    private readonly IBotFrameworkHttpAdapter _teamsAdapter;

    string welcomeMessage = "## 👋 Hi, I'm your new Azure SRE Partner!\n\nI'm here to help monitor your applications and keep everything running smoothly.\n\nI've **already started scanning your applications** and will let you know shortly if I find anything that needs attention.\n\nThink of me as your reliable sidekick for all things related to system reliability and operations. Whether you need help with security updates, monitoring metrics, or troubleshooting issues, I've got your back!\n\n### ⚙️ **Autopilot Mode**:\n\nI'm designed to work proactively on your behalf! From time to time, I'll notify you about important updates and ask for your approval before taking action. I'll continuously monitor your systems in the background, so you can focus on what matters most.\n\n### **How to get started**:\n\nIf you have any specific questions or needs, simply mention what you'd like help with, and I'll jump right in. You can ask me to:\n\n- \"Monitor my application performance\"\n- \"Check on my app's metrics\"\n- \"Create a app migration plan\"\n- \"Help diagnose why my service is slow\"\n\nNo fancy commands needed - just chat with me like you would with a colleague, and I'll help you tackle whatever challenges come your way.\n\nLooking forward to working together and keeping your systems running at their best!";

    // Teams has strict limitation for activities per second: https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/rate-limit#per-bot-per-thread-limit
    // Constants for optimization
    private const int BATCH_SIZE = 16; // Process chunks in batches of 16
    // TODO: we need to design a better client side ratelimiter to satisfy teams throttling policy while keep sending the message in a timely manner
    // 7 per 1s
    // 8 per 2s
    // 60 per 30s
    // 1800 per 3600s
    private const int UPDATE_INTERVAL_MS = 300; // Send updates every 300ms, 
    public TeamsBot(IChatService chatService, ILogger<TeamsBot> logger, IBotFrameworkHttpAdapter teamsAdapter)
    {
        _chatService = chatService;
        _logger = logger;
        _conversationThreadMapping = new Dictionary<string, string>();
        _teamsAdapter = teamsAdapter;
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var teamsChannelId = turnContext.Activity.TeamsGetChannelId();
        var serviceUrl = turnContext.Activity.ServiceUrl;
        var conversationReference = turnContext.Activity.GetConversationReference();
        conversationReference.ServiceUrl = serviceUrl;
        conversationReference.ChannelId = teamsChannelId;
        _logger.LogInformation($"Teams Conversation: Received message from Teams channel {teamsChannelId}, service URL: {serviceUrl}");
        //lock (ConversationReferences)
        //  {
        ConversationReferences[conversationReference.Conversation.Id] = conversationReference;
        //}

        string messageText = turnContext.Activity.RemoveRecipientMention()?.Trim();
        if (string.IsNullOrEmpty(messageText))
        {
            _logger.LogInformation("Received empty message from user");
            return;
        }

        string conversationId = turnContext.Activity.Conversation.Id;

        // Get or create thread ID for this conversation using a more efficient method
        string chatId = GetOrCreateChatId(conversationId);

        _logger.LogInformation($"[Teams Conversation: {conversationId}][Thread: {chatId}]\nSending message to agent: {messageText}");

        if (messageText.ToLowerInvariant() == "hello")
        {
            // If the user says "hello", respond with a greeting quickly without using AI backend
            await turnContext.SendActivityAsync(MessageFactory.Text(welcomeMessage), cancellationToken);
            return;
        }
        if (turnContext.Activity.Conversation.IsGroup.GetValueOrDefault())
        {
            var channelDataObj = turnContext.Activity.ChannelData as JObject;
            if (channelDataObj != null && channelDataObj["team"] != null)
            {
                // Teams channel response is slow, add this to tell user that the bot is processing the request
                await turnContext.SendActivityAsync(MessageFactory.Text("got, processing your request"), cancellationToken).ConfigureAwait(false);
            }
            var response = await _chatService.ProcessMessageAsync(messageText, chatId);
            // Send non-streaming response back to Teams Channel or Chat Group due to Teams limitation.
            // Once teams support streaming API for them, we can use the unified way below.
            await turnContext.SendActivityAsync(MessageFactory.Text(response.Message), cancellationToken);
            return;
        }
        try
        {
            // Initialize variables
            StringBuilder contentBuilder = new(1024); // Pre-allocate buffer for better performance
            int streamSequence = 1;
            Stopwatch lastUpdateTime = Stopwatch.StartNew();

            // Batch collection for chunks
            List<string> chunkBatch = new(BATCH_SIZE);

            // Send initial typing indicator
            ChannelData channelData = new ChannelData
            {
                StreamType = StreamType.Informative,
                StreamSequence = streamSequence++
            };

            // Send initial message and get streamId
            string streamId = await BuildAndSendStreamingActivity(
                turnContext,
                cancellationToken,
                "Processing...",
                channelData).ConfigureAwait(false);

            // Process chunks with batching and enforced minimum wait time
            await foreach (var chunk in _chatService.ProcessMessageStreamAsync(messageText, chatId, cancellationToken))
            {
                contentBuilder.Append(chunk);
                chunkBatch.Add(chunk);

                // Check if it's time to send an update, but ALWAYS ensure minimum time has elapsed
                if (chunkBatch.Count >= BATCH_SIZE && lastUpdateTime.ElapsedMilliseconds > UPDATE_INTERVAL_MS)
                {
                    // Update with current content
                    channelData = new ChannelData
                    {
                        StreamType = StreamType.Streaming,
                        StreamSequence = streamSequence++,
                        StreamId = streamId
                    };

                    await BuildAndSendStreamingActivity(
                        turnContext,
                        cancellationToken,
                        contentBuilder.ToString(),
                        channelData);

                    // Reset batch and timer
                    chunkBatch.Clear();
                    lastUpdateTime.Restart();
                }
            }

            // Ensure we wait the minimum interval before sending final messages
            int remainingWaitTime = UPDATE_INTERVAL_MS - (int)lastUpdateTime.ElapsedMilliseconds;
            if (remainingWaitTime > 0)
            {
                await Task.Delay(remainingWaitTime, cancellationToken);
            }

            // Send final message, remaining content will be included
            channelData = new ChannelData
            {
                StreamType = StreamType.Final,
                StreamSequence = streamSequence,
                StreamId = streamId
            };

            await BuildAndSendStreamingActivity(
                turnContext,
                cancellationToken,
                contentBuilder.ToString(),
                channelData);

            _logger.LogInformation($"Completed streaming response for message: {messageText}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Teams bot message processing");
            await turnContext.SendActivityAsync($"Error process the request: {ex.Message}");
        }
    }

    /// <summary>
    /// Get or create a thread ID for this conversation,
    /// and proactively create a new Teams thread (post) in the channel with a message.
    /// </summary>
    private string CreateChatWithTeamsPost(string conversationId, string message)
    {
        lock (_conversationThreadMapping)
        {
            if (!_conversationThreadMapping.TryGetValue(conversationId, out string chatId))
            {
                // Create a new internal thread ID.
                string newChatId = Guid.NewGuid().ToString();
                _conversationThreadMapping[conversationId] = newChatId;

                // Retrieve the first stored conversation reference.
                if (!ConversationReferences.TryGetValue(conversationId, out var conversationReference))
                {
                    // If not found, select any available reference.
                    conversationReference = ConversationReferences.Values.FirstOrDefault();
                    if (conversationReference == null)
                    {
                        _logger.LogWarning("No conversation reference available for proactive messaging.");
                        return newChatId;
                    }
                }

                // Extract necessary details from the conversation reference.
                var serviceUrl = conversationReference.ServiceUrl;
                var continuationActivity = conversationReference.GetContinuationActivity();
                var teamsChannelData = continuationActivity.GetChannelData<TeamsChannelData>();
                string channelId = teamsChannelData?.Channel?.Id;
                string teamId = teamsChannelData?.Team?.Id;

                if (string.IsNullOrEmpty(channelId) || string.IsNullOrEmpty(serviceUrl))
                {
                    _logger.LogWarning("Missing channelId or serviceUrl in stored conversation reference.");
                    return newChatId;
                }

                var adapter = _teamsAdapter as CloudAdapter;
                if (adapter == null)
                {
                    _logger.LogError("Adapter is not a CloudAdapter instance.");
                    return newChatId;
                }

                // Fire-and-forget task to create a new thread on Teams.
                Task.Run(async () =>
                {
                    try
                    {
                        var conversationParameters = new ConversationParameters
                        {
                            IsGroup = true,
                            ChannelData = new TeamsChannelData
                            {
                                Team = new TeamInfo { Id = teamId },
                                Channel = new ChannelInfo { Id = channelId }
                            },
                            Activity = MessageFactory.Text(message)
                        };

                        await adapter.CreateConversationAsync(
                            botAppId: null, // Use null if the bot's App ID is configured in the adapter.
                            serviceUrl: serviceUrl,
                            channelId: channelId,
                            audience: null,
                            conversationParameters: conversationParameters,
                            callback: async (turnContext, ct) =>
                            {
                                await turnContext.SendActivityAsync("New conversation thread created.", cancellationToken: ct);
                            },
                            cancellationToken: CancellationToken.None);

                        _logger.LogInformation($"Created new thread with Teams post {newChatId} for conversation {conversationId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to create thread with Teams post for conversation {conversationId}");
                    }
                });

                return newChatId;
            }

            return chatId;
        }
    }

    /// <summary>
    /// Get or create a thread ID for this conversation with improved performance
    /// </summary>
    private string GetOrCreateChatId(string conversationId)
    {
        lock (_conversationThreadMapping)
        {
            if (!_conversationThreadMapping.TryGetValue(conversationId, out string chatId))
            {
                // Create thread asynchronously but return immediately with a new ID
                // This avoids blocking the main thread
                string newChatId = Guid.NewGuid().ToString();
                _conversationThreadMapping[conversationId] = newChatId;

                // Fire and forget task to create the thread
                Task.Run(async () =>
                {
                    try
                    {
                        string createdChatId = await _chatService.StartThreadAsync("/", newChatId);
                        // If IDs don't match, update the mapping
                        if (createdChatId != newChatId)
                        {
                            lock (_conversationThreadMapping)
                            {
                                _conversationThreadMapping[conversationId] = createdChatId;
                            }
                        }
                        _logger.LogInformation($"Created new thread {createdChatId} for Teams conversation {conversationId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to create thread for conversation {conversationId}");
                    }
                });

                return newChatId;
            }

            return chatId;
        }
    }

    /// <summary>
    /// Builds the activity with the corresponding data for streaming and sends it.
    /// </summary>
    private async Task<string> BuildAndSendStreamingActivity(
        ITurnContext<IMessageActivity> turnContext,
        CancellationToken cancellationToken,
        string text,
        ChannelData channelData)
    {
        bool isStreamFinal = channelData.StreamType.ToString().Equals(StreamType.Final.ToString());
        Activity streamingActivity = new()
        {
            Type = isStreamFinal ? ActivityTypes.Message : ActivityTypes.Typing,
            Id = channelData.StreamId,
            ChannelData = channelData,
            Text = text // Set text directly here instead of conditional assignment
        };

        // Create streaming info properties once
        var streamingInfoProperties = new
        {
            streamId = channelData.StreamId,
            streamType = channelData.StreamType.ToString(),
            streamSequence = channelData.StreamSequence,
        };

        // Add to entities collection
        streamingActivity.Entities = new List<Entity>
        {
            new Entity("streaminfo")
            {
                Properties = JObject.FromObject(streamingInfoProperties)
            }
        };

        return await SendStreamingActivityAsync(turnContext, cancellationToken, streamingActivity).ConfigureAwait(false);
    }

    private async Task<string> SendStreamingActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken, IActivity streamingActivity)
    {
        try
        {
            ResourceResponse streamingResponse = await turnContext.SendActivityAsync(streamingActivity, cancellationToken).ConfigureAwait(false);
            return streamingResponse.Id;
        }
        catch (Exception ex)
        {
            var errorResponse = ex as ErrorResponseException;
            string errorMessage = "Error while sending streaming activity: " + (errorResponse?.Body?.Error?.Message ?? ex.Message);
            _logger.LogError(ex, errorMessage);

            // Only send error message back to user if it's a critical error
            if (ex is not TimeoutException)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(errorMessage), cancellationToken).ConfigureAwait(false);
            }

            throw new Exception(errorMessage, ex);
        }
    }

    // Welcome message handler
    protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        var teamsChannelId = turnContext.Activity.TeamsGetChannelId();
        var serviceUrl = turnContext.Activity.ServiceUrl;
        var conversationReference = turnContext.Activity.GetConversationReference();
        conversationReference.ServiceUrl = serviceUrl;
        // TODO: we need to get the channel id here, it's empty for now.
        conversationReference.ChannelId = teamsChannelId;
        _logger.LogInformation($"Teams Conversation (OnMembersAddedAsync): Received message from Teams channel {teamsChannelId}, service URL: {serviceUrl}");
        //lock (ConversationReferences)
        //  {
        //  ConversationReferences[conversationReference.Conversation.Id] = conversationReference;
        //}

        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(welcomeMessage), cancellationToken);
            }
        }
    }
    // This function can help us send the "typing" indicator to the user, it's not useful in streaming API which has the "processing" indicator, but it's useful in non-streaming API scenario such as teams channel or chat group.
    public async override Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {

        ITypingActivity replyActivity = Activity.CreateTypingActivity();

        await turnContext.SendActivityAsync((Activity)replyActivity).ConfigureAwait(false);

        await Task.Delay(200);

        await base.OnTurnAsync(turnContext, cancellationToken);

    }


    //-----Subscribe to Conversation Events in Bot integration
    protected override async Task OnTeamsChannelCreatedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the Channel created");
        await turnContext.SendActivityAsync(MessageFactory.Attachment(heroCard.ToAttachment()), cancellationToken);
    }

    private async Task MentionActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var mention = new Mention
        {
            Mentioned = turnContext.Activity.From,
            Text = $"<at>{XmlConvert.EncodeName(turnContext.Activity.From.Name)}</at>",
        };

        var replyActivity = MessageFactory.Text($"Hello {mention.Text}.");
        replyActivity.Entities = new List<Entity> { mention };

        await turnContext.SendActivityAsync(replyActivity, cancellationToken);
    }
}

public class AdapterWithErrorHandler : CloudAdapter
{
    public AdapterWithErrorHandler(BotFrameworkAuthentication auth, ILogger<IBotFrameworkHttpAdapter> logger)
        : base(auth, logger)
    {
        OnTurnError = async (turnContext, exception) =>
        {
            // Log any leaked exception from the application.
            // NOTE: In production environment, you should consider logging this to
            // Azure Application Insights. Visit https://aka.ms/bottelemetry to see how
            // to add telemetry capture to your bot.
            logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

            // Only send error message for user messages, not for other message types so the bot doesn't spam a channel or chat.
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // Send a message to the user
                await turnContext.SendActivityAsync("The bot encountered an error or bug.");
                await turnContext.SendActivityAsync("To continue to run this bot, please fix the bot source code.");

                // Send a trace activity, which will be displayed in the Bot Framework Emulator
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, "https://www.botframework.com/schemas/error", "TurnError");
            }
        };
    }
}
