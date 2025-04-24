using System.Reflection;
using System.Text.Json.Serialization;
using Agent.Core.Attributes;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Attributes;
using Agent.Plugins.Definitions;
using Agent.Runtime.SubAgents.Core.Steps;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;


namespace Agent.Runtime.SubAgents.Core;
public class OrchestrationAgent
{
    private readonly TaskOrchestrationContext _taskOrchestrationContext;
    public List<ChatMessage> ChatHistory { get; private set; }
    public IReadOnlyList<string> ToolSignatures { get; private set; }
    public CancellationTokenSource WaitTokenSource { get; set; } = new CancellationTokenSource();
    public Task? WaitTask { get; set; }
    public bool Done { get; set; } = false;
    public bool ResponseFromUserIsPending { get; set; } = false;
    public List<Task<ChatMessage>> Pending202Activities { get; set; } = new();
    public ApprovalStatus? ApprovalStatus { get; set; } = null;
    public Guid ThreadId { get; set; }

    public Task<ChatMessage> _newMessageTask;
    private ILogger log;

    public int StepCount { get; set; } = 0;

    public OrchestrationAgent(
        TaskOrchestrationContext taskOrchestrationContext,
        List<ChatMessage> initialContext,
        IReadOnlyList<string> toolSignatures,
        Guid threadId)
    {
        _taskOrchestrationContext = taskOrchestrationContext;
        this.ChatHistory = initialContext;
        this.ToolSignatures = toolSignatures;
        this.ThreadId = threadId;

        log = taskOrchestrationContext.CreateReplaySafeLogger<OrchestrationAgent>();
        _newMessageTask = _taskOrchestrationContext.WaitForExternalEvent<ChatMessage>("NewChatMessage");
    }

    // we can remove the generic args once we have derived classes inherit from this rather than generic agent orchestrator
    public async Task RunReasoningLoop<TInput, TResult>(GenericAgentOrchestrator<TInput, TResult> genericAgentOrchestrator)
    {
        log.LogInformation("Starting reasoning loop with thread ID: {ThreadId}", this.ThreadId);

        while (!Done)
        {
            StepCount += 1;
            log.LogInformation("[{ThreadId}] Step {StepCount} of reasoning loop", this.ThreadId, StepCount);

            UpdateOrchestrationStatus();
            await WaitIfNecessary();
            await Process202Activities();
            await ProcessNewMessages(genericAgentOrchestrator);
            await DoReasoningStep();
        }

        await ProcessCompletion();
    }

    public async Task DoReasoningStep()
    {
        string threadId = this.ThreadId.ToString();

        // Get the next action from the derived implementation
        var reasoningResult = await _taskOrchestrationContext.CallActivityAsync<AgentReasoningResult>(new TaskName(nameof(AgentReasoningActivity)), new GetNextActionInput
        {
            ChatMessages = this.ChatHistory,
            StepCounter = this.StepCount,
            ToolSignatures = this.ToolSignatures,
        });

        log.LogInformation("[{ThreadId}] Next action received: {ChatMessage}", threadId, reasoningResult.ToString());

        var functionCalls = reasoningResult.ChatMessages.Last().Contents.OfType<FunctionCallContent>();
        log.LogInformation("[{ThreadId}] Function calls found: {FunctionCalls}", threadId, string.Join(", ", functionCalls.Select(f => f.Name)));
        // Extract the function call (assumes a single function call in the message)
        var functionCall = functionCalls.Single();

        bool requiresApproval = await _taskOrchestrationContext.CallCheckRequiresApprovalActivityAsync((ToolSignatures, functionCall.Name));
        if (requiresApproval)
        {
            if (WaitTask != null && WaitTask is Task<ApprovalStatus> approvalTask)
            {
                if (approvalTask.IsCompleted)
                {
                    ApprovalStatus = await approvalTask;
                    WaitTask = null;
                    log.LogInformation("[{ThreadId}] Approval status retrieved: {ApprovalStatus}", threadId, ApprovalStatus.ToString());
                }
                else
                {
                    // Move to the next iteration if the approval task is not completed
                    return;
                }
            }

            // Check if the approval status is approved. If yes, the function call will continue; otherwise, it will be redirected to the approval service tool
            if (ApprovalStatus == null || !ApprovalStatus.IsApproved)
            {
                log.LogInformation("[{ThreadId}] function call to {FunctionCall} requires approval. Intercepted this call and redirected to the approval service.", threadId, functionCall.Name);

                // replace the function call to the approval service call
                string callId = functionCall.CallId;    // use the original callId for convenience
                var approvalFunctionCall = new FunctionCallContent(callId, nameof(ApprovalPluginDefinition.StartApprovalFlow), new Dictionary<string, object?>
                {
                    { "operationName", functionCall.Name },
                    { "description", "Generated approval request of " + functionCall.Name }
                });
                this.ChatHistory.Add(new ChatMessage(ChatRole.Assistant, [approvalFunctionCall]));
                functionCall = approvalFunctionCall;
            }
            else
            {
                log.LogInformation("[{ThreadId}] function call to {FunctionCall} is approved. Proceeding with the function call.", threadId, functionCall.Name);
                this.ChatHistory.AddRange(reasoningResult.ChatMessages);
            }
        }
        else
        {
            this.ChatHistory.AddRange(reasoningResult.ChatMessages);
        }

        if (!string.IsNullOrEmpty(ApprovalStatus?.OboToken))
        {
            if (functionCall.Arguments != null)
            {
                functionCall.Arguments["oboToken"] = ApprovalStatus.OboToken;
            }
        }

        // For thread specific functions, set the accurate threadId in case of LLM hallucination
        bool isThreadSpecific = IsThreadSpecificFunction(functionCall.Name, this.ToolSignatures);
        if (isThreadSpecific && functionCall.Arguments != null)
        {
            functionCall.Arguments["threadId"] = threadId;
        }

        var step = OrchestrationAgentStep.CreateStep(functionCall);
        await step.ExecuteAsync(_taskOrchestrationContext, this);
    }

