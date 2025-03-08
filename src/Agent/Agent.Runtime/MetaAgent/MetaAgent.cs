using Agent.Core;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.MetaAgent;

// [Export]
public sealed class MetaAgent
{
    // TODO: sys prompt and initial introductory msg to user
    private const string SystemPrompt = @"You're a helpful Agent to help user interact with Microsoft Azure products.
You may start various kinds of workflow depends on what the user asks.
You must scope all of your answer to Azure related questions.
DO NOT RESPOND IF QUSETION IS NOT ABOUT MICROSOFT AZURE";

    private readonly List<ChatMessage> _chatHistory = new();
    private readonly List<AIFunction> _aiTools = new();
    private readonly AsyncReaderWriterLock _lock = new();

    private readonly IChatClient _chatClient;

    public MetaAgent(
        IChatClient chatClient,
        ManagedIdentityMigrationPlugin managedIdentityMigrationPlugin,
        TlsBestPracticesPlugin tlsBestPracticesPlugin)
    {
        _chatClient = chatClient;
        _chatHistory.Add(new ChatMessage(ChatRole.System, SystemPrompt));

        _aiTools.Add(AIFunctionFactory.Create(managedIdentityMigrationPlugin.ListManagedIdentityMigrations));
        _aiTools.Add(AIFunctionFactory.Create(managedIdentityMigrationPlugin.SummarizeManagedIdentityMigration));
        _aiTools.Add(AIFunctionFactory.Create(managedIdentityMigrationPlugin.StartManagedIdentityMigrationAgent));
        _aiTools.Add(AIFunctionFactory.Create(tlsBestPracticesPlugin.ListTlsBestPracticeWorkflows));
        _aiTools.Add(AIFunctionFactory.Create(tlsBestPracticesPlugin.SummarizeTlsBestPractice));
        _aiTools.Add(AIFunctionFactory.Create(tlsBestPracticesPlugin.StartTlsBestPracticeAgent));
    }

    public async Task<string> ProcessUserMessage(
        string userMessage)
    {
        using var _ = await _lock.AcquireWriterAsync();

        _chatHistory.Add(new ChatMessage(ChatRole.User, userMessage));

        while (true)
        {
            var response = await _chatClient.GetResponseAsync(
                _chatHistory,
                new ChatOptions
                {
                    Tools = _aiTools.Select<AIFunction, AITool>(x => x).ToList(),
                    ToolMode = ChatToolMode.Auto,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["AllowParallelToolCalls"] = true,
                    }
                });
            // Add model response back to ChatHistory
            _chatHistory.Add(response.Message);

            var results = new List<AIContent>();
            foreach (var fnCall in response.Message.Contents.OfType<FunctionCallContent>())
            {
                var matchingTool = _aiTools.Single(x => x.Name == fnCall.Name);
                // Invoke function call if model decided so
                var invokeResult = await matchingTool.InvokeAsync(fnCall.Arguments);
                var result = new FunctionResultContent(fnCall.CallId, invokeResult);
                results.Add(result);
            }

            if (results.Count > 0)
            {
                // Add function call response, and re-evaluate the ChatHistory with model
                _chatHistory.Add(new ChatMessage(ChatRole.Tool, results));
            }
            else
            {
                // When model has no function call response, we assume it's a single text response
                // We return this response to user
                return response.Message.Contents.OfType<TextContent>().Single().Text;
            }
        }
    }
}
