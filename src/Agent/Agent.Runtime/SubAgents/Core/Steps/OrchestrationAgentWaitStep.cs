using System;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;
using Agent.Core.Extensions;

namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentWaitStep : OrchestrationAgentStep
{
    public FunctionCallContent? FunctionCall { get; set; }

    //use this once function calling is enabled
    //public TimeSpan WaitTime { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        if (FunctionCall == null)
        {
            throw new ArgumentNullException(nameof(FunctionCall), "FunctionCall cannot be null.");
        }

        await Task.Yield();
        var log = context.CreateReplaySafeLogger<OrchestrationAgentWaitStep>();
        Guid threadId = agent.ThreadId;

        // so, the correct implementation is to grab the wait seconds argument and use that.
        // but we are still in demo mode and we dont want to actually wait 30 seconds if the model decides to do that
        // also, if we are in unit tests we dont want to wait at all
        double waitSeconds = Double.TryParse(FunctionCall.Arguments?["seconds"]?.ToString(), out double seconds) ? seconds : 1;

        bool isTestContext = AppDomain.CurrentDomain.IsTestingContext();
        if (isTestContext)
        {
            waitSeconds = 0.1;
        }

        try
        {
            // Make sure we're not using a canceled token
            if (agent.WaitTokenSource.IsCancellationRequested)
            {
                agent.WaitTokenSource = new CancellationTokenSource();
            }

            agent.WaitTask = context.CreateTimer(TimeSpan.FromSeconds(waitSeconds), agent.WaitTokenSource.Token);
            agent.WaitTimeRemaining = TimeSpan.FromSeconds(waitSeconds);
            agent.WaitTimeInitiated = context.CurrentUtcDateTime;

            string waitMessage = $"Wait initiated at {agent.WaitTimeInitiated:O} for a duration of {waitSeconds} seconds. Wait is due to complete at {agent.WaitTimeInitiated.AddSeconds(waitSeconds):O}, but might be interrupted due to system events or user messages.";

            if(isTestContext)
            {
                waitMessage = "Wait initiated.";
            }

            var resultContent = new FunctionResultContent(FunctionCall.CallId, waitMessage);
            agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
            log.LogInternalInformation("[{ThreadId}] Waiting for {WaitSeconds} seconds", threadId, waitSeconds);
        }
        catch (Exception ex)
        {
            log.LogInternalError(ex, "[{ThreadId}] Error creating wait timer", threadId);
            var errorContent = new FunctionResultContent(FunctionCall.CallId, $"Error during wait operation: {ex.Message}");
            agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { errorContent }));
        }
    }
}
