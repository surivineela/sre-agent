using System.Text.Json;
using Agent.Core;
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

namespace FirstPartyAgent.Web.Services;

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
        _history.AddSystemMessage(Prompts.QuotaAgent);
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()           // Disable HTML parsing
            .Build();
    }

    public async Task<QuotaIncidentState> Process(QuotaIncidentState state, IList<Disscussion> discussions)
    {
        _logger.LogInformation($"Processing request: {JsonSerializer.Serialize(state)}");
        var settings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            //ResponseFormat = typeof(QuotaChatResponse),
            Temperature = 0,
            TopP = 0.95,
        };

        ChatHistory chatHistory = new();
        chatHistory.AddSystemMessage(Prompts.QuotaAgent);
        var msg = state.SummarizeState();
        _logger.LogInformation($"User > {msg}");
        chatHistory.AddUserMessage(msg);
        if (discussions is not null)
        {
            foreach (var discussion in discussions)
            {
                var message = $"{discussion.User} said: {discussion.Message}";
                _logger.LogInformation($"User > {message}");
                chatHistory.AddUserMessage(message);
            }
        }

        IChatCompletionService chatService = _kernel.Services.GetRequiredService<IChatCompletionService>();
        ChatMessageContent result = await chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel).ConfigureAwait(false);

        if (result is null)
        {
            _logger.LogInformation($"No result is returned from Agent.");
            // enqueue original message
            state.LastUpdateTimestamp = DateTime.UtcNow;
            return state;
        }

        //_logger.LogInformation($"Chat history: {JsonSerializer.Serialize(chatHistory)}");

        IEnumerable<FunctionCallContent> functionCalls = FunctionCallContent.GetFunctionCalls(result);
        foreach (FunctionCallContent functionCall in functionCalls)
        {
            _logger.LogInformation("Function call: {}", functionCall);
        }

        _logger.LogInformation($"Assistant > {result.Content}");
        chatHistory.AddAssistantMessage(result.Content ?? string.Empty);

        var resp = JsonSerializer.Deserialize<QuotaRequest>(result.Items[0].ToString());

        if (resp is null)
        {
            _logger.LogInformation($"Failed to deserialize result.");
            // enqueue original message
            state.LastUpdateTimestamp = DateTime.UtcNow;

            await _taskStorageService.UpdateTaskAsync(state);
            return state;
        }

        var discussionMsg = $"{resp.Message}\n{resp.ToString()}";

        await _icmPlugin.AddDiscussionEntry(state.IncidentId, discussionMsg);

        if (string.IsNullOrEmpty(state.TeamsMessageId))
        {
            var teamsResp = await _cappPlugin.PostTeamsDiscussionAsync(state.IncidentId, state.Title ?? "New GPU Quota Request Received", discussionMsg);
            if (teamsResp is null || string.IsNullOrEmpty(teamsResp.MessageId))
            {
                throw new Exception("Failed to get messageId from Teams.");
            }
            state.TeamsMessageId = teamsResp.MessageId;
        }
        else
        {
            await _cappPlugin.ReplyTeamsDiscussionAsync(state.IncidentId, state.TeamsMessageId, discussionMsg);
        }

        if (resp.ApprovalResult == ApprovalState.NotStarted || resp.ApprovalResult == ApprovalState.Pending)
        {
            _logger.LogInformation($"Need more user inputs to proceed");

            state.Summary = resp.Message;
            state.Request = resp;
            state.LastUpdateTimestamp = DateTime.UtcNow;
            await _taskStorageService.UpdateTaskAsync(state);

            return state;
        }

        var logMsg = "";
        if (resp.ApprovalResult == ApprovalState.Approved)
        {
            _logger.LogInformation("Quota request approved and geneva action executed.");
            logMsg = $"Quota request approved and geneva action executed. Incident resolved. <br/>- Region: {resp.Region} <br/>- Quota Type: {resp.QuotaType} <br/>- Approved Quota: {resp.ApprovedQuotaLimit}.";
        }
        else
        {
            _logger.LogInformation("Quota request rejected.");
            logMsg = $"Quota request rejected. Incident resolved. <br/>- Region: {resp.Region} <br/>- Quota Type: {resp.QuotaType} <br/>- Approved Quota: {resp.ApprovedQuotaLimit} <br/>- Reason: {resp.Message}.";
        }

        await _icmPlugin.ResolveIncident(state.IncidentId, logMsg);
        await _cappPlugin.ReplyTeamsDiscussionAsync(state.IncidentId, state.TeamsMessageId, logMsg);

        await _taskStorageService.RemoveTaskAsync(state.IncidentId);
        return null;
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
                executionSettings: new()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
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
}

#pragma warning restore SKEXP0010