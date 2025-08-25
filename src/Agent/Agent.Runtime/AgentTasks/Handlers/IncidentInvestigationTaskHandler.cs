// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.AgentTasks.Agents;
using Agent.Runtime.Reasoning;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Agent.Runtime.AgentTasks.Handlers;

public sealed class IncidentInvestigationTaskHandler(
    ILoggerFactory loggerFactory,
    ILogger<IncidentInvestigationTaskHandler> logger,
    IAgentTasksRepository agentTaskRepository,
    IThreadRepository threadRepository,
    IChatClient chatClient,
    IToolFactory<AgentContext> toolFactory,
    IAgentOutboundCommunicationService outboundCommunicationService,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    SearchHelper searchHelper,
    Tracer tracer,
    IConfiguration configuration
) : IAgentTaskHandler
{
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private AgentTask? _currentAgentTask;
    private readonly ConcurrentDictionary<string, object?> _toolCache = new();
    private readonly Lazy<AgentTaskLocalStore?> _rcaAgentsStore = new(() =>
    {
        var agentTasksEnabled = configuration.GetValue<bool>("AppSettings:Core:AgentTasksEnabled", false);
        if (!agentTasksEnabled)
        {
            logger.LogInternalInformation("Agent tasks are disabled, skipping RCA agents store initialization");
            return null;
        }

        logger.LogInternalInformation("Initializing RCA agents store");
        return new AgentTaskLocalStore(["AgentsV2\\ACA-FirstParty\\"], embeddingGenerator);
    });
    private readonly List<ChatMessage> _aggregatedToolHistory = new();
    private List<string>? toolSubset = null;
    private readonly bool is1PAgent = Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") == "ACAAgent";

    /// <summary>
    /// Gets the RCA agents store if agent tasks are enabled, otherwise returns null.
    /// </summary>
    private AgentTaskLocalStore? RcaAgentsStore => _rcaAgentsStore.Value;

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

            // Send deep investigation notification immediately when investigation starts
            await SendDeepInvestigationNotificationAsync(agentTask.ThreadId, agentTask.Id);

            // Save the agent task to the thread document immediately when investigation starts
            await threadRepository.UpdateTaskOnThreadAsync(agentTask.ThreadId, agentTask.ToShortForm());
            logger.LogInternalInformation("Agent task {TaskId} saved to thread {ThreadId}", agentTask.Id, agentTask.ThreadId);

            if (is1PAgent)
            {
                var allAgents = YamlHelper.LoadAgentsFromYamlDirectories(
                    new List<string> { Path.Combine("AgentsV2", "ACA-FirstParty") },
                    "RCA"
                );
                toolSubset = allAgents.SelectMany(agent => agent.Tools)
                    .Distinct()
                    .ToList();

                logger.LogInternalInformation($"Successfully loaded {allAgents.Count} agents");

                // Common tools
                toolSubset.AddRange(["OneLinerToRCA", "GetASIPageForManagedCluster", "GetASIPageForContainerAppJob", "GetASIPageForManagedEnvironment", "GetASIPageForRevision", "PlotTimeSeriesData", "HandoffBack"]);
                logger.LogInternalInformation($"Complete tool subset: [{string.Join(", ", toolSubset.Distinct())}]");
            }

            using var tracingHelper = new TracingHelper(tracer, context.ThreadId.ToString(), nameof(AgentTaskType.IncidentInvestigation));
            var runHooks = tracingHelper.GetAgentTaskTracingHooks();

            runHooks.ResolveFactoryTools = (runContext, agent) =>
            {
                List<AIFunction> tools = [];

                foreach (var toolName in agent.FactoryTools)
                {
                    var tool = (toolFactory as ToolFactory<AgentContext>)!.GetTool(toolName, context.ThreadId);

                    tools.Add(tool);
                }

                return Task.FromResult(tools);
            };

            // 1. Initial Investigation
            logger.LogInternalInformation("Starting initial investigation for task: {TaskId}", agentTask.Id);
            var currentStepSpan = tracingHelper.StartAgentTaskStepSpan("InitialInvestigation");

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

                    var toolSelectionAgent = IncidentInvestigationAgents.CreateToolSelectionAgent(
                        IncidentInvestigationHelper.GatheringContext.GetToolSelectionInstructions(toolFactory, is1PAgent, toolSubset));

                    var toolNames = await CallAgentAsync<List<string>>(
                        toolSelectionAgent,
                        context,
                        new ChatMessage(ChatRole.User, msg),
                        runHooks,
                        true,
                        tracer,
                        currentStepSpan,
                        cancellationToken);

                    ValidateAndAddRequiredTools(toolNames);

                    logger.LogInternalInformation(
                        "Tool selection agent selected {ToolCount} tools: [{Tools}]",
                        toolNames.Count,
                        string.Join(", ", toolNames)
                    );

                    if (is1PAgent)
                    {
                        state.InitialInvestigation.ToolNames = toolNames;
                    }

                    state.InitialInvestigation.StatusMessage = $"Selected {toolNames.Count} investigation tools, beginning analysis...";
                    state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);

                    logger.LogInternalInformation("Starting initial investigation agent.");
                    var initialInvestigationAgent = IncidentInvestigationAgents.CreateInitialInvestigationAgent(toolNames);

                    var initialInvestigationResult = await CallAgentAsync<InitialInvestigationResult>(
                        initialInvestigationAgent,
                        context,
                        new ChatMessage(ChatRole.User, msg),
                        runHooks,
                        true,
                        tracer,
                        currentStepSpan,
                        cancellationToken);

                    state.InitialInvestigation.GatheringContext.Status = InitialInvestigationStatus.Complete;
                    state.InitialInvestigation.GatheringContext.Steps = [.. initialInvestigationResult.ContextGatheringSteps.Select(s =>
                    {
                        return new InitialInvestigationStep
                        {
                            Title = s.Title,
                            Summary = s.Summary,
                            Status = InitialInvestigationStatus.Complete
                        };
                    })];

                    logger.LogInternalInformation("Initial investigation agent completed with summary: {Summary}", initialInvestigationResult.Summary);

                    state.InitialInvestigation.StatusMessage = "Initial investigation complete.";

                    // 3. Replaying memories
                    // retrieve past trajectories
                    // trajectories will have steps to follow and instructions about what NOT to do
                    //state.InitialInvestigation.GatheringContext.Steps = new List<InitialInvestigationStep>();

                    // 4. Generate a summary of the initial investigation

                    state.InitialInvestigation.Summary = initialInvestigationResult.Summary;
                    state.InitialInvestigation.Status = InitialInvestigationStatus.Complete;

                    state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);
                }
            }
            logger.LogInternalInformation("Initial investigation completed with summary.");
            tracingHelper.EndAgentTaskStepSpan();

            // 2. Forming Hypothesis
            logger.LogInternalInformation("Starting forming hypothesis for task: {TaskId}", agentTask.Id);
            currentStepSpan = tracingHelper.StartAgentTaskStepSpan("FormingHypothesis");

            var finalValidatedHypotheses = new List<HypothesisTreeItem>();
            var allInvestigatedHypotheses = new List<HypothesisTreeItem>();

            if (state.FormingHypothesis.Status != FormingHypothesisStatus.Complete)
            {
                // Generate initial hypotheses
                state.FormingHypothesis.StatusMessage = "Generating hypotheses...";
                state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);

                var initialHypotheses = await GenerateHypotheses(
                    inputData.IncidentDescription,
                    state.InitialInvestigation.Summary,
                    null,
                    context,
                    runHooks,
                    currentStepSpan,
                    cancellationToken);

                state.FormingHypothesis.Hypotheses = initialHypotheses;

                state = await SaveStateAndStreamUpdateAsync(cancellationToken: cancellationToken);

                // 3. Validating Hypotheses
                logger.LogInternalInformation("Starting hypothesis validation for task: {TaskId}", agentTask.Id);

                var queue = new Queue<(HypothesisTreeItem, int)>();

                foreach (var h in state.FormingHypothesis.Hypotheses)
                {
                    queue.Enqueue((h, 1));
                }

                while (queue.Count > 0)
                {
                    // Validate the current hypothesis
                    (var current, int depth) = queue.Dequeue();
                    string validatedHypothesis = current.ParentHypothesisDescription;

                    current.StatusMessage = "Analyzing...";
                    current.Status = HypothesisStatus.Validating;
                    await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);

                    var validationResult = await ValidateHypothesisAsync(
                        inputData.IncidentDescription,
                        state.InitialInvestigation.Summary,
                        validatedHypothesis,
                        current.Description,
                        context,
                        runHooks,
                        currentStepSpan,
                        async step =>
                        {
                            // Save and update the state with the current step
                            current.Steps.Add(step);
                            await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);
                        },
                        cancellationToken);

                    current.Status = validationResult.Status switch
                    {
                        HypothesisValidationStatus.Validated => HypothesisStatus.Validated,
                        HypothesisValidationStatus.Invalidated => HypothesisStatus.Invalidated,
                        HypothesisValidationStatus.Inconclusive => HypothesisStatus.Inconclusive,
                        _ => HypothesisStatus.Inconclusive
                    };

                    current.Steps = validationResult.Steps;

                    // Add to all investigated hypotheses list
                    allInvestigatedHypotheses.Add(current);

                    // Stream hypothesis status update with more descriptive message
                    var statusMessage = current.Status switch
                    {
                        HypothesisStatus.Validated => $"Hypothesis validated: {current.Title}",
                        HypothesisStatus.Invalidated => $"Hypothesis invalidated: {current.Title}",
                        HypothesisStatus.Inconclusive => $"Hypothesis inconclusive: {current.Title}",
                        _ => $"Hypothesis status updated to {current.Status}: {current.Title}"
                    };

                    current.StatusMessage = statusMessage;
                    await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);

                    if (current.Status == HypothesisStatus.Validated)
                    {
                        validatedHypothesis = current.Description;
                    }
                    else
                    {
                        // if hypothesis is invalidated, continue to next hypothesis
                        continue;
                    }

                    // TODO: isRootCause functionality removed - using depth-based stopping only

                    // logger.LogInternalInformation("Checking if we should stop based current validations.");
                    // string message = $"""
                    //     The incident description is as follows:
                    //     {inputData.IncidentDescription}

                    //     The summary of the current investigation is:
                    //     {state.InitialInvestigation.Summary}

                    //     The following hypothesis was validated:
                    //     - {validatedHypothesis}
                    //     """;
                    // var checkAgent = IncidentInvestigationAgents.CreateHypothesisValidationCheckAgent();
                    // bool shouldStop = await CallAgentAsync<bool>(
                    //     checkAgent,
                    //     context,
                    //     new ChatMessage(ChatRole.User, message),
                    //     runHooks,
                    //     cancellationToken);

                    // // Keep the IsRootCause value for cross checking later
                    // logger.LogInternalInformation("Hypothesis validation check result: {ShouldStop}, isRootCause: {IsRootCause}", shouldStop, validationResult.IsRootCause);

                    if (depth >= 4)
                    {
                        // if hypothesis is at maximum depth, add to final validated hypotheses but continue processing
                        // TODO: isRootCause functionality removed - only using depth-based stopping
                        finalValidatedHypotheses.Add(current);

                        state.FormingHypothesis.StatusMessage = "Maximum search depth reached for this hypothesis, continuing with other hypotheses.";

                        // Continue to next hypothesis without generating children
                        continue;
                    }

                    // call LLM to generate hypotheses
                    var hypotheses = await GenerateHypotheses(
                        inputData.IncidentDescription,
                        state.InitialInvestigation.Summary,
                        validatedHypothesis,
                        context,
                        runHooks,
                        currentStepSpan,
                        cancellationToken);
                    current.Children = hypotheses;

                    // Stream children addition
                    foreach (var child in hypotheses)
                    {
                        child.ParentHypothesisDescription = current.Description;
                    }

                    await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);

                    foreach (var h in hypotheses)
                    {
                        // set the current hypothesis as the parent description for the child hypotheses
                        h.ParentHypothesisDescription = current.Description;
                        queue.Enqueue((h, depth + 1));
                    }
                }

                state.FormingHypothesis.Status = FormingHypothesisStatus.Complete;

                state = await SaveStateAndStreamUpdateAsync(state, cancellationToken: cancellationToken);
            }
            tracingHelper.EndAgentTaskStepSpan();
            logger.LogInternalInformation("Forming hypothesis completed with {ValidHypothesisCount} valid hypotheses.",
                finalValidatedHypotheses.Count);

            // 4. Conclusion
            // three possibilities based on investigation results:
            // TODO: isRootCause functionality removed
            // 2. 1 valid hypothesis at the end → isRootCause == true, or all other hypothesis invalidated treat final valid hypothesis like root cause
            // 2. 1 valid hypothesis at the end → treat final valid hypothesis like root cause
            // 3. >1 valid hypothesis at the end → multiple hypotheses
            // 4. 0 valid hypothesis at the end → inconclusive
            logger.LogInternalInformation("Starting conclusion generation for task: {TaskId}", agentTask.Id);
            currentStepSpan = tracingHelper.StartAgentTaskStepSpan("Conclusion");

            if (finalValidatedHypotheses.Count == 1)
            {
                // Stream single hypothesis conclusion
                await GenerateSingleValidHypothesisConclusion(finalValidatedHypotheses.First(), inputData, context, runHooks, currentStepSpan, cancellationToken);
            }
            else if (finalValidatedHypotheses.Count > 1)
            {
                // Stream multiple hypotheses conclusion
                await GenerateMultipleValidHypothesesConclusion(finalValidatedHypotheses, inputData, context, runHooks, currentStepSpan, cancellationToken);
            }
            else
            {
                // Stream inconclusive conclusion - use all investigated hypotheses
                await GenerateInconclusiveConclusion(inputData, context, runHooks, currentStepSpan, allInvestigatedHypotheses, cancellationToken);
            }

            state = await SaveStateAndStreamUpdateAsync(newStatus: AgentTaskStatus.Complete, cancellationToken: cancellationToken);

            // Stream conclusion completion
            tracingHelper.EndAgentTaskStepSpan();
            logger.LogInternalInformation("Incident investigation task {TaskId} completed successfully.", agentTask.Id);
        }
        catch (Exception e)
        {
            // Stream error
            await SaveStateAndStreamUpdateAsync(newStatus: AgentTaskStatus.Failed, cancellationToken: cancellationToken);

            logger.LogInternalError(e, "Error while executing investigation");
            throw;
        }
    }

    /// <summary>
    /// Sends a deep investigation notification to the user.
    /// </summary>
    /// <param name="threadId">The thread ID</param>
    /// <param name="agentTaskId">The agent task ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task SendDeepInvestigationNotificationAsync(Guid threadId, Guid agentTaskId)
    {
        try
        {
            logger.LogInternalInformation("Sending deep investigation notification for thread {ThreadId}, task {TaskId}", threadId, agentTaskId);

            ChatMessage message = new ChatMessage(ChatRole.User, "Running Deep investigation in parallel. You can still chat with the agent");

            await outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                threadId,
                string.Empty,
                message: message, 
                type: StreamMessageType.DeepInvestigation,
                agentTaskId: agentTaskId
               );

            logger.LogInternalInformation("Successfully sent deep investigation notification for thread {ThreadId}, task {TaskId}", threadId, agentTaskId);
        }
        catch (Exception ex)
        {
            logger.LogInternalWarning(ex, "Failed to send deep investigation notification for thread {ThreadId}, task {TaskId}. Investigation will continue.", threadId, agentTaskId);
            // Don't rethrow - notification failure shouldn't break the investigation
        }
    }

    private void ValidateAndAddRequiredTools(List<string> toolNames)
    {
        if (is1PAgent)
        {
            toolNames.AddRange(
            [
                "GetIssueInvestigationTimeRangeRCAContainerApp",
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
                "SearchMemory"
            ]);
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

        foreach (var message in allMessages)
        {
            // Look for function calls in assistant messages
            if (message.Role == ChatRole.Assistant)
            {
                var functionCalls = message.Contents.OfType<FunctionCallContent>().ToList();
                foreach (var functionCall in functionCalls)
                {
                    if (!string.IsNullOrEmpty(functionCall.CallId))
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
            // Add a context message explaining the tool history
            var contextMessage = new ChatMessage(ChatRole.System,
                $"The following {_aggregatedToolHistory.Count / 2} tool interactions have been performed previously in this investigation. " +
                "Use this information and avoid redundant tool calls and build upon previous results. IMPORTANT: Do not repeat tool calls with same parameters");

            chatHistory.Insert(0, contextMessage);

            // Insert the aggregated tool history after the context message
            chatHistory.InsertRange(1, _aggregatedToolHistory);

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
        Tracer? tracer = null,
        TelemetrySpan? parentSpan = null,
        CancellationToken cancellationToken = default)
    {
        if (agent.HasStructuredOutput && typeof(TResult) != agent.OutputType)
        {
            throw new InvalidOperationException("Agent has structured output but the result type is not the same as the output type.");
        }

        const int retryLimit = 3;
        var threadId = context.ThreadId.ToString();

        for (var i = 0; i < retryLimit; i++)
        {
            try
            {
                if (enableDocumentSearch)
                {
                    var docs = new List<SearchDocument>();
                    string query = await DocumentRetrieval.GenerateSearchQuery(
                        chatClient,
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
                    if (is1PAgent)
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
                    ChatClient = chatClient,
                    LoggerFactory = loggerFactory,
                };

                // Inject tool call history into the chat input
                var chatHistory = InjectToolCallHistory(inputMessage);

                var runResult = await Runner.RunAsync(
                    startingAgent: agent,
                    input: chatHistory,
                    config: runConfig,
                    context: context,
                    hooks: runHooks,
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
                                SkipToolCall = true,
                                Output = null,
                            });
                        }

                        var toolOutput = await InvokeToolWithErrorHandlingAsync(toolCall, context, cancellationToken);
                        results.Add(new ManualToolCallResult
                        {
                            FunctionCall = toolCall.FunctionCall,
                            SkipToolCall = false,
                            Output = toolOutput,
                        });
                    }

                    runResult = await Runner.ResumeFromManualToolsAsync(
                        previousResult: runResult,
                        manualToolResults: results,
                        config: runConfig,
                        context: context,
                        hooks: runHooks,
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
            catch (Exception e) when (e.Message.Contains("HTTP 429"))
            {
                // probably rate-limited by AOAI, retry after a few seconds
                if (i == retryLimit - 1)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        throw new InvalidOperationException("Retry exceeded");
    }

    private async Task<ICollection<SearchDocument>> RetrieveDocumentsFromLocalStore(string query)
    {
        if (RcaAgentsStore == null)
        {
            logger.LogInternalInformation("RCA agents store is not initialized, returning empty results");
            return Array.Empty<SearchDocument>();
        }

        return await RcaAgentsStore.SearchAsync(query, 3).ToListAsync();
    }

    private async Task<IEnumerable<SearchDocument>> RetrieveDocumentsFromRegionalStore(string query, string threadId, TelemetrySpan? parentSpan = null)
    {
        var results = await searchHelper.SearchAsync(query, SearchRequest.TypeDocument, false, parentSpan, threadId);
        return results;
    }

    private void CacheToolResult(string toolName, object? parameters, object? result)
    {
        var key = GenerateCacheKey(toolName, parameters);
        _toolCache[key] = result;
    }

    private bool TryGetCachedResult(string toolName, object? parameters, out object? result)
    {
        var key = GenerateCacheKey(toolName, parameters);
        return _toolCache.TryGetValue(key, out result);
    }

    private static string GenerateCacheKey(string toolName, object? parameters)
    {
        // the order of the parameters shouldn't matter since its serialized to JSON and then hashed
        var parametersJson = parameters == null ? "null" : JsonSerializer.Serialize(parameters, JsonSerializerOptions.Web);
        var combined = $"{toolName}:{parametersJson}";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hashBytes);
    }

    private async Task<object?> InvokeToolWithErrorHandlingAsync(
        ManualToolCall toolCall,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            if (TryGetCachedResult(toolCall.Tool.Name, toolCall.FunctionCall.Arguments, out var cachedResult))
            {
                logger.LogInternalInformation("Cache hit for tool: {ToolName}", toolCall.Tool.Name);
                return cachedResult;
            }

            Core.ToolStatic.AsyncLocalThreadId.Value = context.ThreadId;
            Core.ToolStatic.AsyncLocalCancellationToken.Value = cancellationToken;
            var result = await toolCall.Tool.InvokeAsync(new AIFunctionArguments(toolCall.FunctionCall.Arguments), cancellationToken);

            CacheToolResult(toolCall.Tool.Name, toolCall.FunctionCall.Arguments, result);
            logger.LogInternalInformation("Cached result for tool: {ToolName}", toolCall.Tool.Name);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error while calling tool {ToolName}", toolCall.Tool!.Name);
            return GetErrorMessage(toolCall.FunctionCall, ex);
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
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Generating hypotheses for incident description.");
        var hypothesisGenerationAgent = IncidentInvestigationAgents.CreateHypothesisGenerationAgent();
        string message = $"""
            The incident description is as follows:
            {incidentDescription}

            The summary of the current investigation is:
            {investigationSummary}
            """;

        if (!string.IsNullOrEmpty(validatedHypothesis))
        {
            message += $"""

                The following hypothesis was validated:
                - {validatedHypothesis}

                Please dig deeper into the hypothesis above and make more detailed hypotheses in the scope of it. Don't make any assumptions out of the scope.
                """;
        }
        var hypotheses = await CallAgentAsync<List<HypothesisGenerationResult>>(
            hypothesisGenerationAgent,
            context,
            new ChatMessage(ChatRole.User, message),
            runHooks,
            true,
            tracer,
            currentStepSpan,
            cancellationToken);
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
        string validatedHypothesis,
        string currentHypothesis,
        AgentContext context,
        RunHooks<AgentContext> runHooks,
        TelemetrySpan currentStepSpan,
        Func<HypothesisStep, Task> saveAndUpdateCallback,
        CancellationToken cancellationToken)
    {
        logger.LogInternalInformation("Validating hypothesis: {Hypothesis}", currentHypothesis);

        var toolSelectionAgent = IncidentInvestigationAgents.CreateToolSelectionAgent(
            IncidentInvestigationHelper.HypothesisValidation.GetToolSelectionInstructions(toolFactory, incidentDescription, investigationSummary, toolSubset));

        var toolNames = await CallAgentAsync<List<string>>(
                    toolSelectionAgent,
                    context,
                    new ChatMessage(ChatRole.User, currentHypothesis),
                    runHooks,
                    true,
                    tracer,
                    currentStepSpan,
                    cancellationToken);

        ValidateAndAddRequiredTools(toolNames);

        // call LLM to validate/invalidate the hypothesis
        //var hypothesisValidationAgent = IncidentInvestigationAgents.CreateHypothesisValidationAgent(
        //    toolNames,
        //    incidentDescription,
        //    investigationSummary,
        //    validatedHypothesis);

        var inputMessage = new ChatMessage(ChatRole.User, $"""
            Please validate the following hypothesis:

            {currentHypothesis}
        """);

        //var validationResult = await CallAgentAsync<HypothesisValidationResult>(
        //    hypothesisValidationAgent,
        //    context,
        //    inputMessage,
        //    runHooks,
        //    cancellationToken
        //);

        // new flow:
        // start by generating a plan
        var planningAgent = IncidentInvestigationAgents.CreateHypothesisValidationPlanningAgent(
            (toolFactory as ToolFactory<AgentContext>)!.FetchToolInfoForToolNames(toolNames),
            incidentDescription,
            investigationSummary,
            validatedHypothesis);

        var plan = await CallAgentAsync<HypothesisValidationPlanOutput>(
            planningAgent,
            context,
            inputMessage,
            runHooks,
            true,
            tracer,
            currentStepSpan,
            cancellationToken);

        //var toolSelectionAgent = IncidentInvestigationAgents.CreateToolSelectionAgent(
        //    IncidentInvestigationHelper.HypothesisValidation.GetToolSelectionInstructions(toolFactory, incidentDescription, investigationSummary));

        //var toolSelectionInput = $"""
        //    # Current hypothesis

        //    {currentHypothesis}

        //    # Validation Plan

        //    {string.Join(Environment.NewLine + Environment.NewLine, plan.Steps.Select(s => $"## Plan Step: {s.Title}{Environment.NewLine}{s.Description}"))}
        //    """;

        //var toolNames = await CallAgentAsync<List<string>>(
        //            toolSelectionAgent,
        //            context,
        //            new ChatMessage(ChatRole.User, toolSelectionInput),
        //            runHooks,
        //            cancellationToken);

        //ValidateAndAddRequiredTools(toolNames);

        // execute plan step by step
        List<HypothesisStep> completedSteps = [];

        foreach (var step in plan.Steps)
        {
            // todo: test selecting tool names per-step instead of once at the beginning
            var stepExecutionAgent = IncidentInvestigationAgents.CreateHypothesisValidationPlanExecutionAgent(
                toolNames,
                incidentDescription,
                investigationSummary,
                validatedHypothesis,
                currentHypothesis,
                plan,
                completedSteps);

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
                tracer,
                currentStepSpan,
                cancellationToken);

            var item = new HypothesisStep
            {
                Summary = step.Title,
                Details = stepExecutionResult.Summary
            };
            completedSteps.Add(item);
            await saveAndUpdateCallback.Invoke(item);

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
            completedSteps);

        var result = await CallAgentAsync<HypothesisResultSummaryOutput>(
            summarizationAgent,
            context,
            new ChatMessage(ChatRole.User, "Analyze the validation steps and provide your result"),
            runHooks,
            false,
            tracer,
            currentStepSpan,
            cancellationToken);

        logger.LogInternalInformation("Hypothesis validation result: Hypothesis: {Hypothesis}, Status: {Status}, Reasoning: {Reasoning}",
            currentHypothesis, result.Status, result.Reasoning);

        var validationResult = new HypothesisValidationResult
        {
            Status = result.Status,
            Steps = completedSteps,
            IsRootCause = false
        };

        // TODO: isRootCause functionality removed
        // logger.LogInternalInformation("Hypothesis validation result: {Status}, IsRootCause: {IsRootCause}",
        //     validationResult.Status, validationResult.IsRootCause);
        logger.LogInternalInformation("Hypothesis validation result: {Status}",
            validationResult.Status);
        return validationResult;
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
        var conclusionAgent = IncidentInvestigationAgents.CreateConclusionAgent();

        var state = GetCurrentState();

        var message = $"""
            ## Incident Investigation Conclusion

            **Incident Description:**
            {inputData.IncidentDescription}

            **Initial Investigation Summary:**
            {state.InitialInvestigation.Summary}

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
            tracer,
            currentStepSpan,
            cancellationToken
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
        var conclusionAgent = IncidentInvestigationAgents.CreateConclusionAgent();

        var state = GetCurrentState();

        var hypothesesDescription = string.Join("\n", validHypotheses.Select((vh, index) =>
            $"- **Hypothesis {index + 1}:** {vh.Title}\n  - **Description:** {vh.Description}\n  - **Status:** {vh.Status}"));

        var message = $"""
            ## Incident Investigation Conclusion

            **Incident Description:**
            {inputData.IncidentDescription}

            **Initial Investigation Summary:**
            {state.InitialInvestigation.Summary}

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
            tracer,
            currentStepSpan,
            cancellationToken
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
        var conclusionAgent = IncidentInvestigationAgents.CreateConclusionAgent();

        var state = GetCurrentState();

        var hypothesesDescription = string.Join("\n\n", allInvestigatedHypotheses.Select((vh, index) =>
            $"- **Hypothesis {index + 1}:** {vh.Title}\n  - **Description:** {vh.Description}\n  - **Status:** {vh.Status}"));

        var message = $"""
            ## Incident Investigation Conclusion

            **Incident Description:**
            {inputData.IncidentDescription}

            **Initial Investigation Summary:**
            {state.InitialInvestigation.Summary}

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
            tracer,
            currentStepSpan,
            cancellationToken
        );

        state.Conclusion.Title = conclusion.Title;
        state.Conclusion.Summary = conclusion.Summary;

        logger.LogInternalInformation("Conclusion generated for inconclusive investigation: {ConclusionTitle}", conclusion.Title);
    }

    private async Task StreamTaskUpdateAsync(Guid threadId, AgentTask task, CancellationToken cancellationToken = default)
    {
        try
        {
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
}
