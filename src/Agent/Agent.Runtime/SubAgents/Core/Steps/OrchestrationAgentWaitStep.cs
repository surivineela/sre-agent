using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;


namespace Agent.Runtime.SubAgents.Core.Steps;

public class OrchestrationAgentWaitStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; }

    //use this once function calling is enabled
    //public TimeSpan WaitTime { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentWaitStep>();
        Guid threadId = agent.ThreadContext.ThreadId;

        // so, the correct implementation is to grab the wait seconds argument and use that.
        // but we are still in demo mode and we dont want to actually wait 30 seconds if the model decides to do that
        // also, if we are in unit tests we dont want to wait at all
        double waitSeconds = 7;
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)))
        {
            waitSeconds = 0.1;
        }

        agent.WaitTask = context.CreateTimer(TimeSpan.FromSeconds(waitSeconds), agent.WaitTokenSource.Token);
        var resultContent = new FunctionResultContent(FunctionCall.CallId, "Wait operation submitted.");
        agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
        log.LogInformation("[{ThreadId}] Waiting for {WaitSeconds} seconds", threadId, waitSeconds);
    }
}
