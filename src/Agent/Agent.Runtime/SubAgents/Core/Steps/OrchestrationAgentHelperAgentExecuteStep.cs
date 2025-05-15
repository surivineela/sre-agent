// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Runtime.HelperAgents;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentHelperAgentExecuteStep : OrchestrationAgentStep
{
    public required FunctionCallContent FunctionCall { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent state)
    {
        var logger = context.CreateReplaySafeLogger<OrchestrationAgentHelperAgentExecuteStep>();
        Guid threadId = state.ThreadId;

        logger.LogInternalInformation("[{ThreadId}] Executing helper agent: {FunctionCall}", threadId, FunctionCall.ToString());

        var pluginMethod = typeof(HelperAgentsPluginDefinition).GetMethod(FunctionCall.Name)
            ?? throw new InvalidOperationException($"Helper agent method {FunctionCall.Name} not found");

        var helperAgentAttr = pluginMethod.GetCustomAttribute<HelperAgentPluginAttribute>()
            ?? throw new InvalidOperationException($"Helper agent method {FunctionCall.Name} is not decorated with {nameof(HelperAgentPluginAttribute)}");

        var helperAgentInput = state.HelperAgentInputs.FirstOrDefault(h => h.GetType() == helperAgentAttr.AgentInputType)
            ?? throw new InvalidOperationException($"Helper agent input {helperAgentAttr.AgentInputType.Name} not found");

        var input = new ExecuteHelperAgentInput(
            ThreadId: threadId,
            FunctionCall: FunctionCall,
            HelperAgentInput: helperAgentInput
        );

        await state.RecordActionIfNeeded(FunctionCall, ActionStatus.InProgress);

        var result = await context.CallStartHelperAgentActivityAsync(input);

        state.ChatHistory.Add(result.ChatMessage);

        if (result.RunAsync)
        {
            state.Pending202Activities.Add(context.CallRunHelperAgentLongRunningActivityAsync(input));
            logger.LogInternalInformation("[{ThreadId}] 202 activity submitted for helper agent: {ChatMessage}",
                threadId, result.ChatMessage.ToString());
        }

        await state.RecordActionIfNeeded(FunctionCall, result.Succeeded ? ActionStatus.Completed : ActionStatus.Failed);
    }
}
