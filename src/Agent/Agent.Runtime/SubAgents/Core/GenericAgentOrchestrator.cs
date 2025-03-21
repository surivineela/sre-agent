using Agent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Agent.Plugins;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Attributes;
using System.Reflection;
using FunctionCallContent = Microsoft.Extensions.AI.FunctionCallContent;
using FunctionResultContent = Microsoft.Extensions.AI.FunctionResultContent;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.SubAgents.Core;

public abstract class GenericAgentOrchestrator<TInput, TResult> : TaskOrchestrator<TInput, TResult>
{
    // The common reasoning loop logic
    protected async Task<List<ChatMessage>> RunReasoningLoopAsync(
        TaskOrchestrationContext context,
        List<ChatMessage> chatHistory,
        IReadOnlyList<string> toolSignatures,
        string threadId)
    {
        int stepCount = 0;
        bool done = false;
        Task? waitTask = null;
        CancellationTokenSource waitTokenSource = new CancellationTokenSource();

        // Begin listening for external chat messages
        var newMessageTask = context.WaitForExternalEvent<ChatMessage>("NewChatMessage");
        var pending202Activities = new List<Task<ChatMessage>>();

        while (!done)
        {
            stepCount++;

            // If there's an active wait task, then wait for it or for a new chat message
            if (waitTask is not null)
            {
                var tasksToWaitFor = new List<Task>();
                tasksToWaitFor.AddRange(pending202Activities);
                tasksToWaitFor.Add(newMessageTask);
                tasksToWaitFor.Add(waitTask);

                await Task.WhenAny(tasksToWaitFor);

                if (waitTask.IsCompleted)
                {
                    // TODO: error handling
                    await waitTask;
                    waitTask = null;
                }
                else
                {
                    waitTokenSource.Cancel();
                    waitTokenSource.Dispose();
                    waitTokenSource = new CancellationTokenSource();
                }
            }

            // Process finished 202 activities
            var notCompleted202 = new List<Task<ChatMessage>>();
            foreach (var pending202ActivityTask in pending202Activities)
            {
                if (pending202ActivityTask.IsCompleted)
                {
                    // TODO: error handling
                    chatHistory.Add(await pending202ActivityTask);
                }
                else
                {
                    notCompleted202.Add(pending202ActivityTask);
                }
            }
            pending202Activities = notCompleted202;

            // Process any external chat messages
            while (newMessageTask.IsCompleted)
            {
                // TODO: error handling
                var newMessage = await newMessageTask;
                chatHistory.Add(newMessage);
                newMessageTask = context.WaitForExternalEvent<ChatMessage>("NewChatMessage");
            }

            // Get the next action from the derived implementation
            var nextAction = await context.CallGenericGetNextAction2ActivityAsync(new GetNextActionInput
            {
                ChatMessages = chatHistory,
                StepCounter = stepCount,
                ToolSignatures = toolSignatures,
            });
            chatHistory.Add(nextAction);

            // Extract the function call (assumes a single function call in the message)
            var functionCall = nextAction.Contents.OfType<FunctionCallContent>().Single();

            // For thread specific functions, set the accurate threadId in case of LLM hallucination
            bool isThreadSpecific = IsThreadSpecificFunction(functionCall.Name, toolSignatures);
            if (isThreadSpecific && functionCall.Arguments != null)
            {
                functionCall.Arguments["threadId"] = threadId;
            }

            // Process built-in control flow function calls
            if (functionCall.Name == nameof(ControlFlowPluginDefinition.MarkPlanComplete))
            {
                done = true;
                var resultContent = new FunctionResultContent(functionCall.CallId, "Plan marked as complete.");
                chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
            }
            else if (functionCall.Name == nameof(ControlFlowPluginDefinition.Wait))
            {
                // For simplicity, using a fixed wait time (adjust as needed)
                double waitSeconds = 0.1;
                waitTask = context.CreateTimer(TimeSpan.FromSeconds(waitSeconds), waitTokenSource.Token);
                var resultContent = new FunctionResultContent(functionCall.CallId, "Wait operation submitted.");
                chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
            }
            else if (functionCall.Name == nameof(RecordActionsPluginDefinition.RecordAction))
            {
                string title = string.Empty;
                ActionStatus status = ActionStatus.Pending;

                if (functionCall.Arguments.TryGetValue("title", out var titleObj) && titleObj != null)
                {
                    title = titleObj.ToString() ?? string.Empty;
                }

                if (functionCall.Arguments.TryGetValue("status", out var statusObj) && statusObj != null)
                {
                    if (Enum.TryParse<ActionStatus>(statusObj.ToString(), out var parsedStatus))
                    {
                        status = parsedStatus;
                    }
                }

                // Call the record action activity
                var action = await context.CallRecordActionActivityAsync(new RecordActionInput(
                    ThreadId: Guid.Parse(threadId),
                    Title: title,
                    Status: status
                ));

                // Return the action details as a JSON string
                var resultContent = new FunctionResultContent(
                    functionCall.CallId,
                    System.Text.Json.JsonSerializer.Serialize(action));
                chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
            }
            else if (functionCall.Name == nameof(RecordActionsPluginDefinition.GetActionDetails))
            {
                Guid actionId = Guid.Empty;

                if (functionCall.Arguments.TryGetValue("actionId", out var actionIdObj) && actionIdObj != null)
                {
                    if (Guid.TryParse(actionIdObj.ToString(), out var parsedActionId))
                    {
                        actionId = parsedActionId;
                    }
                }

                if (actionId == Guid.Empty)
                {
                    var errorContent = new FunctionResultContent(
                        functionCall.CallId,
                        "Invalid arguments. actionId is required.");
                    chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { errorContent }));
                }
                else
                {
                    try
                    {
                        // Call the get action details activity
                        var action = await context.CallGetActionDetailsActivityAsync(new GetActionDetailsInput(
                            ThreadId: Guid.Parse(threadId),
                            ActionId: actionId
                        ));

                        // Return the action details as a JSON string
                        var resultContent = new FunctionResultContent(
                            functionCall.CallId,
                            System.Text.Json.JsonSerializer.Serialize(action));
                        chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
                    }
                    catch (Exception ex)
                    {
                        // Handle case where action is not found
                        var errorContent = new FunctionResultContent(
                            functionCall.CallId,
                            $"Error retrieving action: {ex.Message}");
                        chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { errorContent }));
                    }
                }
            }
            else if (functionCall.Name == nameof(ControlFlowPluginDefinition.NotifyUser))
            {
                // Fix: Extract message from the arguments dictionary
                string message = string.Empty;
                if (functionCall.Arguments.TryGetValue("message", out var messageObj) && messageObj != null)
                {
                    message = messageObj.ToString() ?? string.Empty;
                }

                // Call the communication activity
                await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(
                    ThreadId: threadId,
                    InstanceId: context.InstanceId,
                    Message: message
                ));

                var resultContent = new FunctionResultContent(functionCall.CallId, "User notified.");
                chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
            }
            else if (functionCall.Name == nameof(ApprovalPluginDefinition.StartApprovalFlow))
            {
                var operationName = functionCall.Arguments["operationName"]?.ToString() ?? "operation";
                var approvalInstanceId = $"approval-{context.NewGuid()}";
                var approvalInput = new ApprovalInput(context.InstanceId, operationName, threadId, approvalInstanceId);
                context.SetCustomStatus($"Pending approval:{approvalInstanceId}");
                var description = functionCall.Arguments["description"]?.ToString() ?? "Pending approval";
                context.SetCustomStatus($"Pending approval:{approvalInstanceId}");

                // Generate approval link with the new activity
                string approvalLink = await context.CallActivityAsync<string>(
                    nameof(GenerateApprovalLinkActivity),
                    (approvalInstanceId, operationName, description)
                );

                // Notify user about approval with the generated link
                await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(
                    ThreadId: threadId,
                    InstanceId: context.InstanceId,
                    Message: $"Approval required for: {operationName}. [Click here to approve]({approvalLink})"
                ));

                // Start the approval suborchestration
                waitTask = context.CallSubOrchestratorAsync(
                    new TaskName(nameof(ApprovalOrchestration)),
                    approvalInput,
                    new SubOrchestrationOptions { InstanceId = approvalInstanceId }
                );

                var resultContent = new FunctionResultContent(
                    functionCall.CallId,
                    $"Approval flow started for operation {operationName}");
                chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
            }
            else
            {

                // Create execution input, including threadId if needed
                var execInput = new ExecuteActionInput(
                    FunctionCallContent: chatHistory.Last().Contents.Single() as FunctionCallContent,
                    ToolSignatures: toolSignatures);

                var executionResult = await context.CallGenericExecuteActionActivityAsync(execInput);
                chatHistory.Add(executionResult.ChatMessage);

                if (executionResult.Is202Submit)
                {
                    pending202Activities.Add(context.CallGenericExecute202ActionActivityAsync(execInput));
                }
            }
        }

        // Notify completion when done - use explicit call to activity
        await context.CallNotifyCompletionActivityAsync(new NotifyCompletionInput(
            ThreadId: threadId,
            InstanceId: context.InstanceId,
            Status: "Completed",
            Summary: "Task completed successfully"
        ));

        return chatHistory;
    }

    // Helper method to check if a function has the ThreadSpecific attribute
    private bool IsThreadSpecificFunction(string functionName, IReadOnlyList<string> toolSignatures)
    {
        // This is a simplified implementation. In a ideal scenario, you would need to:
        // 1. Parse tool signatures to identify the class and method, so that we don't need to specify the class type below
        // 2. Use reflection to check if the method has the ThreadSpecific attribute

        Type chartPluginType = typeof(ChartPluginDefinition);
        var methodInfo = chartPluginType.GetMethods()
            .FirstOrDefault(m =>
                m.Name.Contains(functionName, StringComparison.OrdinalIgnoreCase) ||
                m.GetCustomAttribute<KernelFunctionAttribute>()?.Name == functionName);

        if (methodInfo != null)
        {
            return methodInfo.GetCustomAttribute<ThreadSpecificAttribute>() != null;
        }

        return false;
    }
}