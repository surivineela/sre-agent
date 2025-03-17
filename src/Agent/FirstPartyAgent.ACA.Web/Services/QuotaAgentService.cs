// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Models;
using FirstPartyAgent.Agents;
using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Markdig;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace FirstPartyAgent.ACA.Web.Services;

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class QuotaAgentService : IQuotaAgentService
{
    private readonly ILogger<QuotaAgentService> _logger;
    private readonly Kernel _kernel;
    private readonly IIcmPlugin _icmPlugin;
    private readonly IContainerAppsPlugin _cappPlugin;
    private readonly ITaskStorageService _taskStorageService;

    // single chat session
    private static readonly AsyncReaderWriterLock _lock = new();
    private ChatHistory _history;
    private readonly MarkdownPipeline _markdownPipeline;

    public QuotaAgentService(IServiceProvider provider,
        ILogger<QuotaAgentService> logger,
        Kernel kernel,
        IIcmPlugin icmPlugin,
        IContainerAppsPlugin cappPlugin,
        ITaskStorageService taskStorageService)
    {
        _logger = logger;
        _kernel = kernel.Clone();


        _kernel.Plugins.AddFromType<IcmPluginDefinition>(nameof(IcmPluginDefinition), provider);
        _kernel.Plugins.AddFromType<ContainerAppsPluginDefinition>(nameof(ContainerAppsPluginDefinition), provider);

        _icmPlugin = icmPlugin;
        _cappPlugin = cappPlugin;
        _taskStorageService = taskStorageService;

        _history = new ChatHistory();
        _history.AddSystemMessage(ContainerAppAgent.GpuQuota.SystemMessage);
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()           // Disable HTML parsing
            .Build();
    }

    public async Task<QuotaIncidentState> Process(QuotaIncidentState state, IList<ConversationEntry> discussions)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        _logger.LogInformation($"Processing request: {JsonSerializer.Serialize(state)}");
        var settings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0,
            TopP = 0.95,
        };

        ChatHistory chatHistory = new();
        chatHistory.AddSystemMessage(_logger, ContainerAppAgent.GpuQuota.SystemMessage);

        if (state.IsNewRequest)
        {
            chatHistory.AddUserMessage(_logger, state.ToString());
        }
        else
        {
            chatHistory.AddAssistantMessage(_logger, state.ToString());
        }

        if (discussions is not null)
        {
            foreach (var discussion in discussions)
            {
                var message = $"{discussion.Message}. Note: this message is provider by {discussion.User}.";
                chatHistory.AddUserMessage(_logger, message);
            }
        }

        IChatCompletionService chatService = _kernel.Services.GetRequiredService<IChatCompletionService>();



        bool needProcess = true;
        int retry = 0;
        QuotaIncidentState? newState = null;

        do
        {
            ChatMessageContent result = await chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel).ConfigureAwait(false);

            retry++;
            needProcess = true;
            
            if (result is null)
            {
                _logger.LogError($"No result is returned from Agent. Retry = {retry}");

                if (retry >= 3)
                {
                    await _taskStorageService.UpdateTaskAsync(state);
                    return state;
                }
            }
            else
            {
                chatHistory.AddAssistantMessage(_logger, result.Content ?? string.Empty);

                try
                {
                    newState = JsonSerializer.Deserialize<QuotaIncidentState>(result.Items[0].ToString(), new JsonSerializerOptions
                    {
                        NumberHandling = JsonNumberHandling.AllowReadingFromString
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to deserialize result.");
                    newState = null;
                }

                if (result.Items.Count > 1)
                {
                    _logger.LogWarning($"More than one result is returned from Agent. Only the first one is used.");
                }

                if (newState is null)
                {
                    _logger.LogWarning($"Failed to deserialize result.");
                    chatHistory.AddUserMessage(_logger, ContainerAppAgent.GpuQuota.AskFormattedResponseMessage);
                }
                else if (!string.IsNullOrEmpty(newState.QuotaType) && newState.QuotaType.Contains(" "))
                {
                    _logger.LogWarning($"The quota type is not normalized.");
                    chatHistory.AddUserMessage(_logger, ContainerAppAgent.GpuQuota.AskNormalizeQuotaTypeMessage);
                }
                else if (!string.IsNullOrEmpty(newState.Region) && newState.Region.Contains(" "))
                {
                    _logger.LogWarning($"The region is not normalized.");
                    chatHistory.AddUserMessage(_logger, ContainerAppAgent.GpuQuota.AskNormalizeRegionMessage);
                }
                else
                {
                    if (newState.ApprovalResult == ApprovalState.NotStarted &&
                        string.IsNullOrEmpty(newState.OfferType) &&
                        !string.IsNullOrEmpty(newState.QuotaType) &&
                        !string.IsNullOrEmpty(newState.Region) &&
                        !string.IsNullOrEmpty(newState.SubscriptionId) &&
                        newState.TargetQuotaLimit != null)
                    {
                        // For some reason, the AI didn't get the offer type. But all the information is extracted. It is usually because it is asking some studio question.
                        chatHistory.AddUserMessage(_logger, "I don't understand you questions. Because it seems you have already extracted the information you needed. Can you please re-process the request and give a new response? ");
                        continue;
                    }

                    needProcess = false;
                }
            }
        }
        while (needProcess && retry < 5);

        if (newState != null) {
            state.UpdateFrom(newState);
        }
        
        state.IsNewRequest = false;

        if ( !string.IsNullOrEmpty(state.QuotaType) && !state.QuotaType.Contains("GPU", StringComparison.OrdinalIgnoreCase))
        {
            state.ApprovalResult = ApprovalState.NotSupported;
            return state;
        }

        var messageContent = state?.ToString();
        if (state?.ApprovalResult == ApprovalState.Pending && state.SubscriptionId != null)
        {
            state.HasBeenPended = true;
            StringBuilder referenceBuilder = new StringBuilder();
            await AppendSubscriptionUsageInformation(referenceBuilder, state.SubscriptionId);
            AppendReferenceInformation(referenceBuilder, state.SubscriptionId);           
            messageContent += referenceBuilder.ToString();
        }

        if (state?.Incident?.IncidentId != null)
        {
            await _icmPlugin.AddDiscussionEntry(state.Incident.IncidentId, messageContent);
        }
        else
        {
            _logger.LogWarning("IncidentId is null. Cannot add discussion entry.");
        }

        if (string.IsNullOrEmpty(state?.ConversationContext?.TeamsMessageId))
        {
            var teamsResp = await _cappPlugin.PostTeamsDiscussionAsync(
                state?.Incident?.IncidentId,
                state?.Incident?.Title ?? "New GPU Quota Request Received",
                messageContent);

            if (teamsResp is null || string.IsNullOrEmpty(teamsResp.MessageId))
            {
                throw new Exception("Failed to get messageId from Teams.");
            }

            if ( state?.ConversationContext is null)
            {
                state.ConversationContext = new ConversationContext();
                state.ConversationContext.IncidentId = state.Incident?.IncidentId;
            }

            state.ConversationContext.TeamsMessageId = teamsResp.MessageId;
        }
        else
        {
            await _cappPlugin.ReplyTeamsDiscussionAsync(state.Incident?.IncidentId, state.ConversationContext?.TeamsMessageId, messageContent);
        }

        if (state.ApprovalResult == ApprovalState.NotStarted || state.ApprovalResult == ApprovalState.Pending)
        {
            _logger.LogInformation($"Need more user inputs to proceed");
            await _taskStorageService.UpdateTaskAsync(state);
            return state;
        }

        var logMsg = "";

        bool resolveIncident = false;

        await _icmPlugin.AddTag(state.Incident.IncidentId, "ai agent");

        if (state.ApprovalResult == ApprovalState.Approved)
        {
            try
            {
                var result = await _cappPlugin.SetSubscriptionQuota(state.SubscriptionId, state.Region, state.QuotaType, state.ApprovedQuotaLimit?.ToString());

                resolveIncident = true;
                _logger.LogInformation("Quota request approved and geneva action executed.");
                logMsg = $"Quota request approved and geneva action executed. Incident resolved. <br/> {result}.";

                string approveTag = state.HasBeenPended ? "manual approve" : "auto approve";
                await _icmPlugin.AddTag(state.Incident.IncidentId, approveTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute geneva action.");
                logMsg = $"Quota request approved but failed to execute geneva action. Error: {ex}.";
            }
        }
        else
        {
            resolveIncident = true;
            _logger.LogInformation("Quota request rejected.");
            logMsg = $"Quota request rejected. Incident resolved. <br/>- Region: {state.Region} <br/>- Quota Type: {state.QuotaType} <br/>- Approved Quota: {state.ApprovedQuotaLimit} <br/>- Reason: {state.Summary}.";

            string rejectTag = state.HasBeenPended ? "manual reject" : "auto reject";
            await _icmPlugin.AddTag(state.Incident.IncidentId, rejectTag);
        }

        var successfullyResolved = false;
        if (resolveIncident)
        {
            successfullyResolved = await _icmPlugin.ResolveIncident(state.Incident.IncidentId, logMsg);
        }

        await _cappPlugin.ReplyTeamsDiscussionAsync(state.Incident.IncidentId, state.ConversationContext.TeamsMessageId, logMsg);

        if (successfullyResolved)
        {
            await _taskStorageService.RemoveTaskAsync(state.Incident.IncidentId);
            var offTrackMsg = "The quota request has been successfully resolved. The agent will now stop proactive tracking on this case.";
            await _cappPlugin.ReplyTeamsDiscussionAsync(state.Incident.IncidentId, state.ConversationContext.TeamsMessageId, offTrackMsg);
        }

        return state;
    }

    public async Task<ChatMessage> ProcessMessageAsync(string message)
    {
        using var _ = await _lock.AcquireWriterAsync();
        try
        {
            if (message != null)
            {
                _history.AddUserMessage(message);
            }

            // Load tracked app states before adding user message
            var trackedStates = TrackedActionHelper.GetActions(type: ActionType.AppStateTracking)
                .OrderByDescending(a => a.Timestamp)
                .DistinctBy(a => a.Metadata["name"])
                .ToList();

            _logger.LogInformation("User > " + message);
            FunctionChoiceBehaviorOptions options = new() { AllowParallelCalls = true };

            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletionService.GetChatMessageContentAsync(
                _history,
                executionSettings: new AzureOpenAIPromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    //ResponseFormat = typeof(QuotaChatResponse),
                    Temperature = 0,
                    TopP = 0.95,
                },
                kernel: _kernel);

            IEnumerable<FunctionCallContent> functionCalls = FunctionCallContent.GetFunctionCalls(result);

            foreach (FunctionCallContent functionCall in functionCalls)
            {
                FunctionResultContent resultContent = await functionCall.InvokeAsync(_kernel);

                _history.Add(resultContent.ToChatMessage());
            }

            Console.WriteLine("Assistant > " + result);
            _history.AddMessage(result.Role, result.Content ?? string.Empty);

            string content = result.Content ?? string.Empty;
            var htmlContent = Markdown.ToHtml(content, _markdownPipeline);

            return new ChatMessage()
            {
                Message = htmlContent,  // Raw markdown
                Timestamp = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running the chat service");
            throw;
        }
    }

    public static void AppendReferenceInformation(StringBuilder messageBuilder, string subscriptionId)
    {
        messageBuilder.AppendLine("<br/>");
        messageBuilder.AppendLine($"-------- Reference Information --------<br/>");

        string sherlockLink = $"https://sherlock.trafficmanager.net/Customers?customer={subscriptionId}";
        messageBuilder.AppendLine("You can use Sherlock to find out usage details and offer status of the subscription:<br/>");
        messageBuilder.AppendLine($"<a href=\"{sherlockLink}\">Sherlock Link for {subscriptionId}</a><br/>");

        string refDocLink = $"https://eng.ms/docs/cloud-ai-platform/devdiv/serverless-paas-balam/serverless-paas-vikr/azure-container-apps/container-apps-on-call-process-tsg/troubleshooting/tsg/tsg-capps-quota-008";
        messageBuilder.AppendLine("<br/>You can find the general guidelines for GPU quota request handling from:");
        messageBuilder.AppendLine($"<a href=\"{refDocLink}\">GPU quota handling guidelines</a>");
    }

    public async Task AppendSubscriptionUsageInformation(StringBuilder messageBuilder, string subscriptionId)
    {
        AcaSubscriptionUsage? usage = null;
        try
        {
            usage = await _cappPlugin.GetSubscriptionUsage(subscriptionId);
            messageBuilder.AppendLine("<br/>");
            messageBuilder.AppendLine($"-------- Subscription Usage Information--------<br/>");
            if (usage != null && (!string.IsNullOrEmpty(usage.TrustLevel) || !string.IsNullOrEmpty(usage.NumberOfEnvironments) || !string.IsNullOrEmpty(usage.NumberOfContainerApps) || !string.IsNullOrEmpty(usage.NumberOfJobs)))
            {
                messageBuilder.AppendLine($"- Trust Level: {usage.TrustLevel}<br/>");
                messageBuilder.AppendLine($"- Number Of Environments: {usage.NumberOfEnvironments}<br/>");
                messageBuilder.AppendLine($"- Number Of ContainerApps: {usage.NumberOfContainerApps}<br/>");
                messageBuilder.AppendLine($"- Number Of Jobs: {usage.NumberOfJobs}<br/>");
            }
            else
            {
                messageBuilder.AppendLine($"Cannot find Azure Container Apps Environment/App/Job usage record for subscription {subscriptionId}.<br/>");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get subscription usage information for {subscriptionId}.");
        }
    }
}

#pragma warning restore SKEXP0010