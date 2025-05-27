using System.Text.Json.Serialization;
using Agent.Core.Extensions;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.HelperAgents;
using Agent.Runtime.SubAgents.Core.Steps;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ActionStatus = Agent.Core.Models.Api.v1.ActionStatus;

namespace Agent.Runtime.SubAgents.Core;
public class OrchestrationAgent
{
    private readonly TaskOrchestrationContext _taskOrchestrationContext;
    public List<ChatMessage> ChatHistory { get; private set; }
    public IReadOnlyList<string> ToolSignatures { get; private set; }
    public CancellationTokenSource WaitTokenSource { get; set; } = new CancellationTokenSource();
    public Task? WaitTask { get; set; }
    public DateTime WaitTimeInitiated { get; set; }
    public TimeSpan WaitTimeRemaining { get; set; }
    public bool Done { get; set; } = false;
    public bool ResponseFromUserIsPending { get; set; } = false;
    public Dictionary<Guid, PendingApprovalData> PendingApprovals { get; set; } = [];
    public List<Task<ChatMessage>> Pending202Activities { get; set; } = [];
    public Guid ThreadId { get; set; }
    public Guid CurrentActionId { get; set; }
    public IReadOnlyList<HelperAgentInput> HelperAgentInputs { get; set; }
    ThreadContext? ThreadContext { get; set; }

    public Task<ChatMessage> _newMessageTask;
    public Task<ApprovalStatus> _approvalTask;
    private ILogger log;

    public int StepCount { get; set; } = 0;

    public OrchestrationAgent(
        TaskOrchestrationContext taskOrchestrationContext,
        List<ChatMessage> initialContext,
        IReadOnlyList<string> toolSignatures,
        Guid threadId,
        IReadOnlyList<HelperAgentInput> helperAgentsInputs)
    {
        _taskOrchestrationContext = taskOrchestrationContext;
        this.ChatHistory = initialContext;
        this.ToolSignatures = toolSignatures;
        this.ThreadId = threadId;
        this.HelperAgentInputs = helperAgentsInputs;

        log = taskOrchestrationContext.CreateReplaySafeLogger<OrchestrationAgent>();
        _newMessageTask = _taskOrchestrationContext.WaitForExternalEvent<ChatMessage>("NewChatMessage");
        _approvalTask = _taskOrchestrationContext.WaitForExternalEvent<ApprovalStatus>("ApprovalEvent");
    }

    // we can remove the generic args once we have derived classes inherit from this rather than generic agent orchestrator
    public async Task RunReasoningLoop<TInput, TResult>(GenericAgentOrchestrator<TInput, TResult> genericAgentOrchestrator)
    {
        log.LogInternalInformation("Starting reasoning loop with thread ID: {ThreadId}", this.ThreadId);
        while (!Done)
        {
            StepCount += 1;
            log.LogInternalInformation("[{ThreadId}] Step {StepCount} of reasoning loop", this.ThreadId, StepCount);

            UpdateOrchestrationStatus();

            await WaitIfNecessary();
            await Process202Activities();
            await ProcessNewMessages(genericAgentOrchestrator);
            await ProcessNewApproval();
            await DoReasoningStep();
        }

        await ProcessCompletion();
    }

