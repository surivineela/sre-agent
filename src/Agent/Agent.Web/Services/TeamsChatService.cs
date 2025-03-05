using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Agent.Web.Models.Streaming;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Newtonsoft.Json.Linq;
using Activity = Microsoft.Bot.Schema.Activity;

namespace Agent.Web.Services;

public class TeamsBot : TeamsActivityHandler
{
    private readonly IChatService _chatService;
    private readonly ILogger<TeamsBot> _logger;
    private readonly Dictionary<string, string> _conversationThreadMapping;

    // Teams has strict limitation for activities per second: https://learn.microsoft.com/en-us/microsoftteams/platform/bots/how-to/rate-limit#per-bot-per-thread-limit
    // Constants for optimization
    private const int BATCH_SIZE = 16; // Process chunks in batches of 16
    // TODO: we need to design a better client side ratelimiter to satisfy teams throttling policy while keep sending the message in a timely manner
    // 7 per 1s
    // 8 per 2s
    // 60 per 30s
    // 1800 per 3600s
    private const int UPDATE_INTERVAL_MS = 300; // Send updates every 300ms, 

    public TeamsBot(IChatService chatService, ILogger<TeamsBot> logger)
    {
        _chatService = chatService;
        _logger = logger;
        _conversationThreadMapping = new Dictionary<string, string>();
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
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
            await turnContext.SendActivityAsync(MessageFactory.Text("Hello! I'm your SRE agent assistant. How can I help you today?"), cancellationToken);
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
        var welcomeText = "Hello! I'm your SRE agent assistant. How can I help you today?";
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText), cancellationToken);
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
