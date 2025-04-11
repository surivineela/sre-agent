// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
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
        ThreadContext threadContext,
        ILogger log)
    {
        var agent = new OrchestrationAgent(context, threadContext, chatHistory, toolSignatures);
        await agent.RunReasoningLoop(this);
        return agent.ChatHistory;
    }


    internal virtual Task OnUserMessage(TaskOrchestrationContext context, ThreadContext threadContext, List<ChatMessage> chatHistory, ChatMessage userMessage)
    {
        chatHistory.Add(userMessage);
        return Task.CompletedTask;
    }


}