    public async Task DoReasoningStep()
    {
        string threadId = this.ThreadId.ToString();
        await RecordStateChange(ReasoningState.PlanningNextAction, "Calling LLM for next action planning");


        // Get the next action from the derived implementation
        var reasoningResult = await _taskOrchestrationContext.CallActivityAsync<AgentReasoningResult>(new TaskName(nameof(AgentReasoningActivity)), new GetNextActionInput
        {
            ChatMessages = this.ChatHistory,
            StepCounter = this.StepCount,
            ToolSignatures = this.ToolSignatures
        },
        // There's potential throttling for OpenAI calls, use retry policy to avoid.
        new TaskOptions(new TaskRetryOptions(new RetryPolicy(10, TimeSpan.FromSeconds(1), backoffCoefficient: 1.5f, maxRetryInterval: TimeSpan.FromSeconds(10)))));

        log.LogInternalInformation("[{ThreadId}] Next action received: {ChatMessage}", threadId, reasoningResult.ToString());

        var functionCalls = reasoningResult.ChatMessages.Last().Contents.OfType<FunctionCallContent>();

        log.LogInternalInformation("[{ThreadId}] Function calls found: {FunctionCalls}", threadId, string.Join(", ", functionCalls.Select(f => f.Name)));
        // Extract the function call (assumes a single function call in the message)
        var functionCall = functionCalls.Single();

        await RecordActionIfNeeded(functionCall, ActionStatus.Pending);

        // Check for write operations and add dryrun=true parameter
        var WriteActionActivityInput = new WriteActionActivityInput()
        {
            ToolSignatures = ToolSignatures,
            FunctionCall = functionCall,
            ThreadId = threadId,
            OrchestrationId = _taskOrchestrationContext.InstanceId,
            ActionId = CurrentActionId,
        };

        await RecordStateChange(ReasoningState.RunningFunctionCall, $"Checking if function is a write operation: {functionCall.Name}");

        var Result = await _taskOrchestrationContext.CallActivityAsync<WriteActionActivityOutput>(
            new TaskName(nameof(WriteActionActivity)),
            WriteActionActivityInput);

        if (Result.NeedSkip)
        {
            log.LogInternalInformation("[{ThreadId}] Function {FunctionName} is write action and need to skip.", threadId, functionCall.Name);
            ChatHistory.Add(new ChatMessage(ChatRole.System, Result.Prompt));
            return;
        }

        if (Result.ModifiedFunctionCall != null && Result.IsWriteAction)
        {
            log.LogInternalInformation("[{ThreadId}] Function {FunctionName} is write action and need run in read-only mode.", threadId, functionCall.Name);
            ChatHistory.Add(new ChatMessage(ChatRole.System, Result.Prompt));
            functionCall = Result.ModifiedFunctionCall;
        }

        var checkApprovalActivityInput = new CheckApprovalActivityInput()
        {
            ToolSignatures = ToolSignatures,
            FunctionCall = functionCall,
            ThreadId = threadId,
            OrchestrationId = _taskOrchestrationContext.InstanceId,
            ActionId = CurrentActionId,
        };

        await RecordStateChange(ReasoningState.RunningFunctionCall, $"Checking approval for function call: {functionCall.Name}");

        var approvalResult = await _taskOrchestrationContext.CallCheckApprovalActivityAsync(checkApprovalActivityInput);
        log.LogInternalInformation("[{ThreadId}] Approval status is: {ApprovalStatus}", threadId, approvalResult.ApprovalStatus);

        if (approvalResult.ApprovalStatus == ToolApprovalStatus.NotRequired
            || approvalResult.ApprovalStatus == ToolApprovalStatus.AutoApproved)
        {
            ChatHistory.AddRange(reasoningResult.ChatMessages);
        }
        else
        {
            // tool call chat history will be added once the approval request is approved or denied
            if (approvalResult.ApprovalStatus == ToolApprovalStatus.Pending && approvalResult.ApprovalId != null)
            {
                // add pending approval for tracking
                PendingApprovals.Add(approvalResult.ApprovalId.Value, new(approvalResult.ApprovalId.Value, reasoningResult.ChatMessages, functionCall));
            }

            // defer execution to ProcessNewApproval
            functionCall = null;
        }

        if (functionCall == null)
        {
            return;
        }

        await RecordStateChange(ReasoningState.RunningFunctionCall, $"Running function call: {functionCall.Name}");

        var step = OrchestrationAgentStep.CreateStep(functionCall, approvalResult.ApprovalId);

        await step.ExecuteAsync(_taskOrchestrationContext, this);
    }

    public async Task RecordStateChange(ReasoningState state, string message)
    {
        this.ThreadContext = await _taskOrchestrationContext.CallActivityAsync<ThreadContext>(
            new TaskName(nameof(PersistThreadContextActivity)),
            new PersistThreadContextInput
            {
                OrchestrationInstanceId = _taskOrchestrationContext.InstanceId,
                ThreadContext = this.ThreadContext,
                StepCounter = this.StepCount,
                ThreadId = this.ThreadId,
                ReasoningState = state,
                StateMessage = message,
                TimeStamp = DateTime.UtcNow
            });
    }

