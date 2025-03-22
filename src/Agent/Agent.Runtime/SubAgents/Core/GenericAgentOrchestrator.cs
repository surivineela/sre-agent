using Agent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Agent.Plugins;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.Logging;
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
        var logger = context.CreateReplaySafeLogger<GenericAgentOrchestrator<TInput, TResult>>();
        logger.LogInformation("Starting reasoning loop with thread ID: {ThreadId}", threadId);

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

            string jsonChatHistory = System.Text.Json.JsonSerializer.Serialize(chatHistory, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            context.SetCustomStatus(jsonChatHistory);

            logger.LogInformation("[{ThreadId}] Step {StepCount} of reasoning loop", threadId, stepCount);

            // If there's an active wait task, then wait for it or for a new chat message
            if (waitTask is not null)
            {
                logger.LogInformation("[{ThreadId}] Waiting for task to complete", threadId);

                var tasksToWaitFor = new List<Task>();
                tasksToWaitFor.AddRange(pending202Activities);
                tasksToWaitFor.Add(newMessageTask);
                tasksToWaitFor.Add(waitTask);

                await Task.WhenAny(tasksToWaitFor);
                logger.LogInformation("[{ThreadId}] Some task completed", threadId);

                if (waitTask.IsCompleted)
                {
                    // TODO: error handling
                    await waitTask;
                    waitTask = null;
                    logger.LogInformation("[{ThreadId}] waitTask completed", threadId);
                }
                else
                {
                    waitTokenSource.Cancel();
                    waitTokenSource.Dispose();
                    waitTokenSource = new CancellationTokenSource();
                    logger.LogInformation("[{ThreadId}] waitTask cancelled", threadId);
                }
            }

            // Process finished 202 activities
            var notCompleted202 = new List<Task<ChatMessage>>();
            logger.LogInformation("[{ThreadId}] Processing pending 202 activities", threadId);
            foreach (var pending202ActivityTask in pending202Activities)
            {
                if (pending202ActivityTask.IsCompleted)
                {
                    // TODO: error handling
                    var pendingTaskResult = await pending202ActivityTask;
                    chatHistory.Add(pendingTaskResult);
                    logger.LogInformation("[{ThreadId}] 202 activity completed with message: {ChatMessage}", threadId, pendingTaskResult.ToString());
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
                logger.LogInformation("[{ThreadId}] New chat message received: {ChatMessage}", threadId, newMessage.ToString());
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
            logger.LogInformation("[{ThreadId}] Next action received: {ChatMessage}", threadId, nextAction.ToString());
            chatHistory.Add(nextAction);

            var functionCalls = nextAction.Contents.OfType<FunctionCallContent>();
            logger.LogInformation("[{ThreadId}] Function calls found: {FunctionCalls}", threadId, string.Join(", ", functionCalls.Select(f => f.Name)));
            // Extract the function call (assumes a single function call in the message)
            var functionCall = functionCalls.Single();

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

                string message = string.Empty;
                if (functionCall.Arguments.TryGetValue("message", out var messageObj) && messageObj != null)
                {
                    message = messageObj.ToString() ?? string.Empty;
                }

                logger.LogInformation("[{ThreadId}] Marking plan as complete with message: {Message}", threadId, message);

                // Call the communication activity
                await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(
                    ThreadId: threadId,
                    InstanceId: context.InstanceId,
                    Message: message
                ));

                await this.OnPlanComplete(context, threadId);

                var resultContent = new FunctionResultContent(functionCall.CallId, "Plan marked as complete.");
                chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
                logger.LogInformation("[{ThreadId}] Marking plan as complete", threadId);
            }
            else if (functionCall.Name == nameof(ControlFlowPluginDefinition.Wait))
            {
                // For simplicity, using a fixed wait time (adjust as needed)
                double waitSeconds = 0.1;
                waitTask = context.CreateTimer(TimeSpan.FromSeconds(waitSeconds), waitTokenSource.Token);
                var resultContent = new FunctionResultContent(functionCall.CallId, "Wait operation submitted.");
                chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
                logger.LogInformation("[{ThreadId}] Waiting for {WaitSeconds} seconds", threadId, waitSeconds);
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
                logger.LogInformation("[{ThreadId}] Recording action with title: {Title}, status: {Status}", threadId, title, status);

                // Call the record action activity
                var action = await context.CallRecordActionActivityAsync(new RecordActionInput(
                    ThreadId: Guid.Parse(threadId),
                    Title: title,
                    Status: status
                ));
                logger.LogInformation("[{ThreadId}] Action recorded: {Action}", threadId, action.ToString());

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
                logger.LogInformation("[{ThreadId}] Getting action details for actionId: {ActionId}", threadId, actionId);

                if (actionId == Guid.Empty)
                {
                    var errorContent = new FunctionResultContent(
                        functionCall.CallId,
                        "Invalid arguments. actionId is required.");
                    chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { errorContent }));
                    logger.LogError("[{ThreadId}] Invalid actionId: {ActionId}", threadId, actionId);
                }
                else
                {
                    try
                    {
                        logger.LogInformation("[{ThreadId}] Retrieving action details for actionId: {ActionId}", threadId, actionId);
                        // Call the get action details activity
                        var action = await context.CallGetActionDetailsActivityAsync(new GetActionDetailsInput(
                            ThreadId: Guid.Parse(threadId),
                            ActionId: actionId
                        ));
                        logger.LogInformation("[{ThreadId}] Action details retrieved: {Action}", threadId, action.ToString());

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
                        logger.LogError("[{ThreadId}] Error retrieving action details: {Error}", threadId, ex.Message);
                    }
                }
            }
            else if (functionCall.Name == nameof(ControlFlowPluginDefinition.NotifyUser))
            {
                logger.LogInformation("[{ThreadId}] Notifying user", threadId);
                // Fix: Extract message from the arguments dictionary
                string message = string.Empty;
                if (functionCall.Arguments.TryGetValue("message", out var messageObj) && messageObj != null)
                {
                    message = messageObj.ToString() ?? string.Empty;
                }
                logger.LogInformation("[{ThreadId}] Message to notify user: {Message}", threadId, message);

                // Call the communication activity
                await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(
                    ThreadId: threadId,
                    InstanceId: context.InstanceId,
                    Message: message
                ));
                logger.LogInformation("[{ThreadId}] User notified with message: {Message}", threadId, message);

                var resultContent = new FunctionResultContent(functionCall.CallId, "User notified.");
                chatHistory.Add(new ChatMessage(ChatRole.Tool, new[] { resultContent }));
            }
            else if (functionCall.Name == nameof(ApprovalPluginDefinition.StartApprovalFlow))
            {
                logger.LogInformation("[{ThreadId}] Starting approval flow", threadId);

                var operationName = functionCall.Arguments["operationName"]?.ToString() ?? "operation";
                var approvalInstanceId = $"approval-{context.NewGuid()}";
                var approvalInput = new ApprovalInput(context.InstanceId, operationName, threadId, approvalInstanceId);
                context.SetCustomStatus($"Pending approval:{approvalInstanceId}");
                var description = functionCall.Arguments["description"]?.ToString() ?? "Pending approval";
                context.SetCustomStatus($"Pending approval:{approvalInstanceId}");

                logger.LogInformation("[{ThreadId}] Trying to generate approvalLink for operation: {OperationName} approval instanceId: {approvalInstanceId}", threadId, operationName, approvalInstanceId);

                // Generate approval link with the new activity
                string approvalLink = await context.CallActivityAsync<string>(
                    nameof(GenerateApprovalLinkActivity),
                    (approvalInstanceId, operationName, description)
                );
                logger.LogInformation("[{ThreadId}] Approval link generated: {ApprovalLink} for {approvalInstanceId}. Trying to notify user.", threadId, approvalLink, approvalInstanceId);

                // Notify user about approval with the generated link
                await context.CallUpdateThreadWithAgentMessageActivityAsync(new UpdateThreadWithAgentMessageInput(
                    ThreadId: threadId,
                    InstanceId: context.InstanceId,
                    Message: $"Approval required for: {operationName}. [Click here to approve]({approvalLink})"
                ));
                logger.LogInformation("[{ThreadId}] User notified about approval with link: {ApprovalLink}. Trying to start ApprovalOrchestration", threadId, approvalLink);

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
                logger.LogInformation("[{ThreadId}] Get other Function call: {FunctionCall}", threadId, functionCall.ToString());
                // For any other function call, defer to the derived implementation
                var execInput = new ExecuteActionInput(
                    FunctionCallContent: chatHistory.Last().Contents.Single() as FunctionCallContent,
                    ToolSignatures: toolSignatures);
                var executionResult = await context.CallGenericExecuteActionActivityAsync(execInput);
                chatHistory.Add(executionResult.ChatMessage);

                if (executionResult.Is202Submit)
                {
                    pending202Activities.Add(context.CallGenericExecute202ActionActivityAsync(execInput));
                    logger.LogInformation("[{ThreadId}] 202 activity submitted: {ChatMessage}", threadId, executionResult.ChatMessage.ToString());
                }
            }
        }

        logger.LogInformation("[{ThreadId}] Reasoning loop completed. Notifying user", threadId);
        // Notify completion when done - use explicit call to activity
        await context.CallNotifyCompletionActivityAsync(new NotifyCompletionInput(
            ThreadId: threadId,
            InstanceId: context.InstanceId,
            Status: "Completed",
            Summary: "Task completed successfully"
        ));
        logger.LogInformation("[{ThreadId}] Completion notification sent", threadId);

        return chatHistory;
    }

    protected virtual Task OnPlanComplete(TaskOrchestrationContext context, string threadId)
    {
        return Task.CompletedTask;
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