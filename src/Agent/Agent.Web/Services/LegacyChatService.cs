namespace Agent.Web.Services;

using Agent.Core;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Markdig;

public class LegacyChatService : IChatService
{

    private readonly ILogger<LegacyChatService> _logger;
    private readonly Kernel _kernel;
    private readonly MarkdownPipeline _markdownPipeline;

    public LegacyChatService(ILogger<LegacyChatService> logger, Kernel kernel)
    {
        _logger = logger;
        _kernel = kernel;
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()           // Disable HTML parsing
            .Build();
    }

    public async Task<ChatMessage> ProcessMessageAsync(string message)
    {
        return await ChatHistoryPersistency.ChatHistoryTransition(
                async chatHistory =>
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
                    FunctionChoiceBehaviorOptions options = new() { AllowParallelCalls = true };

                    var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
                    var result = await chatCompletionService.GetChatMessageContentAsync(
                        chatHistory,
                        executionSettings: new()
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                        },
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
                });
    }
}