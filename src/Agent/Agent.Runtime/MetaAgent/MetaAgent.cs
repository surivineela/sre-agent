using System.Text.Json;
using Agent.Core;
using Microsoft.Extensions.AI;
using Agent.Data.Repositories;

namespace Agent.Runtime.MetaAgent;

// [Export]
public sealed class MetaAgent : IAgent
{
    private const string SystemPrompt = @"# Azure SRE Agent

You are a specialized Azure SRE Agent designed to assist users with Microsoft Azure products and services. Your primary role is to understand user requests and delegate tasks to appropriate sub-agents when necessary.

## Primary Capabilities
- **Managed Identity Migration**: Help users migrate from certificate-based authentication to managed identities
- **TLS Best Practices**: Guide users in implementing TLS best practices for Azure resources

## Core Responsibilities
1. **Request Triage**: Determine if a user request is related to Azure SRE concerns
2. **Sub-Agent Delegation**: Route requests to specialized sub-agents when appropriate
3. **Workflow Management**: Start, monitor, and summarize various Azure-related workflows

## Response Protocol
- Maintain focus exclusively on Microsoft Azure products and services
- Decline to respond to non-Azure related queries with a polite redirection
- When delegating to sub-agents, clearly communicate the handoff process to users
- Provide concise, actionable responses formatted according to Microsoft Teams guidelines

## Operation Framework
When handling Azure SRE requests, follow this general pattern:

1. **List**: Provide users with available options and workflows relevant to their query
2. **Summarize**: Explain details of a specific option when requested or selected
3. **Start**: Initiate the appropriate workflow by delegating to specialized sub-agents

This framework applies to all Azure SRE operations, allowing you to:
- Help users discover available capabilities
- Provide detailed information before taking action
- Seamlessly transition to specialized sub-agents for execution

## Formatting Guidelines
Format all responses according to Microsoft Teams markdown support:
- Use **bold** for emphasis and key points
- Use *italics* for parameters or variables
- Use bulleted or numbered lists for steps and options
- Use code blocks with triple backticks for code or configuration examples
- Use headings (###) for organizing complex responses
- Avoid tables, HTML tags, and other unsupported formats

DO NOT RESPOND IF THE QUESTION IS NOT ABOUT MICROSOFT AZURE.";

    private readonly IThreadRepository _repository;
    private readonly List<ChatMessage> _chatHistory = new();
    private readonly List<AIFunction> _aiTools = new();
    private readonly AsyncReaderWriterLock _lock = new();

    private readonly IChatClient _chatClient;

    public MetaAgent(
        IChatClient chatClient,
        IThreadRepository repository,
        ManagedIdentityMigrationPlugin managedIdentityMigrationPlugin,
        TlsBestPracticesPlugin tlsBestPracticesPlugin)
    {
        _chatClient = chatClient;
        _repository = repository;
        _chatHistory.Add(new ChatMessage(ChatRole.System, SystemPrompt));

        _aiTools.Add(AIFunctionFactory.Create(managedIdentityMigrationPlugin.ListManagedIdentityMigrations));
        _aiTools.Add(AIFunctionFactory.Create(managedIdentityMigrationPlugin.SummarizeManagedIdentityMigration));
        _aiTools.Add(AIFunctionFactory.Create(managedIdentityMigrationPlugin.StartManagedIdentityMigrationAgent));
        _aiTools.Add(AIFunctionFactory.Create(tlsBestPracticesPlugin.ListTlsBestPracticeWorkflows));
        _aiTools.Add(AIFunctionFactory.Create(tlsBestPracticesPlugin.SummarizeTlsBestPractice));
        _aiTools.Add(AIFunctionFactory.Create(tlsBestPracticesPlugin.StartTlsBestPracticeAgent));
    }

    // TODO: the userMessage is not needed as we are using the repository to get the messages
    public async Task<string> ProcessUserMessage(string userMessage, string threadId)
    {
        using var _ = await _lock.AcquireWriterAsync();
        Guid threadGuid = Guid.Parse(threadId);
        var threadMessages = await _repository.GetMessagesAsync(threadGuid);
        foreach (var msg in threadMessages)
        {
            ChatRole role = msg.Author.Role == Core.Models.Api.v1.Role.User ? ChatRole.User : ChatRole.Assistant;
            _chatHistory.Add(new ChatMessage(role, msg.Text));
        }

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
                        ["ThreadId"] = threadId, // Pass threadId in additional properties
                    }
                });

            // Add model response back to ChatHistory
            // TODO: do we add the intermediate responses to the repository?
            _chatHistory.Add(response.Message);

            var results = new List<AIContent>();
            foreach (var fnCall in response.Message.Contents.OfType<FunctionCallContent>())
            {
                var matchingTool = _aiTools.Single(x => x.Name == fnCall.Name);

                // Create a new dictionary with the thread ID if needed
                IDictionary<string, object?> arguments = fnCall.Arguments;
                if (!string.IsNullOrEmpty(threadId))
                {
                    // Make a copy of the arguments and add the threadId
                    arguments = new Dictionary<string, object?>(fnCall.Arguments);

                    // Inject threadId to the arguments to avoid hallucination
                    arguments["threadId"] = threadId;
                }

                // Invoke function call with potentially modified arguments
                var invokeResult = await matchingTool.InvokeAsync(arguments);
                var result = new FunctionResultContent(fnCall.CallId, invokeResult);
                results.Add(result);
            }

            if (results.Count > 0)
            {
                // Add function call response, and re-evaluate the ChatHistory with model
                // TODO: do we add the intermediate tools responses to the repository?
                _chatHistory.Add(new ChatMessage(ChatRole.Tool, results));
            }
            else
            {
                // When model has no function call response, we assume it's a single text response
                // We return this response to user
                // This response will be added to repository outside of this method
                return response.Message.Contents.OfType<TextContent>().Single().Text;
            }
        }
    }
}