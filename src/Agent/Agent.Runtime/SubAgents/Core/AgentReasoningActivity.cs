using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Extensions;
using Agent.Plugins;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core;

/// <summary>
/// Replacement for `GenericGetNextAction2Activity`.
/// Eventually this activity will use a ChatClient with function calling enabled.
/// At that point, the same activity is used for determining the next step, and executing the next step.
/// </summary>
[DurableTask]
public class AgentReasoningActivity : TaskActivity<GetNextActionInput, AgentReasoningResult>
{
    protected readonly IChatClient _chatClient;
    private readonly IToolsRepository _toolsRepository;

    public AgentReasoningActivity(IChatClient chatClient, IToolsRepository toolsRepository)
    {
        _chatClient = chatClient;
        _toolsRepository = toolsRepository;
    }

    public override async Task<AgentReasoningResult> RunAsync(TaskActivityContext context, GetNextActionInput input)
    {
        var reasoningResult = new AgentReasoningResult();

        var chatOptions = new ChatOptions
        {
            Tools = _toolsRepository.ResolveTools(input.ToolSignatures),
            ToolMode = ChatToolMode.RequireAny,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["AllowParallelToolCalls"] = false
            }
        };

        var allMessages = _toolsRepository.GetMCPServerInstructions().Concat(input.ChatMessages).ToList();

        var response = await _chatClient.GetResponseAsync(allMessages, chatOptions);

        reasoningResult.ChatMessages = response.Messages.ToList();
        return reasoningResult;
    }
}

public class AgentReasoningResult
{
    public List<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    public OrchestrationAgentStep Next { get; set; }
}