    public async Task RecordActionIfNeeded(FunctionCallContent? functionCall, ActionStatus status)
    {
        var action = await _taskOrchestrationContext.CallRecordActionActivityAsync(
            new RecordActionInput(
                ActionId: this.CurrentActionId,
                ThreadId: this.ThreadId,
                ChatMessages: this.ChatHistory,
                FunctionCall: functionCall,
                ToolSignatures: this.ToolSignatures,
                Status: status
                )
        );

        if (action != null)
        {
            if (action.Status == ActionStatus.Completed || action.Status == ActionStatus.Failed)
            {
                this.CurrentActionId = Guid.Empty;
                this.ChatHistory.Add(new ChatMessage(ChatRole.Assistant, $"Action has been taken at {DateTime.UtcNow:O}. Action Id: {action.Id}"));
            }
            else
            {
                this.CurrentActionId = action.Id;
            }
        }
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

        // We don't let tests do a full wait, and we don't have the necessary manual clock adjustments to make the timestamps line up, so change the wait messages when testing to be less specific.
        bool isTestContext = AppDomain.CurrentDomain.IsTestingContext();

        // If there's an active wait task, the agent is not driving the task forward.
        // However they still need to be responsive to user questions. Answering these questions might take multiple conversation turns (because of tool calls).
        // So in that case, we don't want to block on the pending wait task.
        if (agent.WaitTask is not null || agent.ResponseFromUserIsPending == true || agent.PendingApprovals.Count > 0)
        {
            log.LogInternalInformation("[{ThreadId}] Waiting for task to complete. ResponseFromUserIsPending={ResponseFromUserIsPending}, PendingApprovals={PendingApprovalToolCalls}", threadId, ResponseFromUserIsPending, agent.PendingApprovals.Count);
            string stateMessage = ResponseFromUserIsPending ? "Waiting for user's response" : (agent.PendingApprovals.Count > 0 ? $"Waiting for {agent.PendingApprovals.Count} user approvals" : "Waiting for task to complete");

            await RecordStateChange(ReasoningState.Waiting, stateMessage);

            var tasksToWaitFor = new List<Task>();
            tasksToWaitFor.AddRange(agent.Pending202Activities);
            tasksToWaitFor.Add(_newMessageTask);
            tasksToWaitFor.Add(_approvalTask);

            if (agent.WaitTask != null)
            {
                tasksToWaitFor.Add(agent.WaitTask);
            }

            var task = await Task.WhenAny(tasksToWaitFor);
            log.LogInternalInformation("[{ThreadId}] Some task completed", threadId);

            if (agent.WaitTask != null && agent.WaitTask.IsCompleted)
            {
                try
                {
                    await agent.WaitTask;
                    log.LogInternalInformation("[{ThreadId}] waitTask completed", threadId);

                    if (agent.WaitTask.IsCompletedSuccessfully)
                    {
                        var waitMessage = $"Wait completed at {agent._taskOrchestrationContext.CurrentUtcDateTime:O}.";

                        if (isTestContext)
                        {
                            waitMessage = "Wait completed.";
                        }

                        agent.ChatHistory.Add(new ChatMessage(ChatRole.System, waitMessage));
                    }
                }
                catch (TaskCanceledException)
                {
                    log.LogInternalInformation("[{ThreadId}] waitTask was canceled", threadId);
                    // Task cancellation is expected when tokens are canceled, so we don't need to propagate this exception
                }
                catch (OperationCanceledException)
                {
                    log.LogInternalInformation("[{ThreadId}] waitTask operation was canceled", threadId);
                    // Operation cancellation is expected when tokens are canceled, so we don't need to propagate this exception
                }
                catch (Exception ex)
                {
                    log.LogInternalError(ex, "[{ThreadId}] Error awaiting waitTask", threadId);
                    // Consider whether to rethrow or handle other exceptions differently
                }
                finally
                {
                    agent.WaitTask = null;
                }
            }
            else
            {
                if (agent.WaitTask != null)
                {
                    DateTime currentTime = agent._taskOrchestrationContext.CurrentUtcDateTime;
                    TimeSpan timeWaited = currentTime - agent.WaitTimeInitiated;
                    double timeRemaining = (agent.WaitTimeRemaining - timeWaited).TotalSeconds;

                    string interruptMessage = @$"Wait was interrupted at {currentTime:O}, this was {timeRemaining} seconds earlier than when the wait was scheduled to finish.
                        If the reason of interruption was a user message, respond appropriately to the user depending on the scenario:
                        Scenario 1: If the user wants to cancel the wait/task entirely, then honor the user's request and cancel the wait without resuming the remainder of the wait that is left. Provide an update to the user saying that their request has been executed.
                        Scenario 2: If the user does not want to cancel the wait/task entirely, but has still entered a user message, AND there still remains a duration of time to wait, then respond appropriately to the user. Add the following to your response: ""I will provide an update after the remaining duration of time being {timeRemaining} seconds"". After you provide this response, resume the wait task with the time of {timeRemaining} seconds

                        Wait can be interrupted due to various other system events such as the following examples but not limited to: approvals, background operations, etc. For these scenarios, resume the wait task with the time of {timeRemaining} seconds.";

                    if (isTestContext)
                    {
                        interruptMessage = "Wait was interrupted.";
                    }

                    agent.ChatHistory.Add(new ChatMessage(ChatRole.System, interruptMessage));
                }

                agent.WaitTokenSource.Cancel();
                agent.WaitTokenSource.Dispose();
                agent.WaitTokenSource = new CancellationTokenSource();
                log.LogInternalInformation("[{ThreadId}] waitTask cancelled", threadId);
            }
        }
    }

