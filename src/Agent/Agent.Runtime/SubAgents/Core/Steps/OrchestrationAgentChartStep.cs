using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;


namespace Agent.Runtime.SubAgents.Core.Steps;
public class OrchestrationAgentChartStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentChartStep>();
        Guid threadId = agent.ThreadId;
        
        var execInput = new ChartToolCallInput(
            FunctionCallContent: FunctionCall,
            threadId
            );

        var executionResult = await context.CallActivityAsync<ExecuteActionOutput>(new TaskName(nameof(ChartToolCallActivity)), execInput);
        agent.ChatHistory.Add(executionResult.ChatMessage);
    }
}
