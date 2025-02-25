namespace Agent.Web.Services;

using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Markdig;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agent.Core.Configuration;
using Microsoft.SemanticKernel.Connectors.OpenAI;

public class LegacyChatService : IChatService
{
    private readonly ILogger<LegacyChatService> _logger;
    private readonly Kernel _kernel;
    private readonly MarkdownPipeline _markdownPipeline;
    private readonly OpenAISettings _openAISettings;

    public async Task<string> CreateThreadAsync(string path, string threadId) => "";

    public async Task<List<ChatThread>> GetThreadsAsync() => new List<ChatThread>();

    public async Task SetThreadAsync(string threadId) { }

    public async Task<string?> GetCurrentThreadIdAsync() => null;


    public LegacyChatService(
        ILogger<LegacyChatService> logger,
        Kernel kernel,
        IOptions<AzureSettings> azureSettings)
    {
        _logger = logger;
        _kernel = kernel;
        _openAISettings = azureSettings.Value.OpenAI;
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()           // Disable HTML parsing
            .Build();
    }

    public async Task<ChatMessage> ProcessMessageAsync(string message, string? threadId)
    {
        return await ChatHistoryPersistency.ChatHistoryTransition(
                async chatHistory =>
                {
                    try
                    {
                        if (message != null)
                        {
                            chatHistory.AddUserMessage(message);
                        }

                        // Load tracked app states before adding user message
                        var trackedStates = TrackedActionHelper.GetActions(type: ActionType.AppStateTracking)
                            .OrderByDescending(a => a.Timestamp)
                            .DistinctBy(a => a.Metadata["name"])
                            .ToList();

                        _logger.LogInformation("User > " + message);

                        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

                        // Set execution settings based on model type
                        var executionSettings = new OpenAIPromptExecutionSettings();

                        if (ModelSelectionHelper.IsReasoningModel(_openAISettings.DeploymentName))
                        {
                            executionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true);
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                            executionSettings.ReasoningEffort = "high";
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                        }
                        else
                        {
                            executionSettings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true);
                        }

                        var result = await chatCompletionService.GetChatMessageContentAsync(
                            chatHistory,
                            executionSettings: executionSettings,
                            kernel: _kernel);

                        IEnumerable<FunctionCallContent> functionCalls = FunctionCallContent.GetFunctionCalls(result);

                        foreach (FunctionCallContent functionCall in functionCalls)
                        {
                            FunctionResultContent resultContent = await functionCall.InvokeAsync(_kernel);
                            chatHistory.Add(resultContent.ToChatMessage());
                        }

                        Console.WriteLine("Assistant > " + result);
                        chatHistory.AddMessage(result.Role, result.Content ?? string.Empty);

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
                });
    }

    public Task SwitchAgent(string path, string threadId)
    {
        // No-op for legacy service
        return Task.CompletedTask;
    }

    public Task<List<ChatMessage>> GetChatHistoryAsync(string threadId)
    {
        throw new NotImplementedException();
    }
}
