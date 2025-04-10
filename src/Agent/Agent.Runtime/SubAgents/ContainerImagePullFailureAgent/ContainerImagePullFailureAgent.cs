using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;

[DurableTask]
public class ContainerImagePullFailureAgent : GenericAgentOrchestrator<ContainerImagePullFailureAgentInput, string>
{
    public override async Task<string> RunAsync(TaskOrchestrationContext context, ContainerImagePullFailureAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<ContainerImagePullFailureAgent>();
        log.LogInformation("Starting Container App Image Pull Failure investigation for resource: {ResourceId}",
            agentInput.ResourceId);

        // Create input for the plan activity
        var imagePullInput = new ContainerImagePullFailureInput(
            resourceId: agentInput.ResourceId
        );

        // Initial planning phase: generate plan for investigation
        List<ChatMessage> chatHistory = await context.CallContainerImagePullFailurePlanActivityAsync(imagePullInput);

        // Run the generic reasoning loop to get actions and process function calls until the plan is complete
        chatHistory = await RunReasoningLoopAsync(
            context,
            chatHistory,
            agentInput.ToolSignatures,
            agentInput.Context,
            log);

        log.LogInformation("Completed Container App Image Pull Failure investigation for resource: {ResourceId}",
            agentInput.ResourceId);
        return "success";
    }
}
