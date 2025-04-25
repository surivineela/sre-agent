using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Core.Models.Api.v1;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Threading;

namespace Agent.Runtime.SubAgents.Core.Steps;

/// <summary>
/// TODO - remove this - actions should not be tracked via separate LLM tool calls.
/// We should detect them from existing tool calls.
/// </summary>
public class OrchestrationAgentRecordActionStep : OrchestrationAgentStep
{
    public FunctionCallContent FunctionCall { get; set; }

    public override async Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent agent)
    {
        var log = context.CreateReplaySafeLogger<OrchestrationAgentRecordActionStep>();
        Guid threadId = agent.ThreadId;
        string title = string.Empty;
        ActionStatus status = ActionStatus.Pending;
        string toolName = string.Empty;

        if (FunctionCall.Arguments.TryGetValue("title", out var titleObj) && titleObj != null)
        {
            title = titleObj.ToString() ?? string.Empty;
        }

        if (FunctionCall.Arguments.TryGetValue("status", out var statusObj) && statusObj != null)
        {
            if (Enum.TryParse<ActionStatus>(statusObj.ToString(), out var parsedStatus))
            {
                status = parsedStatus;
            }
        }

        if (FunctionCall.Arguments.TryGetValue("toolName", out var toolNameObj) && toolNameObj != null)
        {
            toolName = toolNameObj.ToString() ?? string.Empty;
        }

        log.LogInformation("[{ThreadId}] Recording action with title: {Title}, status: {Status}", threadId, title, status);

        // Call the record action activity
        var action = await context.CallRecordActionActivityAsync(new RecordActionInput(
            ThreadId: threadId,
            Title: title,
            Status: status,
            ToolName: toolName
        ));
        log.LogInformation("[{ThreadId}] Action recorded: {Action}", threadId, action.ToString());

        // Return the action details as a JSON string
        var resultContent = new FunctionResultContent(
            FunctionCall.CallId,
            System.Text.Json.JsonSerializer.Serialize(action));
        agent.ChatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
    }
}
