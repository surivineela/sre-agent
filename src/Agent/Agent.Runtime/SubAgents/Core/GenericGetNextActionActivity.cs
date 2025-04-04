// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class GenericGetNextAction2Activity : TaskActivity<GetNextActionInput, ChatMessage>
{
    protected readonly IChatClient ChatClient;
    private readonly ToolsRepository _toolsRepository;

    public GenericGetNextAction2Activity(IChatClient chatClient, ToolsRepository toolsRepository)
    {
        ChatClient = chatClient;
        _toolsRepository = toolsRepository;
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

        try
        {
            var response = await ChatClient.GetResponseAsync(allMessages, chatOptions);
            return response.GetMessage();
        }
        catch (Exception ex)
        {
            //Logger.LogError(ex, "Error getting next action");
            throw;
        }
    }
}