    public void UpdateOrchestrationStatus()
    {
        string jsonChatHistory = System.Text.Json.JsonSerializer.Serialize(this.ChatHistory, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        _taskOrchestrationContext.SetCustomStatus(jsonChatHistory);
    }

    public async Task WaitIfNecessary()
    {
        OrchestrationAgent agent = this;
        string threadId = this.ThreadId.ToString();

        // If there's an active wait task, the agent is not driving the task forward.
        // However they still need to be responsive to user questions. Answering these questions might take multiple conversation turns (because of tool calls).
        // So in that case, we don't want to block on the pending wait task.
        if (agent.WaitTask is not null || agent.ResponseFromUserIsPending == true)
        {
            log.LogInformation("[{ThreadId}] Waiting for task to complete", threadId);

            var tasksToWaitFor = new List<Task>();
            tasksToWaitFor.AddRange(agent.Pending202Activities);
            tasksToWaitFor.Add(_newMessageTask);

            if (agent.WaitTask != null)
            {
                tasksToWaitFor.Add(agent.WaitTask);
            }

            var task = await Task.WhenAny(tasksToWaitFor);
            log.LogInformation("[{ThreadId}] Some task completed", threadId);

            if (agent.WaitTask != null && agent.WaitTask.IsCompleted)
            {
                try
                {
                    // Handle the task result
                    if (agent.WaitTask is Task<ApprovalStatus>)
                    {
                        ApprovalStatus = await (Task<ApprovalStatus>)agent.WaitTask;
                    }
                    else
                    {
                        await agent.WaitTask;
                    }
                    log.LogInformation("[{ThreadId}] waitTask completed", threadId);
                }
                catch (TaskCanceledException)
                {
                    log.LogInformation("[{ThreadId}] waitTask was canceled", threadId);
                    // Task cancellation is expected when tokens are canceled, so we don't need to propagate this exception
                }
                catch (OperationCanceledException)
                {
                    log.LogInformation("[{ThreadId}] waitTask operation was canceled", threadId);
                    // Operation cancellation is expected when tokens are canceled, so we don't need to propagate this exception
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "[{ThreadId}] Error awaiting waitTask", threadId);
                    // Consider whether to rethrow or handle other exceptions differently
                }
                finally
                {
                    agent.WaitTask = null;
                }
            }
            else
            {
                agent.WaitTokenSource.Cancel();
                agent.WaitTokenSource.Dispose();
                agent.WaitTokenSource = new CancellationTokenSource();
                log.LogInformation("[{ThreadId}] waitTask cancelled", threadId);
            }
        }
    }

    public async Task Process202Activities()
    {
        string threadId = this.ThreadId.ToString();

        // Process finished 202 activities
        var notCompleted202 = new List<Task<ChatMessage>>();
        log.LogInformation("[{ThreadId}] Processing pending 202 activities", threadId);
        foreach (var pending202ActivityTask in this.Pending202Activities)
        {
            if (pending202ActivityTask.IsCompleted)
            {
                // TODO: error handling
                var pendingTaskResult = await pending202ActivityTask;
                this.ChatHistory.Add(pendingTaskResult);
                log.LogInformation("[{ThreadId}] 202 activity completed with message: {ChatMessage}", threadId, pendingTaskResult.ToString());
            }
            else
            {
                notCompleted202.Add(pending202ActivityTask);
            }
        }

        this.Pending202Activities = notCompleted202;
    }