    public async Task Process202Activities()
    {
        string threadId = this.ThreadId.ToString();

        // Process finished 202 activities
        var notCompleted202 = new List<Task<ChatMessage>>();
        log.LogInternalInformation("[{ThreadId}] Processing pending 202 activities", threadId);
        foreach (var pending202ActivityTask in this.Pending202Activities)
        {
            if (pending202ActivityTask.IsCompleted)
            {
                // TODO: error handling
                var pendingTaskResult = await pending202ActivityTask;
                this.ChatHistory.Add(pendingTaskResult);
                log.LogInternalInformation("[{ThreadId}] 202 activity completed with message: {ChatMessage}", threadId, pendingTaskResult.ToString());
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
            log.LogInternalInformation("[{ThreadId}] New chat message received: {ChatMessage}", this.ThreadId, newMessage.ToString());

            // this is hacky - need to decide whether to move customized behavior into derived types of OrchestrationAgent or keep them on the orchestrator
            await genericAgentOrchestrator.OnUserMessage(_taskOrchestrationContext, this.ChatHistory, newMessage);

            _newMessageTask = _taskOrchestrationContext.WaitForExternalEvent<ChatMessage>("NewChatMessage");

            // The user sent us a message
            this.ResponseFromUserIsPending = false;
        }
    }

    public async Task ProcessNewApproval()
    {
        while (_approvalTask.IsCompleted)
        {
            // TODO: error handling
            var approvalEvent = await _approvalTask;
            PendingApprovalData? approvalData = null;

            if (!string.IsNullOrEmpty(approvalEvent.OperationId) && Guid.TryParse(approvalEvent.OperationId, out var approvalId))
            {
                PendingApprovals.Remove(approvalId, out approvalData);
            }

            if (approvalEvent.Status == ApprovalDecision.Approved)
            {
                var approvalString = $"Approval by **{approvalEvent.DecisionMaker}** received at {approvalEvent.ApprovedTime}";
                log.LogInternalInformation(approvalString);

                ChatHistory.Add(new ChatMessage(ChatRole.System, approvalString));

                if (approvalData != null)
                {
                    // execute the approved tool

                    ChatHistory.AddRange(approvalData.OriginalMessages);

                    await RecordActionIfNeeded(approvalData.FunctionCall, ActionStatus.Approved);

                    await RecordStateChange(ReasoningState.RunningFunctionCall, $"Running function call: {approvalData.FunctionCall.Name}");

                    var step = OrchestrationAgentStep.CreateStep(approvalData.FunctionCall, approvalData.ApprovalId);

                    await step.ExecuteAsync(_taskOrchestrationContext, this);
                }
            }
            else
            {
                var rejectionString = $"Operation was not approved. Rejected by **{approvalEvent.DecisionMaker}** at {approvalEvent.ApprovedTime}";
                log.LogInternalInformation(rejectionString);

                ChatHistory.Add(new ChatMessage(ChatRole.System, rejectionString));

                if (approvalData != null)
                {
                    ChatHistory.AddRange(approvalData.OriginalMessages);

                    await RecordActionIfNeeded(approvalData.FunctionCall, ActionStatus.Rejected);

                    var callResult = new FunctionResultContent(
                            approvalData.FunctionCall.CallId,
                       $"User rejected the action {approvalData.FunctionCall.Name}");

                    ChatHistory.Add(new ChatMessage(ChatRole.Tool, [callResult]));
                }
            }

            _approvalTask = _taskOrchestrationContext.WaitForExternalEvent<ApprovalStatus>("ApprovalEvent");
        }
    }

    public async Task ProcessCompletion()
    {
        string threadId = this.ThreadId.ToString();

        log.LogInternalInformation("[{ThreadId}] Reasoning loop completed. Notifying user", threadId);

        await RecordStateChange(ReasoningState.OrchestrationCompleted, "Orchestration completed");

        // Notify completion when done - use explicit call to activity
        await _taskOrchestrationContext.CallNotifyCompletionActivityAsync(new NotifyCompletionInput(
            ThreadId: threadId,
            InstanceId: _taskOrchestrationContext.InstanceId,
            Status: "Completed",
            Summary: "Task completed successfully"
        ));
        log.LogInternalInformation("[{ThreadId}] Completion notification sent", threadId);
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(OrchestrationAgentCompleteStep), "CompleteStep")]
[JsonDerivedType(typeof(OrchestrationAgentWaitStep), "WaitStep")]
[JsonDerivedType(typeof(OrchestrationAgentGetActionDetailsStep), "GetActionDetailsStep")]
[JsonDerivedType(typeof(OrchestrationAgentUserCommunicationStep), "UserCommunicationStep")]
[JsonDerivedType(typeof(OrchestrationAgentVisualizeAppComponentsStep), "VisualizeAppComponentsStep")]
[JsonDerivedType(typeof(OrchestrationAgentVisualizeAKSMicroserviceTopologyStep), "VisualizeAKSMicroserviceTopologyStep")]
[JsonDerivedType(typeof(OrchestrationAgentGenericExecuteStep), "GenericExecuteStep")]
[JsonDerivedType(typeof(OrchestrationAgentHelperAgentExecuteStep), "HelperAgentExecuteStep")]
public abstract class OrchestrationAgentStep
{
    public abstract Task ExecuteAsync(TaskOrchestrationContext context, OrchestrationAgent state);

