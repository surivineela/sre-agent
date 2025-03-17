using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Xml;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.Streaming;
using Agent.Core.Configuration;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Activity = Microsoft.Bot.Schema.Activity;

namespace Agent.Core.Services;

public class TeamsBot : TeamsActivityHandler
{
    private readonly ILogger<TeamsBot> _logger;
    private readonly Dictionary<string, string> _conversationThreadMapping;
    private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
    private readonly IThreadRepository _threadRepository;
    public static Dictionary<string, ConversationReference> ConversationReferences = new();
    private readonly IBotFrameworkHttpAdapter _teamsAdapter;

    // Track all posted message IDs per thread to prevent duplicate postings
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _postedMessageIds = new();
    // Track last poll timestamp per thread to limit fetching window
    private readonly ConcurrentDictionary<Guid, DateTime> _lastPollTimestamps = new();

    private readonly CancellationTokenSource _pollingCancellationSource = new();
    private bool _isPollingStarted = false;
    private readonly string _appId;
    // Increased polling interval to reduce frequency
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    // Rate limiting for messages posted per poll cycle
    private const int MAX_MESSAGES_PER_POLL = 50;

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
    public TeamsBot(
        ILogger<TeamsBot> logger,
        IBotFrameworkHttpAdapter teamsAdapter,
        IAgentInboundCommunicationService agentInboundCommunicationService,
        IThreadRepository threadRepository,
        TeamsBotSettings teamsBot)
    {
        _logger = logger;
        _conversationThreadMapping = new Dictionary<string, string>();
        _teamsAdapter = teamsAdapter;
        _agentInboundCommunicationService = agentInboundCommunicationService;
        _threadRepository = threadRepository;

        // Initialize credentials from configuration
        _appId = teamsBot.AppId;

        // Log credential information (without exposing the actual password)
        _logger.LogInformation($"TeamsBot initialized with AppId: {(_appId != null ? "Configured" : "Not Configured, disable sending proactive messages")}");

        StartMessagePolling();
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var teamsChannelId = turnContext.Activity.TeamsGetChannelId();
        var serviceUrl = turnContext.Activity.ServiceUrl;
        var conversationReference = turnContext.Activity.GetConversationReference();
        conversationReference.ServiceUrl = serviceUrl;
        conversationReference.ChannelId = teamsChannelId;
        _logger.LogInformation($"Teams Conversation: Received message from Teams channel {teamsChannelId}, service URL: {serviceUrl}");

        // Store conversation reference for later proactive messaging
        lock (ConversationReferences)
        {
            ConversationReferences[conversationReference.Conversation.Id] = conversationReference;
        }

        string messageText = turnContext.Activity.RemoveRecipientMention()?.Trim();
        if (string.IsNullOrEmpty(messageText))
        {
            _logger.LogInformation("Received empty message from user");
            return;
        }

        string conversationId = turnContext.Activity.Conversation.Id;
        string senderName = turnContext.Activity.From?.Name ?? "Unknown User";
        string userId = turnContext.Activity.From?.Id ?? "teams-user";

        // Get or create thread ID for this conversation
        string chatId = GetOrCreateChatId(conversationId);
        Guid chatIdGuid = Guid.Parse(chatId);

        _logger.LogInformation($"[Teams Conversation: {conversationId}][Thread: {chatId}]\nSending message to agent: {messageText}");

        if (messageText.ToLowerInvariant() == "hello")
        {
            // If the user says "hello", respond with a greeting quickly without using AI backend
            await turnContext.SendActivityAsync(MessageFactory.Text(welcomeMessage), cancellationToken);
            return;
        }
        try
        {
            // Teams channel and chat group don't support streaming API for now.
            // The new DTS integration don't support streaming API as well.
            var channelDataObj = turnContext.Activity.ChannelData as JObject;
            if (channelDataObj != null && channelDataObj["team"] != null)
            {
                // Teams channel response is slow, add this to tell user that the bot is processing the request
                await turnContext.SendActivityAsync(MessageFactory.Text("The Agent is processing your request..."), cancellationToken).ConfigureAwait(false);
            }

            // Process the message using the Threads API
            var response = await _agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage(
                ThreadId: chatIdGuid,
                Message: messageText,
                UserId: userId,
                DisplayName: senderName,
                Timestamp: DateTime.UtcNow
            ));
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
                            botAppId: _appId,
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
            if (!_conversationThreadMapping.TryGetValue(conversationId, out string threadId))
            {
                // Create a new thread ID for this conversation
                string newThreadId = Guid.NewGuid().ToString();
                _conversationThreadMapping[conversationId] = newThreadId;

                return newThreadId;
            }

            return threadId;
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
        conversationReference.ChannelId = teamsChannelId;
        _logger.LogInformation($"Teams Conversation (OnMembersAddedAsync): Received message from Teams channel {teamsChannelId}, service URL: {serviceUrl}");

        lock (ConversationReferences)
        {
            ConversationReferences[conversationReference.Conversation.Id] = conversationReference;
        }

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

    // Polling logic starts here
    public void StartMessagePolling()
    {
        if (_isPollingStarted)
            return;

        _isPollingStarted = true;
        _logger.LogInformation("Starting Teams message polling");

        // Run polling in a background task
        Task.Run(async () =>
        {
            while (!_pollingCancellationSource.Token.IsCancellationRequested)
            {
                try
                {
                    await PollForNewMessages();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Teams message polling");
                }

                // Wait before polling again
                await Task.Delay(_pollInterval, _pollingCancellationSource.Token);
            }
        }, _pollingCancellationSource.Token);
    }

    private async Task PollForNewMessages()
    {
        // Make a copy of the current mapping to avoid locking issues during enumeration
        var currentMappings = new Dictionary<string, string>();
        lock (_conversationThreadMapping)
        {
            foreach (var kvp in _conversationThreadMapping)
            {
                currentMappings[kvp.Key] = kvp.Value;
            }
        }

        foreach (var mapping in currentMappings)
        {
            try
            {
                string teamsConversationId = mapping.Key;
                string threadId = mapping.Value;

                if (!Guid.TryParse(threadId, out Guid threadGuid))
                {
                    _logger.LogWarning($"Invalid thread ID format: {threadId}");
                    continue;
                }

                // Get the current time for this poll cycle
                DateTime currentPollTime = DateTime.UtcNow;

                // Get the last poll time for this thread, or use a default (10 minutes ago)
                DateTime lastPollTime = _lastPollTimestamps.GetValueOrDefault(
                    threadGuid,
                    DateTime.UtcNow.AddMinutes(-10));

                // Ensure we're not querying too far back
                if (currentPollTime.Subtract(lastPollTime).TotalHours > 1)
                {
                    lastPollTime = currentPollTime.AddHours(-1);
                }

                // Update the last poll time for next cycle
                _lastPollTimestamps[threadGuid] = currentPollTime;

                // Get already posted message IDs for this thread
                HashSet<Guid> postedMessageIds = _postedMessageIds.GetOrAddValue(
                    threadGuid,
                    () => new HashSet<Guid>());

                // Get recent agent messages from the thread with a specific time window
                var messages = await _threadRepository.GetMessagesAsync(
                    threadGuid); // Limit to 50 most recent messages

                // Filter to get only new messages that haven't been posted yet
                var newMessages = messages
                    .Where(m => m.Author.Role == Role.SREAgent && !postedMessageIds.Contains(m.Id))
                    .OrderBy(m => m.TimeStamp)
                    .Take(MAX_MESSAGES_PER_POLL) // Rate limit: only post up to MAX_MESSAGES_PER_POLL messages per poll cycle
                    .ToList();

                if (newMessages.Any())
                {
                    _logger.LogInformation($"Found {newMessages.Count} new messages to post to Teams for thread {threadId}");

                    // Post messages to Teams
                    await PostMessagesToTeams(teamsConversationId, newMessages);

                    // Track all posted message IDs to prevent re-posting
                    lock (postedMessageIds)
                    {
                        foreach (var message in newMessages)
                        {
                            postedMessageIds.Add(message.Id);
                        }
                    }

                    // Log how many message IDs we're now tracking for this thread
                    _logger.LogInformation($"Now tracking {postedMessageIds.Count} posted message IDs for thread {threadId}");
                }
                else
                {
                    _logger.LogDebug($"No new messages to post for thread {threadId}. Total tracked messages: {postedMessageIds.Count}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error polling messages for conversation {mapping.Key}");
            }
        }
    }

    private async Task PostMessagesToTeams(string conversationId, List<Message> messages)
    {
        try
        {
            // Get the Teams conversation reference
            if (!ConversationReferences.TryGetValue(conversationId, out var conversationReference))
            {
                _logger.LogWarning($"No conversation reference found for conversation {conversationId}");
                return;
            }

            // Ensure we have the necessary credentials
            if (string.IsNullOrEmpty(_appId))
            {
                _logger.LogError("AppId is not configured. Cannot send proactive messages.");
                return;
            }

            // Get correctly typed adapter
            var adapter = _teamsAdapter as CloudAdapter;
            if (adapter == null)
            {
                _logger.LogError("Adapter is not a CloudAdapter instance. Cannot send proactive messages.");
                return;
            }

            foreach (var message in messages)
            {
                try
                {
                    // Create message activity
                    var activity = MessageFactory.Text(message.Text);

                    // Send the message using ContinueConversationAsync
                    await adapter.ContinueConversationAsync(
                        _appId,
                        conversationReference,
                        async (turnContext, ct) =>
                        {
                            await turnContext.SendActivityAsync(activity, ct);
                        },
                        CancellationToken.None);

                    _logger.LogInformation($"Posted message {message.Id} to Teams conversation {conversationId}");

                    // Respect Teams rate limits (7 messages per second)
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error posting message {message.Id} to Teams conversation {conversationId}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in PostMessagesToTeams for conversation {conversationId}");
        }
    }
}

// Helper extension method for ConcurrentDictionary
public static class ConcurrentDictionaryExtensions
{
    public static TValue GetOrAddValue<TKey, TValue>(
        this ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        Func<TValue> valueFactory)
    {
        return dictionary.GetOrAdd(key, _ => valueFactory());
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