    public async Task ProcessNewMessages<TInput, TResult>(GenericAgentOrchestrator<TInput, TResult> genericAgentOrchestrator)
    {
        // Process any external chat messages
        while (_newMessageTask.IsCompleted)
        {
            // TODO: error handling
            var newMessage = await _newMessageTask;
            log.LogInformation("[{ThreadId}] New chat message received: {ChatMessage}", this.ThreadId, newMessage.ToString());

            // this is hacky - need to decide whether to move customized behavior into derived types of OrchestrationAgent or keep them on the orchestrator
            await genericAgentOrchestrator.OnUserMessage(_taskOrchestrationContext, this.ChatHistory, newMessage);

            _newMessageTask = _taskOrchestrationContext.WaitForExternalEvent<ChatMessage>("NewChatMessage");

            // The user sent us a message
            this.ResponseFromUserIsPending = false;
        }
    }

    public async Task ProcessCompletion()
    {
        string threadId = this.ThreadId.ToString();

        log.LogInformation("[{ThreadId}] Reasoning loop completed. Notifying user", threadId);
        // Notify completion when done - use explicit call to activity
        await _taskOrchestrationContext.CallNotifyCompletionActivityAsync(new NotifyCompletionInput(
            ThreadId: threadId,
            InstanceId: _taskOrchestrationContext.InstanceId,
            Status: "Completed",
            Summary: "Task completed successfully"
        ));
        log.LogInformation("[{ThreadId}] Completion notification sent", threadId);
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
                m.GetCustomAttribute<Microsoft.SemanticKernel.KernelFunctionAttribute>()?.Name == functionName);

        if (methodInfo != null)
        {
            return methodInfo.GetCustomAttribute<ThreadSpecificAttribute>() != null;
        }

        return false;
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(OrchestrationAgentCompleteStep), "CompleteStep")]
[JsonDerivedType(typeof(OrchestrationAgentWaitStep), "WaitStep")]
[JsonDerivedType(typeof(OrchestrationAgentRecordActionStep), "RecordActionStep")]
[JsonDerivedType(typeof(OrchestrationAgentGetActionDetailsStep), "GetActionDetailsStep")]
[JsonDerivedType(typeof(OrchestrationAgentUserCommunicationStep), "UserCommunicationStep")]
[JsonDerivedType(typeof(OrchestrationAgentStartApprovalStep), "StartApprovalStep")]
[JsonDerivedType(typeof(OrchestrationAgentVisualizeAppComponentsStep), "VisualizeAppComponentsStep")]
[JsonDerivedType(typeof(OrchestrationAgentVisualizeAKSMicroserviceTopologyStep), "VisualizeAKSMicroserviceTopologyStep")]
[JsonDerivedType(typeof(OrchestrationAgentGenericExecuteStep), "GenericExecuteStep")]
public abstract class OrchestrationAgentStep
{
    public abstract Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent state);

    public static OrchestrationAgentStep CreateStep(FunctionCallContent functionCall)
    {
        if (functionCall.Name == nameof(ControlFlowPluginDefinition.MarkPlanComplete))
        {
            return new OrchestrationAgentCompleteStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(ControlFlowPluginDefinition.Wait))
        {
            return new OrchestrationAgentWaitStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(RecordActionsPluginDefinition.RecordAction))
        {
            return new OrchestrationAgentRecordActionStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(RecordActionsPluginDefinition.GetActionDetails))
        {
            return new OrchestrationAgentGetActionDetailsStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(ControlFlowPluginDefinition.NotifyUser) || functionCall.Name == nameof(ControlFlowPluginDefinition.AskUserForInput))
        {
            return new OrchestrationAgentUserCommunicationStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(ApprovalPluginDefinition.StartApprovalFlow))
        {
            return new OrchestrationAgentStartApprovalStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(GraphDBPluginDefinition.VisualizeApplicationComponents))
        {
            return new OrchestrationAgentVisualizeAppComponentsStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(GraphDBPluginDefinition.VisualizeAKSMicroserviceTopology))
        {
            return new OrchestrationAgentVisualizeAKSMicroserviceTopologyStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(ChartPluginDefinition.PlotBarChartAsync) ||
                 functionCall.Name == nameof(ChartPluginDefinition.PlotPieChartAsync) ||
                 functionCall.Name == nameof(ChartPluginDefinition.PlotScatterAsync) ||
                 functionCall.Name == nameof(ChartPluginDefinition.PlotTimeSeriesDataAsync))
        {
            return new OrchestrationAgentChartStep { FunctionCall = functionCall };
        }
        else
        {
            return new OrchestrationAgentGenericExecuteStep { FunctionCall = functionCall };
        }
    }
}