    public static OrchestrationAgentStep CreateStep(FunctionCallContent functionCall, Guid? approvalId)
    {
        if (functionCall.Name == nameof(ControlFlowPluginDefinition.MarkPlanComplete))
        {
            return new OrchestrationAgentCompleteStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(ControlFlowPluginDefinition.Wait))
        {
            return new OrchestrationAgentWaitStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(RecordActionsPluginDefinition.GetActionDetails))
        {
            return new OrchestrationAgentGetActionDetailsStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(ControlFlowPluginDefinition.NotifyUser) || functionCall.Name == nameof(ControlFlowPluginDefinition.AskUserForInput))
        {
            return new OrchestrationAgentUserCommunicationStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(GraphDBPluginDefinition.VisualizeApplicationComponents))
        {
            return new OrchestrationAgentVisualizeAppComponentsStep { FunctionCall = functionCall };
        }
        else if (functionCall.Name == nameof(GraphDBPluginDefinition.VisualizeAKSMicroserviceTopology))
        {
            return new OrchestrationAgentVisualizeAKSMicroserviceTopologyStep { FunctionCall = functionCall };
        }
        else if (IsChartFunction(functionCall.Name))
        {
            return new OrchestrationAgentChartStep { FunctionCall = functionCall };
        }
        else if (HelperAgentsPluginDefinition.AllPluginNames.Contains(functionCall.Name))
        {
            return new OrchestrationAgentHelperAgentExecuteStep { FunctionCall = functionCall };
        }
        else
        {
            return new OrchestrationAgentGenericExecuteStep { FunctionCall = functionCall, ApprovalId = approvalId };
        }
    }

    private static bool IsChartFunction(string functionName)
    {
        return ((functionName == nameof(ChartPluginDefinition.PlotBarChartAsync) || functionName.Contains("BarChart", StringComparison.OrdinalIgnoreCase)) ||
                (functionName == nameof(ChartPluginDefinition.PlotPieChartAsync) || functionName.Contains("PlotPieChart", StringComparison.OrdinalIgnoreCase)) ||
                (functionName == nameof(ChartPluginDefinition.PlotScatterAsync) || functionName.Contains("PlotScatter", StringComparison.OrdinalIgnoreCase)) ||
                functionName == nameof(ChartPluginDefinition.PlotTimeSeriesData) ||
                (functionName == nameof(ChartPluginDefinition.PlotAreaChartWithCorrelationAsync) || functionName.Contains("PlotAreaChartWithCorrelation", StringComparison.OrdinalIgnoreCase)));
    }
}
