// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core;
using Agent.Core.Attributes;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.AgentTasks.Agents;
using Agent.Runtime.Helpers;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Agent.Runtime.AgentTasks.Handlers;

public sealed class IncidentInvestigationTaskHandler(
    ILoggerFactory loggerFactory,
    ILogger<IncidentInvestigationTaskHandler> logger,
    IAgentTasksRepository agentTaskRepository,
    IThreadRepository threadRepository,
    IChatClientProvider chatClientProvider,
    IToolFactory<AgentContext> toolFactory,
    IExtendedAgentService extendedAgentService,
    IAgentOutboundCommunicationService outboundCommunicationService,
    AgentTaskLocalStore rcaAgentsStore,
    SearchHelper searchHelper,
    Tracer tracer,
    CustomerLogger customerLogger,
    OpenAISettings openAISettings,
    AgentTaskToolResultHelper agentTaskToolResultHelper,
    IAgentFactory<AgentContext> agentFactory,
    IApprovalService approvalService
) : IAgentTaskHandler
{
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _hypothesisValidationSemaphore = new(3, 3); // Allow max 3 concurrent hypothesis validations
    private AgentTask? _currentAgentTask;
    private readonly ToolResultCache _toolCache = new();
    private readonly List<ChatMessage> _aggregatedToolHistory = new();
    private readonly ConcurrentBag<HypothesisTreeItem> _allInvestigatedHypotheses = new();
    private readonly ConcurrentBag<HypothesisTreeItem> _finalValidatedHypotheses = new();
    private readonly ConcurrentBag<string> _allHypothesesTitles = new();
    private List<string>? toolSubset = null;
    private readonly bool _is1PAgent = FirstPartyHelper.IsFirstPartyTenant();
    private Guid? _deepInvestigationNotificationMessageId;
    private readonly string _llmDeploymentName = openAISettings.LLMDeploymentName;

    public async Task ExecuteAsync(AgentTask agentTask, CancellationToken cancellationToken)
    {
        try
        {
            _currentAgentTask = agentTask;

            _toolCache.Clear();
            _aggregatedToolHistory.Clear();

            if (agentTask.Type != AgentTaskType.IncidentInvestigation)
            {
                throw new InvalidOperationException($"Invalid agent task type: {agentTask.Type}");
            }

            var inputData = agentTask.InputData as IncidentInvestigationTaskInputData
                ?? throw new InvalidOperationException("Invalid agent task input data");

            var state = agentTask.Properties as IncidentInvestigationTaskProperties
                ?? throw new InvalidOperationException("Invalid agent task properties");

            var context = (await threadRepository.GetAgentContextsForThreadAsync(agentTask.ThreadId)).FirstOrDefault()
                ?? throw new InvalidOperationException("No agent context found for the given thread");

            logger.LogInternalInformation(
                "Executing incident investigation task {TaskId} for thread {ThreadId}",
                agentTask.Id, context.ThreadId);

            // Check if this is a chat-triggered investigation that needs approval
            Approval? deepInvestigationApproval = null;
            bool isChatTriggered = await IsChatTriggeredInvestigationAsync();

            if (isChatTriggered)
            {
                logger.LogInternalInformation("Chat-triggered deep investigation detected for task {TaskId}, creating approval request", agentTask.Id);

                try
                {
                    deepInvestigationApproval = await CreateDeepInvestigationApprovalAsync(agentTask.ThreadId, agentTask.Id, context.Id);

                    _currentAgentTask = _currentAgentTask with { DeepInvestigationApprovalId = deepInvestigationApproval.Id };
                    await agentTaskRepository.UpdateAgentTaskAsync(_currentAgentTask);

                    await SendDeepInvestigationNotificationAsync(agentTask.ThreadId, agentTask.Id, deepInvestigationApproval);
                    bool approvalGranted = await WaitForApprovalAsync(deepInvestigationApproval.Id, CancellationToken.None);
                    var updatedApproval = await approvalService.GetApproval(_currentAgentTask.ThreadId, deepInvestigationApproval.Id.ToString());

                    if (!approvalGranted)
                    {
                        logger.LogInternalWarning("Deep investigation approval {ApprovalId} was denied or timed out for task {TaskId}, falling back to investigation without elevated tokens",
                            deepInvestigationApproval.Id, agentTask.Id);
                        await SendDeepInvestigationNotificationAsync(agentTask.ThreadId, agentTask.Id, updatedApproval);

                        // Fall back to regular investigation flow without elevated tokens
                        // Clear the approval reference since we're proceeding without approval
                        _currentAgentTask = _currentAgentTask with { DeepInvestigationApprovalId = null };
                        await agentTaskRepository.UpdateAgentTaskAsync(_currentAgentTask);

                        logger.LogInternalInformation("Continuing investigation for task {TaskId} without elevated permissions", agentTask.Id);
                    }
                    else
                    {
                        logger.LogInternalInformation("Deep investigation approval {ApprovalId} granted for task {TaskId}",
                            deepInvestigationApproval.Id, agentTask.Id);
                        await SendDeepInvestigationNotificationAsync(agentTask.ThreadId, agentTask.Id, updatedApproval);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogInternalError(ex, "Failed to create or process approval for deep investigation task {TaskId}", agentTask.Id);

                    // If we have a pending approval, try to update it to cancelled and notify user
                    if (deepInvestigationApproval != null)
                    {
                        try
                        {
                            await approvalService.SubmitApprovalDecision(
                                deepInvestigationApproval.Id.ToString(),
                                "system",
                                ApprovalDecision.Cancelled,
                                agentTask.ThreadId,
                                null,
                                deepInvestigationApproval.OboTokenScope
                            );

                            var failedApproval = await approvalService.GetApproval(agentTask.ThreadId, deepInvestigationApproval.Id.ToString());
                            await SendDeepInvestigationNotificationAsync(agentTask.ThreadId, agentTask.Id, failedApproval);
                        }
                        catch (Exception notificationEx)
                        {
                            logger.LogInternalError(notificationEx, "Failed to notify user about approval failure for task {TaskId}", agentTask.Id);
                        }
                    }

                    agentTask = agentTask with { Status = AgentTaskStatus.Failed, LastModified = DateTime.UtcNow };
                    await agentTaskRepository.UpdateAgentTaskAsync(agentTask);

                    return;
                }
            }
            else
            {
                logger.LogInternalInformation("Incident handler triggered investigation for task {TaskId}, no approval required", agentTask.Id);
                await SendDeepInvestigationNotificationAsync(agentTask.ThreadId, agentTask.Id);
            }

            // Save the agent task to the thread document immediately when investigation starts
            await threadRepository.UpdateTaskOnThreadAsync(agentTask.ThreadId, agentTask.ToShortForm());
            logger.LogInternalInformation("Agent task {TaskId} saved to thread {ThreadId}", agentTask.Id, agentTask.ThreadId);

            // set approval context, tools will use token from this context
            var usingOboToken = await SetupApprovalContextAsync(agentTask.ThreadId, agentTask.Id);

            if (usingOboToken)
            {
                logger.LogInternalInformation("Deep investigation will use elevated OBO permissions for all tools");
            }
            else
            {
                logger.LogInternalInformation("Deep investigation will use standard managed identity permissions");
            }

            if (_is1PAgent)
            {
                var allAgents = new List<YamlAgentDescriptor>();

                // Load agents from the extensibility API
                for (int i = 0; ; i++)
                {
                    var agentsFromExtensibleApi = await extendedAgentService.GetAgentsAsync(i, 100, null);
                    foreach (var agent in agentsFromExtensibleApi)
                    {
                        if (agent.Metadata.Tags?.Contains("rcaagent") == true)
                        {
                            allAgents.Add(agent);
                        }
                    }

                    if (!agentsFromExtensibleApi.HasNextPage)
                        break;
                }

                toolSubset = allAgents.SelectMany(agent => agent.Tools)
                    .Distinct()
                    .ToList();

                logger.LogInternalInformation($"Successfully loaded {allAgents.Count} agents");

                // Common tools
                toolSubset.AddRange(["GetASIPageForManagedCluster", "GetASIPageForContainerAppJob", "GetASIPageForManagedEnvironment", "GetASIPageForRevision", "PlotTimeSeriesData", "HandoffBack"]);
                logger.LogInternalInformation($"Complete tool subset: [{string.Join(", ", toolSubset.Distinct())}]");
            }

            using var tracingHelper = new TracingHelper(tracer, context.ThreadId.ToString(), nameof(AgentTaskType.IncidentInvestigation));
            var runHooks = tracingHelper.GetAgentTaskTracingHooks();

            // Add customer logger hooks for first-party agents (following TracingHelper convention)
            // COMMENTED OUT FOR TESTING - Enable logging for all agents
            // if (_is1PAgent)
            // {
            var customerLoggerHelper = new CustomerLoggerHelper(customerLogger, context.ThreadId.ToString(), nameof(AgentTaskType.IncidentInvestigation));
            var customerLoggerHooks = customerLoggerHelper.GetCustomerLoggerHooks();

            // Subscribe customer logger event handlers to the main runHooks
            runHooks.ToolStart += async (context, agent, functionCall, tool, input) =>
            {
                await customerLoggerHooks.OnToolStart(context, agent, functionCall, tool, input);
            };
            runHooks.ToolEnd += async (context, agent, functionCallContent, tool, output) =>
            {
                await customerLoggerHooks.OnToolEnd(context, agent, functionCallContent, tool, output);
            };
            runHooks.AgentStart += async (context, agent) =>
            {
                await customerLoggerHooks.OnAgentStart(context, agent);
            };
            runHooks.AgentEnd += async (context, agent, result) =>
            {
                await customerLoggerHooks.OnAgentEnd(context, agent, result);
            };
            runHooks.Handoff += async (context, fromAgent, toAgent, handoffReasoning) =>
            {
                await customerLoggerHooks.OnHandoff(context, fromAgent, toAgent, handoffReasoning);
            };
            runHooks.ModelGenerationStart += async (context, agent, chatMessages, chatOptions) =>
            {
                await customerLoggerHooks.OnModelGenerationStart(context, agent, chatMessages, chatOptions);
            };
            runHooks.ModelGenerationEnd += async (context, agent, response) =>
            {
                await customerLoggerHooks.OnModelGenerationEnd(context, agent, response);
            };

            logger.LogInternalInformation("CustomerLogger hooks enabled for testing - first-party check commented out");
            // }
            // else
            // {
            //     logger.LogInternalInformation("CustomerLogger hooks disabled - not a first-party agent");
            // }

            // Register the step completion hook once at the beginning
            runHooks.ToolStart += HandleReportStepCompletionToolCallAsync;

            runHooks.ResolveFactoryTools += (runContext, agent) =>
            {
                List<AIFunction> tools = [];

                foreach (var toolName in agent.FactoryTools)
                {
                    // Skip disabled tools (those that don't meet EnabledIf condition)
                    if ((toolFactory as ToolFactory<AgentContext>)!.IsToolDisabled(toolName))
                    {
                        logger.LogInternalDebug("Skipping disabled tool {toolName} for agent {agentName}", toolName, agent.Name);
                        continue;
                    }

                    var tool = (toolFactory as ToolFactory<AgentContext>)!.GetTool(toolName, context.ThreadId);

                    tools.Add(tool);
                }

                return Task.FromResult(tools);
            };

            // 1. Initial Investigation
            logger.LogInternalInformation("Starting initial investigation for task: {TaskId}", agentTask.Id);
            var currentStepSpan = tracingHelper.StartAgentTaskStepSpan("InitialInvestigation");

            // Set investigation step context for tool result routing
            Core.ToolStatic.AsyncLocalInvestigationStepContext.Value = new InvestigationStepContext(
                "InitialInvestigation");

            if (state.InitialInvestigation.Status != InitialInvestigationStatus.Complete)
            {
                state.InitialInvestigation.StatusMessage = "Starting initial investigation...";
                state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);

                // 1. TODO: runbook for alert scenarios (could be passed in as input data)

                // 2. Gather context
                if (state.InitialInvestigation.GatheringContext.Status != InitialInvestigationStatus.Complete)
                {
                    // Gather context
                    // Gather logs, look at relevant metrics, etc.
                    // generate final summary of the initial investigation
                    var msg = $"""
                        The incident description is as follows:
                        <desc>
                        {inputData.IncidentDescription}
                        </desc>
                        """;

                    var toolSelectionAgent = IncidentInvestigationAgents.CreateGatheringContextToolSelectionAgent(toolFactory, _is1PAgent, toolSubset, _llmDeploymentName);

                    var toolNames = await CallAgentAsync<List<string>>(
                        toolSelectionAgent,
                        context,
                        new ChatMessage(ChatRole.User, msg),
                        runHooks,
                        true,
                        injectToolHistory: false,
                        tracer: tracer,
                        parentSpan: currentStepSpan,
                        cancellationToken: cancellationToken);

                    ValidateAndAddRequiredTools(toolNames);

                    logger.LogInternalInformation(
                        "Tool selection agent selected {ToolCount} tools: [{Tools}]",
                        toolNames.Count,
                        string.Join(", ", toolNames)
                    );

                    if (_is1PAgent)
                    {
                        state.InitialInvestigation.ToolNames = toolNames;
                    }

                    state.InitialInvestigation.StatusMessage = $"Selected {toolNames.Count} investigation tools, running initial analysis...";
                    state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);

                    logger.LogInternalInformation("Starting initial investigation agent.");
                    var initialInvestigationAgent = IncidentInvestigationAgents.CreateInitialInvestigationAgent(toolNames, _is1PAgent, _llmDeploymentName);

                    // local function to wrap updating initial investigation status with tool call info
                    async Task UpdateInitialInvestigationStatus(
                        RunContextWrapper<AgentContext> ctx,
                        Agent<AgentContext> agent,
                        FunctionCallContent functionCall,
                        AIFunction tool,
                        IEnumerable<KeyValuePair<string, object?>>? input)
                    {
                        var userDisplayedToolDescription = ToolDescriptionHelper.GetUserDescriptionForFunctionCallName(tool.Name);
                        state.InitialInvestigation.StatusMessage = userDisplayedToolDescription;
                        state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);
                    }

                    // subscribe to tool start event to update status
                    runHooks.ToolStart += UpdateInitialInvestigationStatus;

                    var initialInvestigationResult = await CallAgentAsync<InitialInvestigationResult>(
                        initialInvestigationAgent,
                        context,
                        new ChatMessage(ChatRole.User, msg),
                        runHooks,
                        true,
                        tracer: tracer,
                        parentSpan: currentStepSpan,
                        cancellationToken: cancellationToken);

                    state.InitialInvestigation.GatheringContext.Status = InitialInvestigationStatus.Complete;

                    // unsubscribe from tool start event
                    runHooks.ToolStart -= UpdateInitialInvestigationStatus;

                    // Create steps from LLM result
                    // merge with existing tool execution step that were saved during initial investigation
                    var steps = initialInvestigationResult.ContextGatheringSteps.Select(s => new InitialInvestigationStep
                    {
                        Title = s.Title,
                        Summary = s.Summary,
                        Status = InitialInvestigationStatus.Complete,
                        ToolExecutions = []
                    }).ToList();

                    // Fetch current agent task from database to get any existing tool results step
                    var currentAgentTask = await agentTaskRepository.GetAgentTaskAsync(_currentAgentTask.ThreadId, _currentAgentTask.Id);
                    if (currentAgentTask?.Properties is IncidentInvestigationTaskProperties currentProperties)
                    {
                        var toolResultsStepTitle = "Context Gathering Operation Results";
                        var existingToolResultsStep = currentProperties.InitialInvestigation.GatheringContext.Steps
                            .FirstOrDefault(s => s.Title.Equals(toolResultsStepTitle, StringComparison.OrdinalIgnoreCase));

                        if (existingToolResultsStep != null && existingToolResultsStep.ToolExecutions.Any())
                        {
                            // Add the tool results to the end of the steps
                            existingToolResultsStep.Status = InitialInvestigationStatus.Complete;
                            steps.Add(existingToolResultsStep);
                        }
                    }

                    state.InitialInvestigation.GatheringContext.Steps = steps;

                    logger.LogInternalInformation("Initial investigation agent completed with summary: {Summary}", initialInvestigationResult.Summary);

                    state.InitialInvestigation.StatusMessage = "Incident research complete.";

                    // 3. Replaying memories
                    // retrieve past trajectories
                    // trajectories will have steps to follow and instructions about what NOT to do
                    //state.InitialInvestigation.GatheringContext.Steps = new List<InitialInvestigationStep>();

                    // 4. Generate a summary of the initial investigation

                    state.InitialInvestigation.Summary = initialInvestigationResult.Summary;
                    state.InitialInvestigation.TimeFrame = initialInvestigationResult.TimeFrame;
                    state.InitialInvestigation.Details = initialInvestigationResult.Details;
                    state.InitialInvestigation.IncidentDescription = initialInvestigationResult.IncidentDescription;
                    state.InitialInvestigation.AffectedResources = new List<string>(initialInvestigationResult.AffectedResources);
                    state.InitialInvestigation.KeyFindings = initialInvestigationResult.KeyFindings;
                    state.InitialInvestigation.Status = InitialInvestigationStatus.Complete;

                    state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);
                }
            }
            logger.LogInternalInformation("Initial investigation completed with summary.");
            tracingHelper.EndAgentTaskStepSpan();

            // 2. Forming Hypothesis
            logger.LogInternalInformation("Starting forming hypothesis for task: {TaskId}", agentTask.Id);
            currentStepSpan = tracingHelper.StartAgentTaskStepSpan("FormingHypothesis");

            // Set investigation step context for tool result routing
            Core.ToolStatic.AsyncLocalInvestigationStepContext.Value = new InvestigationStepContext(
                "FormingHypothesis");

            // Clear thread-safe collections for this investigation
            ClearConcurrentCollections();

            if (state.FormingHypothesis.Status != FormingHypothesisStatus.Complete)
            {
                // Generate initial hypotheses
                state.FormingHypothesis.Status = FormingHypothesisStatus.InProgress;
                state.FormingHypothesis.StatusMessage = "Generating hypotheses...";
                state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);

                var initialHypotheses = await GenerateHypotheses(
                    inputData.IncidentDescription,
                    state.InitialInvestigation.ToString(),
                    null,
                    [],
                    context,
                    runHooks,
                    currentStepSpan,
                    cancellationToken);

                state.FormingHypothesis.Hypotheses = initialHypotheses;

                // Add initial hypothesis titles to thread-safe collection
                foreach (var h in initialHypotheses)
                {
                    _allHypothesesTitles.Add(h.Title);
                }

                state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);

                // 3. Validating Hypotheses with Parallel Processing
                logger.LogInternalInformation("Starting parallel hypothesis validation for task: {TaskId}", agentTask.Id);
                state.FormingHypothesis.StatusMessage = "Validating hypotheses...";
                state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);

                await ProcessHypothesesInParallel(
                    state.FormingHypothesis.Hypotheses,
                    1, // Initial depth
                    inputData,
                    state,
                    context,
                    runHooks,
                    currentStepSpan,
                    cancellationToken);

                state.FormingHypothesis.Status = FormingHypothesisStatus.Complete;
                state = await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);
            }
            tracingHelper.EndAgentTaskStepSpan();
            logger.LogInternalInformation("Forming hypothesis completed with {ValidHypothesisCount} valid hypotheses.",
                _finalValidatedHypotheses.Count);

            // 4. Conclusion
            // three possibilities based on investigation results:
            // TODO: isRootCause functionality removed
            // 2. 1 valid hypothesis at the end → isRootCause == true, or all other hypothesis invalidated treat final valid hypothesis like root cause
            // 2. 1 valid hypothesis at the end → treat final valid hypothesis like root cause
            // 3. >1 valid hypothesis at the end → multiple hypotheses
            // 4. 0 valid hypothesis at the end → inconclusive
            logger.LogInternalInformation("Starting conclusion generation for task: {TaskId}", agentTask.Id);
            currentStepSpan = tracingHelper.StartAgentTaskStepSpan("Conclusion");

            var finalValidatedHypothesesList = _finalValidatedHypotheses.ToList();
            var allInvestigatedHypothesesList = _allInvestigatedHypotheses.ToList();

            if (finalValidatedHypothesesList.Count == 1)
            {
                // Stream single hypothesis conclusion
                await GenerateSingleValidHypothesisConclusion(finalValidatedHypothesesList.First(), inputData, context, runHooks, currentStepSpan, cancellationToken);
            }
            else if (finalValidatedHypothesesList.Count > 1)
            {
                // Stream multiple hypotheses conclusion
                await GenerateMultipleValidHypothesesConclusion(finalValidatedHypothesesList, inputData, context, runHooks, currentStepSpan, cancellationToken);
            }
            else
            {
                // Stream inconclusive conclusion - use all investigated hypotheses
                await GenerateInconclusiveConclusion(inputData, context, runHooks, currentStepSpan, allInvestigatedHypothesesList, cancellationToken);
            }

            state = await SaveStateAndStreamUpdateAsync(newStatus: AgentTaskStatus.Complete, cancellationToken: cancellationToken);


            // Stream conclusion completion
            tracingHelper.EndAgentTaskStepSpan();
            logger.LogInternalInformation("Incident investigation task {TaskId} completed successfully.", agentTask.Id);

            // Post the conclusion to the thread
            string conclusion = state.Conclusion.Title + "\n\n" + state.Conclusion.Summary;

            var assistantMessage = new ChatMessage(ChatRole.Assistant, conclusion);
            var agentChatHistory = await threadRepository.GetAgentChatHistoryAsync(context.Id);
            if (agentChatHistory == null)
            {
                logger.LogInternalError("[{threadId}] AgentChatHistory is null", context.ThreadId);
                throw new InvalidOperationException("AgentChatHistory is null");
            }
            var reasoningMessage = assistantMessage.GetReasoningMessage(context.Id);
            await threadRepository.CreateReasoningMessageAsync(reasoningMessage);
            await threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage);
            await outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                context,
                assistantMessage);
        }
        catch (Exception e)
        {
            // Stream error
            await SaveStateAndStreamUpdateAsync(newStatus: AgentTaskStatus.Failed, cancellationToken: cancellationToken);

            logger.LogInternalError(e, "Error while executing investigation");
            throw;
        }
        finally
        {
            // Clear approval context to prevent OBO token leakage to other operations
            Core.ToolStatic.AsyncLocalApprovalContext.Value = null;
            // Clear investigation step context
            Core.ToolStatic.AsyncLocalInvestigationStepContext.Value = null;
            // Clear agent task context
            Core.ToolStatic.AsyncLocalAgentTaskId.Value = null;
        }
    }

    /// <summary>
    /// Processes hypotheses using BFS approach with a queue for maximum parallelism.
    /// </summary>
    private async Task ProcessHypothesesInParallel(
        IList<HypothesisTreeItem> hypotheses,
        int currentDepth,
        IncidentInvestigationTaskInputData inputData,
        IncidentInvestigationTaskProperties state,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        CancellationToken cancellationToken)
    {
        if (hypotheses.Count == 0)
            return;

        // Use a concurrent queue to track all hypotheses to be processed (BFS style)
        var hypothesisQueue = new ConcurrentQueue<(HypothesisTreeItem hypothesis, int depth)>();

        // Add initial hypotheses to queue
        foreach (var hypothesis in hypotheses)
        {
            hypothesisQueue.Enqueue((hypothesis, currentDepth));
        }

        // Track running tasks to maintain controlled concurrency - use thread-safe collections
        var runningTasks = new ConcurrentBag<Task>();
        var completedTasks = new ConcurrentBag<Task>();

        while (!hypothesisQueue.IsEmpty || !runningTasks.IsEmpty)
        {
            // Start new tasks up to concurrency limit
            while (runningTasks.Count < 3 && hypothesisQueue.TryDequeue(out var item))
            {
                var (hypothesis, depth) = item;

                if (depth > 3)
                    continue;

                var task = ProcessSingleHypothesisWithQueueAsync(
                    hypothesis,
                    depth,
                    inputData,
                    state,
                    context,
                    runHooks,
                    currentStepSpan,
                    hypothesisQueue,
                    cancellationToken);

                runningTasks.Add(task);
                logger.LogInternalInformation("Started processing hypothesis: {HypothesisTitle} at depth {Depth}", hypothesis.Title, depth);
            }

            // Wait for at least one task to complete
            if (!runningTasks.IsEmpty)
            {
                var runningTasksArray = runningTasks.ToArray();
                if (runningTasksArray.Length > 0)
                {
                    var completedTask = await Task.WhenAny(runningTasksArray);

                    // Remove completed task from running tasks (drain and rebuild)
                    var remainingTasks = new ConcurrentBag<Task>();
                    while (runningTasks.TryTake(out var task))
                    {
                        if (task != completedTask)
                        {
                            remainingTasks.Add(task);
                        }
                    }
                    runningTasks = remainingTasks;

                    completedTasks.Add(completedTask);

                    // Handle any exceptions
                    try
                    {
                        await completedTask;
                    }
                    catch (Exception ex)
                    {
                        logger.LogInternalError(ex, "Error in hypothesis processing task");
                    }
                }
            }
        }

        // Wait for all remaining tasks to complete
        var allCompletedTasks = completedTasks.ToArray();
        if (allCompletedTasks.Length > 0)
        {
            await Task.WhenAll(allCompletedTasks);
        }

        logger.LogInternalInformation("Completed BFS-style hypothesis processing");
    }

    /// <summary>
    /// Processes a single hypothesis and adds its children to the queue when validated (BFS approach).
    /// </summary>
    private async Task ProcessSingleHypothesisWithQueueAsync(
        HypothesisTreeItem hypothesis,
        int depth,
        IncidentInvestigationTaskInputData inputData,
        IncidentInvestigationTaskProperties state,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        ConcurrentQueue<(HypothesisTreeItem hypothesis, int depth)> hypothesisQueue,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInternalInformation("Starting validation for hypothesis: {HypothesisTitle} at depth {Depth}", hypothesis.Title, depth);

            // Update status with thread-safe state update
            hypothesis.StatusMessage = "Analyzing...";
            hypothesis.Status = HypothesisStatus.Validating;
            await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);

            var validationResult = await ValidateHypothesisAsync(
                inputData.IncidentDescription,
                state.InitialInvestigation.ToString(),
                hypothesis,
                state,
                context,
                runHooks,
                currentStepSpan,
                cancellationToken);

            // Update hypothesis status
            hypothesis.Status = validationResult.Status switch
            {
                HypothesisValidationStatus.Validated => HypothesisStatus.Validated,
                HypothesisValidationStatus.Invalidated => HypothesisStatus.Invalidated,
                HypothesisValidationStatus.Inconclusive => HypothesisStatus.Inconclusive,
                _ => HypothesisStatus.Inconclusive
            };

            hypothesis.Steps = validationResult.Steps;
            hypothesis.Reasoning = validationResult.Reasoning;

            // Add to thread-safe collections
            _allInvestigatedHypotheses.Add(hypothesis);

            // Update status message
            var statusMessage = hypothesis.Status switch
            {
                HypothesisStatus.Validated => $"Hypothesis validated: {hypothesis.Title}",
                HypothesisStatus.Invalidated => $"Hypothesis invalidated: {hypothesis.Title}",
                HypothesisStatus.Inconclusive => $"Hypothesis inconclusive: {hypothesis.Title}",
                _ => $"Hypothesis status updated to {hypothesis.Status}: {hypothesis.Title}"
            };

            hypothesis.StatusMessage = statusMessage;
            await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);

            // Handle validated hypotheses - add children to queue for BFS processing
            if (hypothesis.Status == HypothesisStatus.Validated)
            {
                if (depth >= 3)
                {
                    // At maximum depth, add to final validated hypotheses
                    _finalValidatedHypotheses.Add(hypothesis);
                    logger.LogInternalInformation("Added hypothesis to final validated list at max depth: {HypothesisTitle}", hypothesis.Title);
                }
                else
                {
                    // Generate child hypotheses for further investigation
                    var childHypotheses = await GenerateHypotheses(
                        inputData.IncidentDescription,
                        state.InitialInvestigation.ToString(),
                        hypothesis.Description,
                        _allHypothesesTitles.ToList(),
                        context,
                        runHooks,
                        currentStepSpan,
                        cancellationToken);

                    hypothesis.Children = childHypotheses;

                    // Add child hypothesis titles to thread-safe collection
                    foreach (var child in childHypotheses)
                    {
                        child.ParentHypothesisDescription = hypothesis.Description;
                        _allHypothesesTitles.Add(child.Title);
                    }

                    await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);

                    // If no children were generated or we're at depth 2, add to final validated
                    if (childHypotheses.Count == 0 || depth >= 2)
                    {
                        _finalValidatedHypotheses.Add(hypothesis);
                    }
                    else
                    {
                        // Add children to queue for BFS processing (they'll be picked up by the main loop)
                        foreach (var child in childHypotheses)
                        {
                            hypothesisQueue.Enqueue((child, depth + 1));
                        }

                        logger.LogInternalInformation("Added {ChildCount} children to queue for hypothesis: {HypothesisTitle}",
                            childHypotheses.Count, hypothesis.Title);
                    }
                }
            }

            logger.LogInternalInformation("Completed validation for hypothesis: {HypothesisTitle} with status: {Status}", hypothesis.Title, hypothesis.Status);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error processing hypothesis: {HypothesisTitle}", hypothesis.Title);
            hypothesis.Status = HypothesisStatus.Inconclusive;
            hypothesis.StatusMessage = $"Error processing hypothesis: {ex.Message}";
            _allInvestigatedHypotheses.Add(hypothesis);
        }
    }

    /// <summary>
    /// Sends a deep investigation notification to the user.
    /// </summary>
    /// <param name="threadId">The thread ID</param>
    /// <param name="agentTaskId">The agent task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task SendDeepInvestigationNotificationAsync(Guid threadId, Guid agentTaskId, Approval? approval = null)
    {
        try
        {
            logger.LogInternalInformation("Sending deep investigation notification for thread {ThreadId}, task {TaskId}, with approval: {HasApproval}",
                threadId, agentTaskId, approval != null);

            var statusMessage = await GetCurrentStatusMessageAsync();
            var currentPhase = await GetCurrentPhaseAsync();
            var agentTaskInfo = _currentAgentTask != null
                ? new AgentTaskInfo(_currentAgentTask.Id, _currentAgentTask.Title, _currentAgentTask.Status, _currentAgentTask.LastModified, Phase: currentPhase, StatusMessage: statusMessage)
                : new AgentTaskInfo(agentTaskId, "Deep Investigation", AgentTaskStatus.InProgress, DateTime.UtcNow, Phase: currentPhase, StatusMessage: statusMessage);

            logger.LogInternalInformation("Creating agentTaskInfo: _currentAgentTask is {IsNull}, agentTaskId: {AgentTaskId}, created agentTaskInfo: {AgentTaskInfoId}",
                _currentAgentTask == null ? "null" : "not null", agentTaskId, agentTaskInfo.Id);

            var sanitizedApproval = approval != null
                ? new Approval(
                    Id: approval.Id,
                    ThreadId: approval.ThreadId,
                    Title: approval.Title,
                    Description: approval.Description,
                    Status: approval.Status,
                    CreatedTimestamp: approval.CreatedTimestamp,
                    DecisionTimestamp: approval.DecisionTimestamp,
                    OrchestrationId: approval.OrchestrationId,
                    AgentContextId: approval.AgentContextId,
                    OboToken: null, // Exclude token for security
                    OboTokenScope: approval.OboTokenScope,
                    DecisionUser: approval.DecisionUser
                )
                : null;

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            };

            var messageContent = new
            {
                AgentTaskInfo = agentTaskInfo,
                Approval = sanitizedApproval
            };

            var messageJson = JsonSerializer.Serialize(messageContent, options);

            // Check if this is an update to existing notification or a new one
            if (_deepInvestigationNotificationMessageId != null && approval?.Status != ApprovalDecision.Pending)
            {
                // Update existing notification message with approval result
                logger.LogInternalInformation("Updating existing message {MessageId} with agentTaskInfo: {AgentTaskInfo}",
                    _deepInvestigationNotificationMessageId.Value, agentTaskInfo);
                await threadRepository.UpdateMessageAsync(threadId, _deepInvestigationNotificationMessageId.Value, messageJson, agentTaskInfo);
                await outboundCommunicationService.NotifyApprovalUpdate(threadId, sanitizedApproval!, _deepInvestigationNotificationMessageId.Value);

                logger.LogInternalInformation("Updated existing deep investigation notification message {MessageId} with approval status {ApprovalStatus}",
                    _deepInvestigationNotificationMessageId.Value, approval!.Status);
            }
            else
            {
                var notificationMessageId = Guid.NewGuid();
                ChatMessage message = new ChatMessage(ChatRole.User, messageJson);

                await outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                    threadId,
                    string.Empty,
                    message: message,
                    agentTaskInfo: agentTaskInfo,
                    approval: sanitizedApproval,
                    messageId: notificationMessageId,
                    type: StreamMessageType.DeepInvestigation
                   );

                _deepInvestigationNotificationMessageId = notificationMessageId;

                logger.LogInternalInformation("Created new deep investigation notification message {MessageId}",
                    notificationMessageId);
            }

            logger.LogInternalInformation("Successfully sent deep investigation notification for thread {ThreadId}, task {TaskId}, approval: {ApprovalId}",
                threadId, agentTaskId, approval?.Id);
        }
        catch (Exception ex)
        {
            logger.LogInternalWarning(ex, "Failed to send deep investigation notification for thread {ThreadId}, task {TaskId}. Investigation will continue.", threadId, agentTaskId);
            // Don't rethrow - notification failure shouldn't break the investigation
        }
    }

    private void ValidateAndAddRequiredTools(List<string> toolNames)
    {
        if (_is1PAgent)
        {
            toolNames.AddRange(
            [
                "GetIssueInvestigationTimeRange",
                "GetIncidentInfoRCAContainerApp",
                "SearchContainerAppsResourcesByName",
                "SearchDesignDocs",
                "SearchMemory"
            ]);
        }
        else
        {
            toolNames.AddRange(
            [
                "RunAzCliReadCommands",
                "GetResourceDetailedProperties",
                "SearchResource",
                "SearchResourceByName",
                "GetResourceIdForResourceName",
                "ListResourcesByType",
                "SearchDesignDocs",
                "SearchMemory",
                "GetApplicationComponentsSummary",
                "GetMetricsTimeSeriesAnalysis",
                "ListAvailableMetrics"
            ]);

            //if (toolNames.Contains("GetMetricsTimeSeriesAnalysis") || toolNames.Contains("GetMetricTimeSeriesElementsForAzureResource"))
            //{
            //    toolNames.Add("ListAvailableMetrics");
            //}
        }

        toolNames.RemoveAll(name => !toolFactory.HasTool(name));

        // Remove duplicates
        var uniqueElements = new HashSet<string>();
        toolNames.RemoveAll(item => !uniqueElements.Add(item));
    }

    private IncidentInvestigationTaskProperties GetCurrentState()
    {
        return _currentAgentTask?.Properties as IncidentInvestigationTaskProperties
            ?? throw new InvalidOperationException("No current agent task set");
    }

    private async Task<string?> GetCurrentStatusMessageAsync()
    {
        if (_currentAgentTask?.Properties is not IncidentInvestigationTaskProperties state)
        {
            return null;
        }

        if (_currentAgentTask.DeepInvestigationApprovalId != null)
        {
            try
            {
                var approval = await approvalService.GetApproval(_currentAgentTask.ThreadId, _currentAgentTask.DeepInvestigationApprovalId.Value.ToString());
                if (approval?.Status == ApprovalDecision.Pending)
                {
                    return "Pending approval";
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalWarning(ex, "Failed to get approval status for task {TaskId}", _currentAgentTask.Id);
            }
        }

        if (_currentAgentTask.Status == AgentTaskStatus.Complete)
        {
            return "Investigation complete";
        }

        if (state.FormingHypothesis.Status == FormingHypothesisStatus.InProgress)
        {
            return state.FormingHypothesis.StatusMessage ?? "Forming hypotheses...";
        }

        if (state.InitialInvestigation.Status == InitialInvestigationStatus.InProgress)
        {
            return state.InitialInvestigation.StatusMessage ?? "Performing incident research...";
        }

        if (state.FormingHypothesis.Status == FormingHypothesisStatus.Complete)
        {
            return state.FormingHypothesis.StatusMessage ?? "Hypothesis analysis complete";
        }

        if (state.InitialInvestigation.Status == InitialInvestigationStatus.Complete)
        {
            return "Initial research complete, preparing to form hypotheses...";
        }

        return null;
    }

    /// <summary>
    /// Gets the current investigation phase from the current agent task
    /// </summary>
    private async Task<string?> GetCurrentPhaseAsync()
    {
        if (_currentAgentTask?.Properties is not IncidentInvestigationTaskProperties state)
        {
            return null;
        }

        if (_currentAgentTask.DeepInvestigationApprovalId != null)
        {
            try
            {
                var approval = await approvalService.GetApproval(_currentAgentTask.ThreadId, _currentAgentTask.DeepInvestigationApprovalId.Value.ToString());
                if (approval?.Status == ApprovalDecision.Pending)
                {
                    return "Pending Approval";
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalWarning(ex, "Failed to get approval status for phase determination, task {TaskId}", _currentAgentTask.Id);
            }
        }

        if (_currentAgentTask.Status == AgentTaskStatus.Complete)
        {
            return "Conclusion";
        }

        if (state.FormingHypothesis.Status == FormingHypothesisStatus.InProgress)
        {
            if (state.FormingHypothesis.StatusMessage?.Contains("Validating") == true)
            {
                return "Validating Hypotheses";
            }
        }

        if (state.FormingHypothesis.Status == FormingHypothesisStatus.InProgress ||
            state.FormingHypothesis.Status == FormingHypothesisStatus.Complete)
        {
            return "Forming Hypotheses";
        }

        if (state.InitialInvestigation.Status == InitialInvestigationStatus.InProgress ||
            state.InitialInvestigation.Status == InitialInvestigationStatus.Complete)
        {
            return "Incident Research";
        }

        return null;
    }

    /// <summary>
    /// Updates the deep investigation notification message with current status and streams to frontend
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task UpdateDeepInvestigationNotificationAsync(CancellationToken cancellationToken = default)
    {
        if (_deepInvestigationNotificationMessageId == null || _currentAgentTask == null)
        {
            return;
        }

        var currentStatusMessage = await GetCurrentStatusMessageAsync();
        var currentPhase = await GetCurrentPhaseAsync();
        var updatedAgentTaskInfo = new AgentTaskInfo(
            _currentAgentTask.Id,
            _currentAgentTask.Title,
            _currentAgentTask.Status,
            _currentAgentTask.LastModified,
            Phase: currentPhase,
            StatusMessage: currentStatusMessage);

        Approval? currentApproval = null;
        if (_currentAgentTask.DeepInvestigationApprovalId != null)
        {
            try
            {
                currentApproval = await approvalService.GetApproval(_currentAgentTask.ThreadId, _currentAgentTask.DeepInvestigationApprovalId.Value.ToString());
            }
            catch (Exception ex)
            {
                logger.LogInternalWarning(ex, "Failed to get approval {ApprovalId} for task {TaskId}",
                    _currentAgentTask.DeepInvestigationApprovalId, _currentAgentTask.Id);
            }
        }

        var sanitizedApproval = currentApproval != null
            ? new Approval(
                Id: currentApproval.Id,
                ThreadId: currentApproval.ThreadId,
                Title: currentApproval.Title,
                Description: currentApproval.Description,
                Status: currentApproval.Status,
                CreatedTimestamp: currentApproval.CreatedTimestamp,
                DecisionTimestamp: currentApproval.DecisionTimestamp,
                OrchestrationId: currentApproval.OrchestrationId,
                AgentContextId: currentApproval.AgentContextId,
                OboToken: null, // Exclude token for security
                OboTokenScope: currentApproval.OboTokenScope,
                DecisionUser: currentApproval.DecisionUser
            )
            : null;

        var messageContent = new
        {
            AgentTaskInfo = updatedAgentTaskInfo,
            Approval = sanitizedApproval
        };

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };
        var updatedMessageJson = JsonSerializer.Serialize(messageContent, options);

        try
        {
            await threadRepository.UpdateMessageAsync(
                _currentAgentTask.ThreadId,
                (Guid)_deepInvestigationNotificationMessageId,
                updatedMessageJson,
                updatedAgentTaskInfo);

            await outboundCommunicationService.AppendAgentStreamMessage(
                _currentAgentTask.ThreadId,
                updatedMessageJson,
                StreamMessageType.DeepInvestigation,
                _deepInvestigationNotificationMessageId,
                _currentAgentTask.LastModified,
                cancellationToken);

            logger.LogInternalInformation(
                "Updated deep investigation notification {MessageId} with status: {StatusMessage}",
                _deepInvestigationNotificationMessageId.Value, currentStatusMessage);
        }
        catch (Exception ex)
        {
            logger.LogInternalWarning(ex,
                "Failed to update deep investigation notification {MessageId} - investigation will continue",
                _deepInvestigationNotificationMessageId.Value);
        }
    }

    /// <summary>
    /// Saves the current state of the agent task and streams an update to the client.
    /// </summary>
    /// <param name="replacementState">
    /// If provided, the current state of the agent task will be replaced with this state.
    /// </param>
    /// <param name="newStatus">If provided, the status of the agent task will be updated to this status.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A new state object of the agent task.</returns>
    private async Task<IncidentInvestigationTaskProperties> SaveStateAndStreamUpdateAsync(
        IncidentInvestigationTaskProperties? replacementState = null,
        AgentTaskStatus? newStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (_currentAgentTask == null)
        {
            throw new InvalidOperationException("No current agent task set");
        }

        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (newStatus != null)
            {
                _currentAgentTask.Status = newStatus.Value;
            }

            if (replacementState != null)
            {
                _currentAgentTask.Properties = replacementState;
            }

            // save the state before streaming to prevent sync issues on frontend
            _currentAgentTask = await agentTaskRepository.UpdateAgentTaskAsync(_currentAgentTask);

            // Update the deep investigation notification with current status
            await UpdateDeepInvestigationNotificationAsync(cancellationToken);

            await StreamTaskUpdateAsync(_currentAgentTask.ThreadId, _currentAgentTask, cancellationToken);

            if (newStatus != null)
            {
                await threadRepository.UpdateTaskOnThreadAsync(_currentAgentTask.ThreadId, _currentAgentTask.ToShortForm());
            }

            return GetCurrentState();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Aggregates tool calls and results from agent responses to build a history of tool usage.
    /// Extracts FunctionCallContent and corresponding FunctionResultContent (matched by CallId)
    /// from agent conversation history to prevent redundant tool calls in subsequent agents.
    /// </summary>
    /// <param name="runResult">The result from the agent execution containing conversation history</param>
    private void AggregateToolCallHistory(RunResult<AgentContext> runResult)
    {
        var newToolMessages = new List<ChatMessage>();
        var pendingFunctionCalls = new Dictionary<string, (ChatMessage message, FunctionCallContent functionCall)>();

        // Examine all messages in the conversation (both input and newly generated)
        var allMessages = runResult.Input.Concat(runResult.NewItems).ToList();

        string[] ignoredTools = [
            "ToDoWrite" // ignore the planning tool calls from other agents
        ];

        foreach (var message in allMessages)
        {
            // Look for function calls in assistant messages
            if (message.Role == ChatRole.Assistant)
            {
                var functionCalls = message.Contents.OfType<FunctionCallContent>().ToList();
                foreach (var functionCall in functionCalls)
                {
                    if (!string.IsNullOrEmpty(functionCall.CallId) && !ignoredTools.Contains(functionCall.Name))
                    {
                        pendingFunctionCalls[functionCall.CallId] = (message, functionCall);
                    }
                }
            }
            // Look for function results in tool messages
            else if (message.Role == ChatRole.Tool)
            {
                var functionResults = message.Contents.OfType<FunctionResultContent>().ToList();
                foreach (var functionResult in functionResults)
                {
                    if (!string.IsNullOrEmpty(functionResult.CallId) &&
                        pendingFunctionCalls.TryGetValue(functionResult.CallId, out var callInfo))
                    {
                        // Found matching call and result - add both to aggregated history
                        newToolMessages.Add(callInfo.message);
                        newToolMessages.Add(message);

                        // Remove from pending since we've processed it
                        pendingFunctionCalls.Remove(functionResult.CallId);

                        _toolCache.Add(callInfo.functionCall, functionResult.Result);

                        logger.LogInternalInformation(
                            "Aggregated tool call: {ToolName} with CallId: {CallId}",
                            callInfo.functionCall.Name,
                            functionResult.CallId);
                    }
                }
            }
        }

        // Add new tool messages to our aggregated history, avoiding duplicates
        foreach (var toolMessage in newToolMessages)
        {
            // Simple deduplication based on message content
            var messageJson = JsonSerializer.Serialize(toolMessage, JsonSerializerOptions.Web);
            var isDuplicate = _aggregatedToolHistory.Any(existing =>
                JsonSerializer.Serialize(existing, JsonSerializerOptions.Web) == messageJson);

            if (!isDuplicate)
            {
                _aggregatedToolHistory.Add(toolMessage);
            }
        }

        logger.LogInternalInformation(
            "Aggregated tool history now contains {Count} messages from {NewCount} new tool interactions",
            _aggregatedToolHistory.Count,
            newToolMessages.Count);
    }

    /// <summary>
    /// Injects aggregated tool call history into the chat input to help prevent redundant tool calls.
    /// The tool history is inserted after the system prompt but before the current user input.
    /// </summary>
    /// <param name="inputMessage">The current user input message</param>
    /// <returns>List of chat messages including injected tool history</returns>
    private List<ChatMessage> InjectToolCallHistory(ChatMessage inputMessage)
    {
        var chatHistory = new List<ChatMessage> { inputMessage };

        // If we have aggregated tool history, inject it before the current input
        if (_aggregatedToolHistory.Count > 0)
        {
            // Insert the aggregated tool history after the context message
            chatHistory.InsertRange(0, [.. _aggregatedToolHistory, new ChatMessage(ChatRole.User,
               $"The tool calls above have been performed previously in this investigation. " +
               "Use this information and avoid redundant tool calls and build upon previous results. IMPORTANT: Do not repeat tool calls with same parameters")]);

            logger.LogInternalInformation(
                "Injected {Count} tool history messages into agent input",
                _aggregatedToolHistory.Count);
        }

        return chatHistory;
    }

    private async Task<TResult> CallAgentAsync<TResult>(
        Agent<AgentContext> agent,
        AgentContext context,
        ChatMessage inputMessage,
        RunHooks<AgentContext> runHooks,
        bool enableDocumentSearch,
        bool injectToolHistory = true,
        Tracer? tracer = null,
        TelemetrySpan? parentSpan = null,
        CancellationToken cancellationToken = default)
    {
        if (agent.HasStructuredOutput && typeof(TResult) != agent.OutputType)
        {
            throw new InvalidOperationException("Agent has structured output but the result type is not the same as the output type.");
        }

        const int retryLimit = 5; // Increased retry limit for rate limiting
        var threadId = context.ThreadId.ToString();

        for (var i = 0; i < retryLimit; i++)
        {
            try
            {
                if (enableDocumentSearch)
                {
                    var docs = new List<SearchDocument>();
                    string query = await DocumentRetrieval.GenerateSearchQuery(
                        chatClientProvider.DefaultModel,
                        [inputMessage],
                        "How to investigate this issue?",
                        logger);
                    TelemetrySpan? searchSpan = null;

                    if (parentSpan != null && tracer != null)
                    {
                        searchSpan = tracer.StartActiveSpan("retrieval_local_documents", SpanKind.Internal, parentSpan);
                        searchSpan.SetAttribute(TraceAttribute.ThreadId, threadId);
                        searchSpan.SetAttribute(TraceAttribute.OperationName, "retrieval.local.documents");
                    }

                    // If the agent is a 1P agent, retrieve the prompts of subagents from the local store
                    if (_is1PAgent)
                    {
                        var docsFromLocal = await RetrieveDocumentsFromLocalStore(query);
                        searchSpan?.SetAttribute("search.results.count", docsFromLocal.Count.ToString());
                        searchSpan?.SetAttribute("search.results", JsonSerializer.Serialize(docsFromLocal));
                        searchSpan?.End();
                        docs.AddRange(docsFromLocal);
                    }

                    docs.AddRange(await RetrieveDocumentsFromRegionalStore(query, threadId, parentSpan));

                    string msg = inputMessage.Text;
                    msg += $"""
                        ---
                        And Here are some relevant documents that can be referenced:
                        <Documents>
                        {string.Join("\n", docs.Select(d => $"{d.Content}"))}
                        </Documents>
                        """;
                    inputMessage = new ChatMessage(inputMessage.Role, msg);
                }

                var runConfig = new RunConfig
                {
                    ChatClient = chatClientProvider.DefaultModel,
                    LoggerFactory = loggerFactory,
                };

                // Inject tool call history into the chat input
                var chatHistory = injectToolHistory ? InjectToolCallHistory(inputMessage) : [inputMessage];

                var runResult = await Runner.RunAsync(
                    startingAgent: agent,
                    input: chatHistory,
                    config: runConfig,
                    context: context,
                    maxTurns: 100,
                    hooks: runHooks,
                    allowParallelToolCalls: true,
                    toolResultCache: _toolCache,
                    cancellationToken: cancellationToken
                );

                while (runResult.ManualToolCalls != null && runResult.ManualToolCalls.Count > 0)
                {
                    List<ManualToolCallResult> results = [];

                    foreach (var toolCall in runResult.ManualToolCalls)
                    {
                        // skip tool calls that require approval or write action
                        if (toolCall.Tool.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>() is not null
                            || toolCall.Tool.UnderlyingMethod?.GetCustomAttribute<WriteActionAttribute>() is not null)
                        {
                            results.Add(new ManualToolCallResult
                            {
                                FunctionCall = toolCall.FunctionCall,
                                Output = "Error: cannot call tool that requires approval or write action",
                            });
                        }

                        var toolOutput = await InvokeToolWithErrorHandlingAsync(toolCall, context, cancellationToken);
                        results.Add(new ManualToolCallResult
                        {
                            FunctionCall = toolCall.FunctionCall,
                            Output = toolOutput,
                        });
                    }

                    runResult = await Runner.ResumeFromManualToolsAsync(
                        previousResult: runResult,
                        manualToolResults: results,
                        config: runConfig,
                        context: context,
                        hooks: runHooks,
                        allowParallelToolCalls: true,
                        toolResultCache: _toolCache,
                        cancellationToken: cancellationToken
                    );
                }

                // Aggregate tool calls and results from this agent execution
                AggregateToolCallHistory(runResult);

                if (runResult.Output is TResult result)
                {
                    return result;
                }

                logger.LogInternalWarning("Agent {AgentName} returned invalid output type. Expected: {ExpectedType}. Actual content: {Output}",
                    agent.Name, typeof(TResult), runResult.Output);
                throw new InvalidOperationException("Invalid output from agent.");
            }
            catch (Exception e) when (IsRateLimitException(e))
            {
                logger.LogInternalWarning("Rate limit encountered on attempt {Attempt}/{MaxAttempts}: {Error}", i + 1, retryLimit, e.Message);

                if (i == retryLimit - 1)
                {
                    logger.LogInternalError("Max retry attempts reached for rate limiting.");
                    throw;
                }

                // Exponential backoff with jitter for rate limits - start with longer delays
                var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, i + 3)); // Start at 8 seconds
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 2000)); // 0-2 second jitter
                var totalDelay = baseDelay + jitter;

                // Cap the delay at 2 minutes
                if (totalDelay > TimeSpan.FromMinutes(2))
                {
                    totalDelay = TimeSpan.FromMinutes(2);
                }

                logger.LogInternalInformation("Waiting {Delay} before retry due to rate limiting.", totalDelay);
                await Task.Delay(totalDelay, cancellationToken);
            }
            catch (Exception e) when (IsTransientException(e))
            {
                logger.LogInternalWarning("Transient error on attempt {Attempt}/{MaxAttempts}: {Error}", i + 1, retryLimit, e.Message);

                if (i == retryLimit - 1)
                {
                    throw;
                }

                // Exponential backoff for transient errors
                var delay = TimeSpan.FromSeconds(Math.Pow(2, i) + Random.Shared.NextDouble());
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Retry exceeded");
    }

    private async Task<ICollection<SearchDocument>> RetrieveDocumentsFromLocalStore(string query)
    {
        return await rcaAgentsStore.SearchAsync(query, 3, extendedAgentService).ToListAsync();
    }

    private async Task<IEnumerable<SearchDocument>> RetrieveDocumentsFromRegionalStore(string query, string threadId, TelemetrySpan? parentSpan = null)
    {
        var results = await searchHelper.SearchAsync(query, SearchRequest.TypeDocument, false, parentSpan, threadId);
        return results;
    }

    private async Task<object?> InvokeToolWithErrorHandlingAsync(
        ManualToolCall toolCall,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            string[] ignoredToolsForCache = [
                "ToDoWrite" // don't cache the planning tool calls from other agents
            ];

            if (!ignoredToolsForCache.Contains(toolCall.FunctionCall.Name) && _toolCache.TryGetValue(toolCall.FunctionCall, out var cachedResult))
            {
                logger.LogInternalInformation("Cache hit for tool: {ToolName}", toolCall.Tool.Name);
                return cachedResult;
            }

            // Set agent task context for tool result routing
            Core.ToolStatic.AsyncLocalAgentTaskId.Value = _currentAgentTask?.Id;

            logger.LogInternalInformation("Set AsyncLocalAgentTaskId to {AgentTaskId} for tool {ToolName}",
                _currentAgentTask?.Id, toolCall.Tool.Name);

            Core.ToolStatic.AsyncLocalThreadId.Value = context.ThreadId;
            Core.ToolStatic.AsyncLocalCancellationToken.Value = cancellationToken;
            var result = await toolCall.Tool.InvokeAsync(new AIFunctionArguments(toolCall.FunctionCall.Arguments), cancellationToken);

            _toolCache.Add(toolCall.FunctionCall, result);
            logger.LogInternalInformation("Cached result for tool: {ToolName}", toolCall.Tool.Name);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error while calling tool {ToolName}", toolCall.Tool!.Name);
            return GetErrorMessage(toolCall.FunctionCall, ex);
        }
        finally
        {
            // Clear the context after tool execution
            logger.LogInternalInformation("Clearing AsyncLocalAgentTaskId after tool {ToolName} execution", toolCall.Tool.Name);
            Core.ToolStatic.AsyncLocalAgentTaskId.Value = null;
            Core.ToolStatic.AsyncLocalInvestigationStepContext.Value = null;
        }
    }

    private static string GetErrorMessage(FunctionCallContent functionCall, Exception ex)
    {
        var message = $"Error: Function {functionCall.Name} failed, {ex.Message}";

        if (ex.InnerException != null)
        {
            message += $" | Inner exception: {ex.InnerException.Message}";
        }

        return message;
    }

    private async Task<List<HypothesisTreeItem>> GenerateHypotheses(
        string incidentDescription,
        string investigationSummary,
        string? validatedHypothesis,
        IList<string> existingHypotheses,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Generating hypotheses for incident description.");

        var hypothesisGenerationAgent = IncidentInvestigationAgents.CreateHypothesisGenerationAgent(_llmDeploymentName, existingHypotheses);
        string message = $"""
            <incident_description>
            {incidentDescription}
            </incident_description>

            <initial_investigation_summary>
            {investigationSummary}
            </initial_investigation_summary>
            """;

        if (!string.IsNullOrEmpty(validatedHypothesis))
        {
            message += $"""

                <previous_validated_hypothesis>
                - {validatedHypothesis}
                </previous_validated_hypothesis>

                Please dig deeper into the hypothesis above and make more detailed hypotheses in the scope of it. Don't make any assumptions out of the scope.
                """;
        }

        var hypotheses = await CallAgentAsync<List<HypothesisGenerationResult>>(
            hypothesisGenerationAgent,
            context,
            new ChatMessage(ChatRole.User, message),
            runHooks,
            true,
            injectToolHistory: false,
            tracer: tracer,
            parentSpan: currentStepSpan,
            cancellationToken: cancellationToken);

        var result = hypotheses.Select(h => new HypothesisTreeItem
        {
            Id = Guid.NewGuid(),
            Title = h.Title,
            Description = h.Content,
            Children = [],
            Status = HypothesisStatus.Pending,
            Steps = [],
            ParentHypothesisDescription = validatedHypothesis ?? string.Empty
        }).ToList();

        logger.LogInternalInformation("Generated {HypothesisCount} hypotheses.", result.Count);
        return result;
    }

    private async Task<HypothesisValidationResult> ValidateHypothesisAsync(
        string incidentDescription,
        string investigationSummary,
        HypothesisTreeItem hypothesis,
        IncidentInvestigationTaskProperties state,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        CancellationToken cancellationToken)
    {
        string currentHypothesis = hypothesis.Description;
        string validatedHypothesis = hypothesis.ParentHypothesisDescription;
        logger.LogInternalInformation("Validating hypothesis: {Hypothesis}", currentHypothesis);

        var toolSelectionAgent = IncidentInvestigationAgents.CreateHypothesisValidationToolSelectionAgent(toolFactory, incidentDescription, investigationSummary, toolSubset, _llmDeploymentName);

        var toolNames = await CallAgentAsync<List<string>>(
            toolSelectionAgent,
            context,
            new ChatMessage(ChatRole.User, currentHypothesis),
            runHooks,
            true,
            injectToolHistory: false,
            tracer: tracer,
            parentSpan: currentStepSpan,
            cancellationToken: cancellationToken);

        ValidateAndAddRequiredTools(toolNames);

        var inputMessage = new ChatMessage(ChatRole.User, $"""
            Please validate the following hypothesis:

            {currentHypothesis}
        """);

        // trying out single agent flow for GPT-5
        if (_llmDeploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase))
        {
            return await ValidateHypothesisWithGpt5Async(incidentDescription, investigationSummary, hypothesis, state, context, runHooks, currentStepSpan, toolNames, inputMessage, cancellationToken);
        }
        else
        {
            return await ValidateHypothesisWithGpt4Async(incidentDescription, investigationSummary, validatedHypothesis, hypothesis, state, context, runHooks, currentStepSpan, toolNames, inputMessage, cancellationToken);
        }

    }

    private async Task<HypothesisValidationResult> ValidateHypothesisWithGpt4Async(
        string incidentDescription,
        string investigationSummary,
        string validatedHypothesis,
        HypothesisTreeItem hypothesis,
        IncidentInvestigationTaskProperties state,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        List<string> toolNames,
        ChatMessage inputMessage,
        CancellationToken cancellationToken)
    {
        Guid currentHypothesisId = hypothesis.Id;
        string currentHypothesis = hypothesis.Description;

        // start by generating a plan
        var planningAgent = IncidentInvestigationAgents.CreateHypothesisValidationPlanningAgent(
            (toolFactory as ToolFactory<AgentContext>)!.FetchToolInfoForToolNames(toolNames),
            incidentDescription,
            investigationSummary,
            validatedHypothesis,
            _llmDeploymentName);

        var plan = await CallAgentAsync<HypothesisValidationPlanOutput>(
            planningAgent,
            context,
            inputMessage,
            runHooks,
            true,
            injectToolHistory: true,
            tracer: tracer,
            parentSpan: currentStepSpan,
            cancellationToken: cancellationToken);

        // execute plan step by step
        List<HypothesisStep> completedSteps = [];

        foreach (var step in plan.Steps)
        {
            // Set hypothesis validation step context for tool result routing
            Core.ToolStatic.AsyncLocalInvestigationStepContext.Value = new InvestigationStepContext(
                "HypothesisValidation",
                step.Title,
                currentHypothesisId
            );

            // todo: test selecting tool names per-step instead of once at the beginning
            var stepExecutionAgent = IncidentInvestigationAgents.CreateHypothesisValidationPlanExecutionAgent(
                toolNames,
                incidentDescription,
                investigationSummary,
                validatedHypothesis,
                currentHypothesis,
                plan,
                completedSteps,
                _is1PAgent,
                _llmDeploymentName);

            var stepInputMessage = $"""
                Execute the following step of the provided plan:

                <step>
                # Title
                {step.Title}

                # Description
                {step.Description}
                </step>
                """;

            var stepExecutionResult = await CallAgentAsync<HypothesisPlanStepExecutionResult>(
                stepExecutionAgent,
                context,
                new ChatMessage(ChatRole.User, stepInputMessage),
                runHooks,
                true,
                tracer: tracer,
                parentSpan: currentStepSpan,
                cancellationToken: cancellationToken);

            // get databse item for the step to get tool executions
            HypothesisStep? databaseStep = null;
            if (_currentAgentTask != null)
            {
                databaseStep = await agentTaskToolResultHelper.FindExistingHypothesisStepAsync(
                    _currentAgentTask.Id,
                    _currentAgentTask.ThreadId,
                    currentHypothesisId,
                    step.Title);
            }

            var item = new HypothesisStep
            {
                Summary = step.Title,
                Details = stepExecutionResult.Summary,
                ToolExecutions = databaseStep?.ToolExecutions ?? []
            };

            completedSteps.Add(item);
            hypothesis.Steps.Add(item);
            await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);

            if (!stepExecutionResult.NeedContinue)
            {
                break;
            }
        }

        // summarize plan execution to determine validation state
        var summarizationAgent = IncidentInvestigationAgents.CreateHypothesisValidationPlanSummaryAgent(
            incidentDescription,
            investigationSummary,
            validatedHypothesis,
            currentHypothesis,
            completedSteps,
            _llmDeploymentName);

        var result = await CallAgentAsync<HypothesisResultSummaryOutput>(
            summarizationAgent,
            context,
            new ChatMessage(ChatRole.User, "Analyze the validation steps and provide your result"),
            runHooks,
            false,
            injectToolHistory: false,
            tracer: tracer,
            parentSpan: currentStepSpan,
            cancellationToken: cancellationToken);

        logger.LogInternalInformation("Hypothesis validation result: Hypothesis: {Hypothesis}, Status: {Status}, Reasoning: {Reasoning}",
            currentHypothesis, result.Status, result.Reasoning);

        var validationResult = new HypothesisValidationResult
        {
            Status = result.Status,
            Steps = completedSteps,
            IsRootCause = false,
            Reasoning = result.Reasoning
        };

        // TODO: isRootCause functionality removed
        // logger.LogInternalInformation("Hypothesis validation result: {Status}, IsRootCause: {IsRootCause}",
        //     validationResult.Status, validationResult.IsRootCause);
        logger.LogInternalInformation("Hypothesis validation result: {Status}",
            validationResult.Status);
        return validationResult;
    }

    private async Task<HypothesisValidationResult> ValidateHypothesisWithGpt5Async(
        string incidentDescription,
        string investigationSummary,
        HypothesisTreeItem hypothesis,
        IncidentInvestigationTaskProperties state,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        List<string> toolNames,
        ChatMessage inputMessage,
        CancellationToken cancellationToken)
    {
        var validationAgent = IncidentInvestigationAgents.CreateHypothesisValidationAgentV2(
                        agentFactory,
                        toolNames,
                        incidentDescription,
                        investigationSummary
                    );

        Core.ToolStatic.AsyncLocalInvestigationStepContext.Value = new InvestigationStepContext(
                "HypothesisValidation",
                null,
                hypothesis.Id
            );

        var resultV2 = await CallAgentAsync<HypothesisValidationResultV2>(
            validationAgent,
            context,
            inputMessage,
            runHooks,
            true,
            tracer: tracer,
            parentSpan: currentStepSpan,
            cancellationToken: cancellationToken);

        // Use the steps that were streamed in real-time via ReportStepCompletion tool calls
        // If no steps were captured via tool calls, fall back to the original method
        IList<HypothesisStep> finalSteps;

        if (hypothesis.Steps.Count > 0)
        {
            logger.LogInternalInformation(
                "Using {StreamedStepCount} steps captured via ReportStepCompletion tool calls",
                hypothesis.Steps.Count);
            finalSteps = hypothesis.Steps;
        }
        else
        {
            logger.LogInternalInformation(
                "No steps captured via ReportStepCompletion, falling back to original method with {OriginalStepCount} steps",
                resultV2.Steps.Count);

            // Fallback to original method if no steps were reported via tool
            var hypSteps = await Task.WhenAll(resultV2.Steps.Select(async step =>
            {
                // get databse item for the step to get tool executions
                HypothesisStep? databaseStep = null;
                if (_currentAgentTask != null)
                {
                    databaseStep = await agentTaskToolResultHelper.FindExistingHypothesisStepAsync(
                        _currentAgentTask.Id,
                        _currentAgentTask.ThreadId,
                        hypothesis.Id,
                        step.Title);
                }

                var hypStep = new HypothesisStep
                {
                    Summary = step.Title,
                    Details = step.Description,
                    ToolExecutions = databaseStep?.ToolExecutions ?? []
                };

                return hypStep;
            }));
            finalSteps = hypSteps.ToList();
        }

        var validationResultV2 = new HypothesisValidationResult
        {
            Status = resultV2.Status,
            Steps = finalSteps,
            IsRootCause = false,
            Reasoning = resultV2.Reasoning
        };

        logger.LogInternalInformation("Hypothesis validation result: {Status}",
            validationResultV2.Status);

        return validationResultV2;
    }

    private async Task GenerateSingleValidHypothesisConclusion(
        HypothesisTreeItem validHypothesis,
        IncidentInvestigationTaskInputData inputData,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Generating conclusion for single valid hypothesis: {HypothesisTitle}", validHypothesis.Title);
        var conclusionAgent = IncidentInvestigationAgents.CreateConclusionAgent(_llmDeploymentName);

        var state = GetCurrentState();

        var message = $"""
            ## Incident Investigation Conclusion

            **Incident Description:**
            {inputData.IncidentDescription}

            **Initial Investigation Summary:**
            {state.InitialInvestigation.ToString()}

            **Investigation Outcome:**
            The investigation has identified a single validated hypothesis that can be treated as the likely root cause.

            **Validated Hypothesis (Likely Root Cause):**
            - **Title:** {validHypothesis.Title}
            - **Description:** {validHypothesis.Description}
            - **Status:** {validHypothesis.Status}

            Please generate a conclusion that treats this validated hypothesis as the likely root cause and provide a detailed summary with actionable recommendations.
            """;

        var conclusion = await CallAgentAsync<ConclusionResult>(
            conclusionAgent,
            context,
            new(ChatRole.User, message),
            runHooks,
            false,
            injectToolHistory: false,
            tracer: tracer,
            parentSpan: currentStepSpan,
            cancellationToken: cancellationToken
        );

        state.Conclusion.Title = conclusion.Title;
        state.Conclusion.Summary = conclusion.Summary;

        logger.LogInternalInformation("Conclusion generated: {ConclusionTitle}", conclusion.Title);
    }

    private async Task GenerateMultipleValidHypothesesConclusion(
        List<HypothesisTreeItem> validHypotheses,
        IncidentInvestigationTaskInputData inputData,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Generating conclusion for multiple valid hypotheses: {HypothesisCount}", validHypotheses.Count);
        var conclusionAgent = IncidentInvestigationAgents.CreateConclusionAgent(_llmDeploymentName);

        var state = GetCurrentState();

        var hypothesesDescription = string.Join("\n", validHypotheses.Select((vh, index) =>
            $"- **Hypothesis {index + 1}:** {vh.Title}\n  - **Description:** {vh.Description}\n  - **Status:** {vh.Status}"));

        var message = $"""
            ## Incident Investigation Conclusion

            **Incident Description:**
            {inputData.IncidentDescription}

            **Initial Investigation Summary:**
            {state.InitialInvestigation.ToString()}

            **Investigation Outcome:**
            The investigation has identified multiple validated hypotheses that could be contributing factors.

            **Validated Hypotheses ({validHypotheses.Count}):**
            {hypothesesDescription}

            Please generate a conclusion that addresses multiple potential contributing factors and provide a detailed summary with prioritized recommendations for each hypothesis.
            """;

        var conclusion = await CallAgentAsync<ConclusionResult>(
            conclusionAgent,
            context,
            new(ChatRole.User, message),
            runHooks,
            false,
            injectToolHistory: false,
            tracer: tracer,
            parentSpan: currentStepSpan,
            cancellationToken: cancellationToken
        );

        state.Conclusion.Title = conclusion.Title;
        state.Conclusion.Summary = conclusion.Summary;

        logger.LogInternalInformation("Conclusion generated for multiple hypotheses: {ConclusionTitle}", conclusion.Title);
    }

    private async Task GenerateInconclusiveConclusion(
        IncidentInvestigationTaskInputData inputData,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        List<HypothesisTreeItem> allInvestigatedHypotheses,
        CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Generating conclusion for inconclusive investigation.");
        var conclusionAgent = IncidentInvestigationAgents.CreateConclusionAgent(_llmDeploymentName);

        var state = GetCurrentState();

        var hypothesesDescription = string.Join("\n\n", allInvestigatedHypotheses.Select((vh, index) =>
            $"- **Hypothesis {index + 1}:** {vh.Title}\n  - **Description:** {vh.Description}\n  - **Status:** {vh.Status}"));

        var message = $"""
            ## Incident Investigation Conclusion

            **Incident Description:**
            {inputData.IncidentDescription}

            **Initial Investigation Summary:**
            {state.InitialInvestigation.ToString()}

            **Investigation Outcome:**
            The investigation was unable to identify validated hypotheses or determine a root cause for this incident.

            **Investigated Hypotheses ({allInvestigatedHypotheses.Count}):**
            {hypothesesDescription}

            Please generate a conclusion that acknowledges the investigation was inconclusive and provide recommendations for further investigation steps, additional data collection, or escalation procedures.
            """;

        var conclusion = await CallAgentAsync<ConclusionResult>(
            conclusionAgent,
            context,
            new(ChatRole.User, message),
            runHooks,
            false,
            injectToolHistory: false,
            tracer: tracer,
            parentSpan: currentStepSpan,
            cancellationToken: cancellationToken
        );

        state.Conclusion.Title = conclusion.Title;
        state.Conclusion.Summary = conclusion.Summary;

        logger.LogInternalInformation("Conclusion generated for inconclusive investigation: {ConclusionTitle}", conclusion.Title);
    }

    private async Task StreamTaskUpdateAsync(Guid threadId, AgentTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            // temp hack to send the status reasoning in the hypothesis details

            // create copy of task record
            // var taskToStream = task with { };

            // var hypotheses = (taskToStream.Properties as IncidentInvestigationTaskProperties)?.FormingHypothesis.Hypotheses;

            // if (hypotheses != null)
            // {
            //     foreach (var hypothesis in hypotheses)
            //     {
            //         if (string.IsNullOrEmpty(hypothesis.Reasoning))
            //         {
            //             continue;
            //         }

            //         // prepend reasoning to description with markdown formatting
            //         hypothesis.Description = $"""
            //         ## Decision Reasoning
            //         {hypothesis.Reasoning}

            //         {hypothesis.Description}
            //         """;
            //     }
            // }

            // var jsonUpdate = JsonSerializer.Serialize(taskToStream, JsonSerializerOptions.Web);
            var jsonUpdate = JsonSerializer.Serialize(task, JsonSerializerOptions.Web);

            await outboundCommunicationService.AppendAgentTaskUpdate(
                threadId,
                jsonUpdate,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogInternalWarning(ex, "Failed to stream task update for thread {ThreadId}", threadId);
        }
    }

    /// <summary>
    /// Clears all concurrent collections by creating new instances.
    /// ConcurrentBag doesn't have a Clear method, so we need to recreate the collections.
    /// </summary>
    private void ClearConcurrentCollections()
    {
        // Since ConcurrentBag doesn't have Clear(), we need to drain them
        while (_allInvestigatedHypotheses.TryTake(out _)) { }
        while (_finalValidatedHypotheses.TryTake(out _)) { }
        while (_allHypothesesTitles.TryTake(out _)) { }
    }

    /// <summary>
    /// Handles ReportStepCompletion tool calls to provide real-time step streaming.
    /// </summary>
    private async Task HandleReportStepCompletionToolCallAsync(RunContextWrapper<AgentContext> runContext, Agent<AgentContext> agent, FunctionCallContent functionCall, AIFunction tool, IEnumerable<KeyValuePair<string, object?>>? input)
    {
        if (tool.Name == "ReportStepCompletion")
        {
            try
            {
                // Extract parameters directly from input
                var stepTitle = ExtractToolParameter<string>(input, "stepTitle") ?? "Unknown Step";
                var summary = ExtractToolParameter<string>(input, "summary") ?? "";
                var status = ExtractToolParameter<string>(input, "status") ?? "Success";
                var errorMessage = ExtractToolParameter<string>(input, "errorMessage");

                // Find the current hypothesis ID from AsyncLocal context
                var stepContext = Core.ToolStatic.AsyncLocalInvestigationStepContext.Value;
                if (stepContext?.HypothesisId == null)
                {
                    logger.LogInternalWarning("No hypothesis ID found in investigation context for ReportStepCompletion call");
                    return;
                }

                var contextHypothesisId = stepContext.HypothesisId.Value;

                // Create hypothesis step
                var hypothesisStep = new HypothesisStep
                {
                    Summary = stepTitle,
                    Details = summary,
                };

                // Add error message if present
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    hypothesisStep.Details += $"\n\nError: {errorMessage}";
                }

                // Get current state and find the hypothesis to update
                var state = GetCurrentState();
                var hypothesis = FindHypothesisInState(state, contextHypothesisId);
                if (hypothesis != null)
                {
                    hypothesis.Steps.Add(hypothesisStep);
                    await SaveStateAndStreamUpdateAsync(state);

                    logger.LogInternalInformation(
                        "Step streamed in real-time: {StepTitle} - {Status}",
                        stepTitle, status);
                }
                else
                {
                    logger.LogInternalWarning("Could not find hypothesis {HypothesisId} in current state", contextHypothesisId);
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalWarning(ex, "Failed to process step completion for tool call");
            }
        }
    }

    /// <summary>
    /// Finds a hypothesis in the state by its ID, searching recursively through the hypothesis tree.
    /// </summary>
    private HypothesisTreeItem? FindHypothesisInState(IncidentInvestigationTaskProperties state, Guid hypothesisId)
    {
        if (state.FormingHypothesis?.Hypotheses == null)
            return null;

        return FindHypothesisRecursive(state.FormingHypothesis.Hypotheses, hypothesisId);
    }

    /// <summary>
    /// Recursively searches for a hypothesis by ID in the hypothesis tree.
    /// </summary>
    private HypothesisTreeItem? FindHypothesisRecursive(IList<HypothesisTreeItem> hypotheses, Guid hypothesisId)
    {
        foreach (var hypothesis in hypotheses)
        {
            if (hypothesis.Id == hypothesisId)
                return hypothesis;

            var found = FindHypothesisRecursive(hypothesis.Children, hypothesisId);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Helper method to extract parameters from tool input for the ReportStepCompletion hook.
    /// </summary>
    private T? ExtractToolParameter<T>(IEnumerable<KeyValuePair<string, object?>>? input, string parameterName)
    {
        if (input == null) return default;

        var parameter = input.FirstOrDefault(kvp => kvp.Key == parameterName);
        if (parameter.Value == null) return default;

        return ConvertParameterValue<T>(parameter.Value, parameterName);
    }


    /// <summary>
    /// Converts a parameter value to the specified type.
    /// </summary>
    private T? ConvertParameterValue<T>(object value, string parameterName)
    {
        try
        {
            if (value is T directValue)
                return directValue;

            if (value is JsonElement jsonElement)
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            logger.LogInternalWarning(ex, "Failed to extract tool parameter {ParameterName} of type {Type}", parameterName, typeof(T).Name);
            return default;
        }
    }

    /// <summary>
    /// Creates a deep investigation approval with required scopes (tokens exchanged at infra layer)
    /// </summary>
    /// <param name="threadId">The thread ID for the approval</param>
    /// <param name="agentTaskId">The agent task ID requiring approval</param>
    /// <param name="agentContextId">The agent context ID for the approval</param>
    /// <returns>The created approval object</returns>
    private async Task<Approval> CreateDeepInvestigationApprovalAsync(Guid threadId, Guid agentTaskId, Guid agentContextId)
    {
        try
        {
            // gather all scopes needed for deep investigation
            var deepInvestigationScopes = string.Join(",", new[]
            {
                Constants.ArmOboTokenScope,
                Constants.AksOboTokenScope,
                Constants.AkvOboTokenScope,
                Constants.StorageOboTokenScope,
                Constants.SynapseOboTokenScope,
                Constants.AppInsightsTokenScope
            });

            var approval = new Approval(
                Id: Guid.NewGuid(),
                ThreadId: threadId.ToString(),
                Title: "Deep Investigation Authorization",
                Description: "Grant elevated permissions to enable comprehensive analysis. If not approved within 10 minutes or declined, the investigation continues with limited permissions only.",
                Status: ApprovalDecision.Pending,
                CreatedTimestamp: DateTime.UtcNow,
                DecisionTimestamp: null,
                OrchestrationId: null,
                AgentContextId: agentContextId,
                DecisionUser: null,
                OboToken: null,
                OboTokenScope: deepInvestigationScopes
            );

            await threadRepository.CreateApprovalAsync(approval);

            logger.LogInternalInformation("Created deep investigation approval {ApprovalId} for agent task {AgentTaskId} with scopes {Scopes}",
                approval.Id, agentTaskId, deepInvestigationScopes);

            return approval;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to create deep investigation approval for agent task {AgentTaskId}", agentTaskId);
            throw; // Re-throw since this is critical for the approval flow
        }
    }

    /// <param name="approvalId">The approval ID to wait for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if approved, false if cancelled or timeout</returns>
    private async Task<bool> WaitForApprovalAsync(Guid approvalId, CancellationToken cancellationToken)
    {
        if (_currentAgentTask == null)
        {
            logger.LogInternalWarning("Cannot wait for approval: no current agent task");
            return false;
        }

        var timeout = TimeSpan.FromMinutes(10); // 10-minute timeout for user approval
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var approval = await approvalService.GetApproval(_currentAgentTask.ThreadId, approvalId.ToString());

                if (approval?.Status == ApprovalDecision.Approved)
                {
                    logger.LogInternalInformation("Deep investigation approval {ApprovalId} approved by {User}",
                        approvalId, approval.DecisionUser?.DisplayName ?? "Unknown");
                    return true;
                }
                else if (approval?.Status == ApprovalDecision.Cancelled)
                {
                    logger.LogInternalInformation("Deep investigation approval {ApprovalId} cancelled by {User}",
                        approvalId, approval.DecisionUser?.DisplayName ?? "Unknown");
                    return false;
                }

                // Poll every 2 seconds
                await Task.Delay(2000, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogInternalWarning(ex, "Error checking approval status for {ApprovalId}", approvalId);
                await Task.Delay(5000, cancellationToken); // Wait longer on error
            }
        }

        logger.LogInternalWarning("Deep investigation approval {ApprovalId} timed out after {TimeoutMinutes} minutes",
            approvalId, timeout.TotalMinutes);

        // Mark approval as cancelled due to timeout
        try
        {
            var approvalForTimeout = await approvalService.GetApproval(_currentAgentTask.ThreadId, approvalId.ToString());

            await approvalService.SubmitApprovalDecision(
                approvalId.ToString(),
                "system",
                ApprovalDecision.Cancelled,
                _currentAgentTask.ThreadId,
                null, // No OBO token for timeout
                approvalForTimeout?.OboTokenScope  // Use the original scope from the approval
            );
            logger.LogInternalInformation("Marked approval {ApprovalId} as cancelled due to timeout", approvalId);
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to update approval {ApprovalId} status to cancelled after timeout", approvalId);
        }

        return false;
    }

    /// <summary>
    /// Checks if the current investigation was triggered from chat vs incident handler.
    /// </summary>
    /// <returns>True if triggered from chat, false if from incident handler</returns>
    private async Task<bool> IsChatTriggeredInvestigationAsync()
    {
        if (_currentAgentTask == null)
        {
            return false;
        }

        try
        {
            var thread = await threadRepository.GetThreadAsync(_currentAgentTask.ThreadId);

            // Determine if this investigation was triggered from chat vs incident handler
            return thread?.Source != ThreadSource.Alert;
        }
        catch (Exception ex)
        {
            logger.LogInternalWarning(ex, "Failed to determine investigation trigger source for thread {ThreadId}", _currentAgentTask.ThreadId);
            return false;
        }
    }

    /// <summary>
    /// Sets up the approval context for OBO token usage if we have an approved deep investigation.
    /// When set, all subsequent tool calls will automatically use the user's OBO tokens instead of managed identity.
    /// </summary>
    /// <param name="threadId">The thread ID</param>
    /// <param name="agentTaskId">The agent task ID</param>
    /// <returns>True if OBO token context was set up successfully, false if using managed identity</returns>
    private async Task<bool> SetupApprovalContextAsync(Guid threadId, Guid agentTaskId)
    {
        try
        {
            if (_currentAgentTask?.DeepInvestigationApprovalId != null)
            {
                var approval = await approvalService.GetApproval(threadId, _currentAgentTask.DeepInvestigationApprovalId.Value.ToString());

                if (approval?.Status == ApprovalDecision.Authorized && !string.IsNullOrEmpty(approval.OboToken))
                {
                    logger.LogInternalInformation("Setting up OBO token context for deep investigation. Approval: {ApprovalId}, Scopes: {Scopes}",
                        approval.Id, approval.OboTokenScope);

                    var approvalContext = new ApprovalContext(
                        ThreadId: threadId,
                        ApprovalId: approval.Id,
                        UseOboToken: true
                    );
                    Core.ToolStatic.AsyncLocalApprovalContext.Value = approvalContext;
                    return true;
                }
                else if (approval?.Status == ApprovalDecision.Pending)
                {
                    logger.LogInternalInformation("Deep investigation approval is still pending - using managed identity for now");
                }
                else if (approval?.Status == ApprovalDecision.Cancelled)
                {
                    logger.LogInternalInformation("Deep investigation approval was cancelled - using managed identity");
                }
                else
                {
                    logger.LogInternalInformation("Deep investigation approval not yet approved or scope not available - using managed identity");
                }
            }
            else
            {
                logger.LogInternalInformation("No deep investigation approval found - using managed identity for investigation");
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogInternalWarning(ex, "Failed to setup approval context - continuing with managed identity");
            return false;
        }
    }

    #region Rate Limiting Helper Methods

    /// <summary>
    /// Determines if an exception is a rate limit exception.
    /// </summary>
    private static bool IsRateLimitException(Exception exception)
    {
        return exception.Message.Contains("HTTP 429") ||
               exception.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("throttle", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if an exception is a transient exception that should be retried.
    /// </summary>
    private static bool IsTransientException(Exception exception)
    {
        return exception.Message.Contains("HTTP 5") || // 5xx errors
               exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               exception is TaskCanceledException ||
               exception is HttpRequestException;
    }


    #endregion
}
