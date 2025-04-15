// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class GenericGetNextAction2Activity : TaskActivity<GetNextActionInput, ChatMessage>
{
    protected readonly IChatClient ChatClient;
    private readonly ToolsRepository _toolsRepository;
    private readonly ILogger<GenericGetNextAction2Activity> _logger;

    public GenericGetNextAction2Activity(
        IChatClient chatClient,
        ToolsRepository toolsRepository,
        ILogger<GenericGetNextAction2Activity> logger)
    {
        ChatClient = chatClient;
        _toolsRepository = toolsRepository;
        _logger = logger;
    }

    public override async Task<ChatMessage> RunAsync(TaskActivityContext context, GetNextActionInput input)
    {
        var chatOptions = new ChatOptions
    {
            Tools = _toolsRepository.GetAllTools(input.ToolSignatures).Select<string, AITool>(sig => _toolsRepository.FindAiFunction(sig).ToolFunction).ToList(),
            ToolMode = ChatToolMode.RequireAny,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["AllowParallelToolCalls"] = false
            }
        };

        var allMessages = _toolsRepository.MCPServerInstructions.Concat(input.ChatMessages).ToList();

        var response = await ChatClientHelper.ExecuteWithRetryAsync(
            async () => await ChatClient.GetResponseAsync(allMessages, chatOptions),
            _logger, 10);

        return response.GetMessage();
    }
}

