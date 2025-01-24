using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace OperationalAgentRuntime.Planner;

public class SkillsPlanner
{
    private readonly Kernel _kernel;
    private readonly ILogger<SkillsPlanner> _logger;
    private readonly IChatCompletionService _chatService;

    public SkillsPlanner(Kernel kernel, ILogger<SkillsPlanner> logger)
    {
        _kernel = kernel;
        _logger = logger;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<string> ExecuteRequestAsync(string userInput, CancellationToken cancellationToken = default)
    {
        try
        {
            throw new NotImplementedException();
            //_logger.LogInformation("Planning execution for request: {UserInput}", userInput);

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(userInput);

            var executionSettings = new OpenAIPromptExecutionSettings 
            { 
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() 
            };

            var result = await _chatService.GetChatMessageContentAsync(
                chatHistory, 
                executionSettings,
                _kernel,
                cancellationToken);
            
            return result.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing request: {UserInput}", userInput);
            throw;
        }
    }

    // TODO: Classify Skills?
    public string GetAvailableSkills()
    {
        throw new NotImplementedException();
        //var skills = _kernel.Plugins.GetFunctionsMetadata()
        //    .GroupBy(f => f.PluginName)
        //    .Select(g => new
        //    {
        //        SkillName = g.Key,
        //        Functions = g.Select(f => f.Name).ToList()
        //    });

        //return JsonSerializer.Serialize(skills, new JsonSerializer.Options { WriteIndented = true });
    }
}
#pragma warning restore SKEXP0060 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
