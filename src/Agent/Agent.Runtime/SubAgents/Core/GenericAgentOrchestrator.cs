// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.HelperAgents;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core;

public abstract class GenericAgentOrchestrator<TInput, TResult> : TaskOrchestrator<TInput, TResult>
{
    // The common reasoning loop logic
    protected async Task<List<ChatMessage>> RunReasoningLoopAsync(
        TaskOrchestrationContext context,
        List<ChatMessage> chatHistory,
        IReadOnlyList<string> toolSignatures,
        ILogger log,
        Guid threadId,
        IReadOnlyList<HelperAgentInput>? helperAgents = null)
    {
        var agent = new OrchestrationAgent(context, chatHistory, toolSignatures, threadId, helperAgents ?? []);
        await agent.RunReasoningLoop(this);
        return agent.ChatHistory;
    }


    internal virtual Task OnUserMessage(TaskOrchestrationContext context, List<ChatMessage> chatHistory, ChatMessage userMessage)
    {
        chatHistory.Add(userMessage);
        return Task.CompletedTask;
    }

}
