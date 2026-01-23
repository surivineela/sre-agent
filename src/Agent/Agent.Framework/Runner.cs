// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Framework.Skills;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Framework;

/// <summary>
/// Result from running an agent with handoff detection
/// </summary>
/// <typeparam name="TContext">The context type</typeparam>
public class RunResultWithHandoff<TContext> where TContext : class
{
    /// <summary>
    /// The standard run result
    /// </summary>
    public RunResult<TContext> RunResult { get; set; } = null!;

    /// <summary>
    /// True if a handoff was detected during execution
    /// </summary>
    public bool HandoffDetected { get; set; }

    /// <summary>
    /// The name of the target agent for the handoff (if detected)
    /// </summary>
    public string? HandoffTargetAgent { get; set; }
}

public static class Runner
{
    private const int DefaultMaxTurns = 50;
    private const int Gpt5InputWindow = 272000;
    private const int TokenThreshold = Gpt5InputWindow * 4 / 10;
    private const int ToolCountHardMax = 128;

    public static async Task<RunResult<TContext>> ResumeFromManualToolsAsync<TContext>(
        RunResult<TContext> previousResult,
        List<ManualToolCallResult> manualToolResults,
        RunConfig config,
        IAgentRuntimeModifier<TContext>? runtimeModifier = null,
        TContext? context = null,
        RunHooks<TContext>? hooks = null,
        IDisplayModelOutput? displayModelOutput = null,
        bool allowParallelToolCalls = true,
        ToolResultCache? toolResultCache = null,
        IToolOutputProcessService? toolOutputProcessService = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        var input = previousResult.Input
            .Concat(previousResult.NewItems)
            .ToList();
        var functionCallMessages = new List<ChatMessage>();

        if (previousResult.ManualToolCalls is null
            || previousResult.ManualToolCalls.Count == 0)
        {
            throw new Exception("No manual tool calls found");
        }

        foreach (var manualToolCall in previousResult.ManualToolCalls)
        {
            var matchingResult = manualToolResults.FirstOrDefault(o => o.FunctionCall.CallId == manualToolCall.FunctionCall.CallId)
                ?? throw new Exception("No matching result found for manual tool call");

            if (config.EnableDebugOutput
                && displayModelOutput is not null)
            {
                await displayModelOutput.OnComplete($"[DEBUG]\n\nCompleted Manually Invoked Tool: {manualToolCall.FunctionCall.Name}");
            }

            var resultContent = new FunctionResultContent(manualToolCall.FunctionCall.CallId, matchingResult.Output);
            functionCallMessages.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
            previousResult.Trajectory.Append(resultContent);

            // Append any additional messages (e.g., images from ViewImage tool)
            if (matchingResult.AdditionalMessages is not null)
            {
                functionCallMessages.AddRange(matchingResult.AdditionalMessages);
            }

            if (hooks != null)
            {
                await hooks.OnToolEnd(previousResult.ContextWrapper, previousResult.LastAgent, manualToolCall.FunctionCall, manualToolCall.Tool, matchingResult.Output);
            }
        }

        return await RunInternalAsync(
            startingAgent: previousResult.LastAgent,
            input: input,
            config: config,
            activeSkills: previousResult.ActiveSkills,
            runtimeModifier: runtimeModifier,
            newGeneratedItems: functionCallMessages,
            context: context,
            currentTurn: previousResult.CurrentTurn,
            maxTurns: previousResult.MaxTurns,
            hooks: hooks,
            trajectory: previousResult.Trajectory,
            displayModelOutput: displayModelOutput,
            cancellationToken: cancellationToken,
            _shouldRunAgentStartHooks: previousResult.AgentChanged(),
            allowParallelToolCalls: allowParallelToolCalls,
            toolResultCache: toolResultCache,
            toolOutputProcessService: toolOutputProcessService
        );
    }

    public static Task<RunResult<TContext>> RunAsync<TContext>(
        Agent<TContext> startingAgent,
        List<ChatMessage> input,
        RunConfig config,
        SkillList? activeSkills = null,
        IAgentRuntimeModifier<TContext>? runtimeModifier = null,
        TContext? context = null,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        IDisplayModelOutput? displayModelOutput = null,
        bool allowParallelToolCalls = true,
        ToolResultCache? toolResultCache = null,
        IToolOutputProcessService? toolOutputProcessService = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        return RunInternalAsync(
            startingAgent: startingAgent,
            input: input,
            config: config,
            activeSkills: activeSkills ?? [],
            runtimeModifier: runtimeModifier,
            context: context,
            maxTurns: maxTurns,
            hooks: hooks,
            displayModelOutput: displayModelOutput,
            cancellationToken: cancellationToken,
            allowParallelToolCalls: allowParallelToolCalls,
            toolResultCache: toolResultCache,
            toolOutputProcessService: toolOutputProcessService,
            _shouldRunAgentStartHooks: true // always run agent start hooks on initial run
        );
    }

    /// <summary>
    /// Runs an agent with handoff detection capability for workflow orchestration
    /// </summary>
    public static async Task<RunResultWithHandoff<TContext>> RunWithHandoffDetectionAsync<TContext>(
        Agent<TContext> startingAgent,
        List<ChatMessage> input,
        RunConfig config,
        SkillList? activeSkills = null,
        IAgentRuntimeModifier<TContext>? runtimeModifier = null,
        TContext? context = null,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        IDisplayModelOutput? displayModelOutput = null,
        bool allowParallelToolCalls = true,
        IToolOutputProcessService? toolOutputProcessService = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        var handoffDetected = false;
        string? handoffTargetAgent = null;

        // Create custom hooks that detect handoff
        var handoffDetectionHooks = hooks ?? new RunHooks<TContext>();

        handoffDetectionHooks.Handoff += (context, fromAgent, toAgent, handoffReasoning) =>
        {
            handoffDetected = true;
            handoffTargetAgent = toAgent.Name;
            return Task.CompletedTask;
        };

        try
        {
            var runResult = await RunInternalAsync(
                startingAgent: startingAgent,
                input: input,
                config: config,
                activeSkills: activeSkills ?? [],
                runtimeModifier: runtimeModifier,
                context: context,
                maxTurns: maxTurns,
                hooks: handoffDetectionHooks,
                displayModelOutput: displayModelOutput,
                cancellationToken: cancellationToken,
                _shouldRunAgentStartHooks: true,
                allowParallelToolCalls: allowParallelToolCalls,
                toolOutputProcessService: toolOutputProcessService
            );

            return new RunResultWithHandoff<TContext>
            {
                RunResult = runResult,
                HandoffDetected = handoffDetected,
                HandoffTargetAgent = handoffTargetAgent
            };
        }
        catch (Exception)
        {
            // Even if an exception occurs, we want to return handoff information
            // But we need to re-throw since this is an unexpected error
            throw;
        }
    }

    private static async Task<RunResult<TContext>> RunInternalAsync<TContext>(
        Agent<TContext> startingAgent,
        List<ChatMessage> input,
        RunConfig config,
        SkillList activeSkills,
        IAgentRuntimeModifier<TContext>? runtimeModifier = null,
        List<ChatMessage>? newGeneratedItems = null,
        TContext? context = null,
        int currentTurn = 0,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        Trajectory? trajectory = null,
        IDisplayModelOutput? displayModelOutput = null,
        bool _shouldRunAgentStartHooks = true,
        bool allowParallelToolCalls = true,
        ToolResultCache? toolResultCache = null,
        IToolOutputProcessService? toolOutputProcessService = null,
        CancellationToken cancellationToken = default // TODO: use cancellation token
    ) where TContext : class
    {
        hooks ??= new RunHooks<TContext>();
        var shouldRunAgentStartHooks = _shouldRunAgentStartHooks;
        var currentAgent = startingAgent;
        List<ChatMessage> originalInput = [.. input];
        List<ChatMessage> generatedMessages = newGeneratedItems is not null
            ? [.. newGeneratedItems]
            : [];
        List<ChatResponse> rawResponses = [];

        // Create trajectory from chat history if null, otherwise use the provided trajectory
        if (trajectory == null)
        {
            var allChatMessages = new List<ChatMessage>();
            allChatMessages.AddRange(originalInput);
            allChatMessages.AddRange(generatedMessages);
            trajectory = Trajectory.FromChatHistory(allChatMessages);
        }

        var contextWrapper = new RunContextWrapper<TContext>(context);

        var logger = config.LoggerFactory.CreateLogger("Agent.Framework.Runner");

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                currentTurn += 1;

                if (currentTurn > maxTurns)
                {
                    var result = new RunResult<TContext>(currentAgent)
                    {
                        Input = originalInput,
                        NewItems = generatedMessages,
                        Output = "Max turns reached",
                        ContextWrapper = contextWrapper,
                        CurrentTurn = currentTurn,
                        MaxTurns = maxTurns,
                        RawResponses = rawResponses,
                        Trajectory = trajectory,
                        ActiveSkills = activeSkills
                    };

                    throw new TurnLimitReachedException<TContext>(
                        "Max turns reached",
                        result
                    );
                }

                var turnResult = await RunSingleStepAsync(
                    agent: currentAgent,
                    originalInput: originalInput,
                    generatedMessages: generatedMessages,
                    config: config,
                    activeSkills: activeSkills,
                    runtimeModifier: runtimeModifier,
                    contextWrapper: contextWrapper,
                    hooks: hooks,
                    shouldRunAgentStartHooks: shouldRunAgentStartHooks,
                    trajectory: trajectory,
                    logger: logger,
                    allowParallelToolCalls: allowParallelToolCalls,
                    displayModelOutput: displayModelOutput,
                    toolResultCache: toolResultCache,
                    toolOutputProcessService: toolOutputProcessService,
                    cancellationToken: cancellationToken
                );

                shouldRunAgentStartHooks = false;
                originalInput = turnResult.OriginalInput;
                generatedMessages = turnResult.GeneratedItems;
                rawResponses.Add(turnResult.ModelResponse);

                var activeAgent = turnResult.NextStep.Type == NextStepType.Handoff && turnResult.NextStep.Agent is not null
                    ? turnResult.NextStep.Agent
                    : currentAgent;

                ProcessActiveSkills(activeSkills, turnResult.NewActivatedSkills, activeAgent, config);

                if (config.EnableDebugOutput
                    && displayModelOutput is not null)
                {
                    foreach (var message in turnResult.ModelResponse.Messages)
                    {
                        foreach (var content in message.Contents)
                        {
                            if (content is TextContent t)
                            {
                                await displayModelOutput.OnComplete($"[DEBUG]\n\nAgent: {currentAgent.Name}\n\nResponse: {t.Text}");
                            }
                            else if (content is FunctionCallContent f)
                            {
                                await displayModelOutput.OnComplete($"[DEBUG]\n\nAgent: {currentAgent.Name}"
                                    + $"\n\nFunction Call: {f.Name}"
                                    + $"\n\nParameters: {f.GetSerializedArguments()}");
                            }
                        }
                    }
                }

                if (turnResult.NextStep.Type == NextStepType.FinalOutput)
                {
                    logger.LogInternalInformation(
                        "FinalOutput received from {AgentName}, Critic: {criticCount}/{MaxReflectionCount}",
                        currentAgent.Name,
                        trajectory.GetCriticCount(currentAgent.Name),
                        currentAgent.MaxReflectionCount);

                    var criticApproval = await CriticAsync(
                        config,
                        runtimeModifier,
                        hooks,
                        activeSkills,
                        trajectory,
                        displayModelOutput,
                        currentAgent,
                        originalInput,
                        generatedMessages,
                        contextWrapper,
                        logger);

                    if (!criticApproval)
                    {
                        continue;
                    }

                    // Auto-compact chat history if needed
                    var compacted = await CompactChatHistory(
                        currentAgent,
                        originalInput,
                        generatedMessages,
                        contextWrapper,
                        currentAgent.GetChatClient(config),
                        activeSkills,
                        hooks,
                        logger);

                    await hooks.OnAgentEnd(contextWrapper, currentAgent, turnResult.NextStep.Output);

                    // Reset critic counts when conversation completes successfully
                    trajectory.ResetCriticCounts();

                    return new RunResult<TContext>(currentAgent)
                    {
                        Input = originalInput,
                        NewItems = generatedMessages,
                        Output = turnResult.NextStep.Output,
                        ContextWrapper = contextWrapper,
                        CurrentTurn = currentTurn,
                        MaxTurns = maxTurns,
                        RawResponses = rawResponses,
                        Trajectory = trajectory,
                        ActiveSkills = activeSkills
                    };
                }
                else if (turnResult.NextStep.Type == NextStepType.Handoff
                    && turnResult.NextStep.Agent is not null)
                {
                    currentAgent = turnResult.NextStep.Agent;
                    shouldRunAgentStartHooks = true;
                    shouldRunAgentStartHooks = true;

                    await CompactChatHistory(
                        currentAgent,
                        originalInput,
                        generatedMessages,
                        contextWrapper,
                        currentAgent.GetChatClient(config),
                        activeSkills,
                        hooks,
                        logger);

                    // we should not reset the trajectory, as it may contain important information for critic as handoff is very cheap frequent behavior.
                }
                else if (turnResult.NextStep.Type == NextStepType.RunAgain)
                {
                    // do nothing, we will run the agent again
                }
                else if (turnResult.NextStep.Type == NextStepType.ManualTool
                    && turnResult.NextStep.ManualToolCalls.Count > 0)
                {
                    return new RunResult<TContext>(currentAgent)
                    {
                        Input = originalInput,
                        NewItems = generatedMessages,
                        Output = turnResult.NextStep.Output,
                        ContextWrapper = contextWrapper,
                        ManualToolCalls = turnResult.NextStep.ManualToolCalls,
                        CurrentTurn = currentTurn,
                        MaxTurns = maxTurns,
                        RawResponses = rawResponses,
                        Trajectory = trajectory,
                        ActiveSkills = activeSkills
                    };
                }
                else
                {
                    throw new Exception("Unknown next step type");
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInternalInformation("Operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogInternalError($"Exception thrown while executing reasoning loop: {ex.Message}");
            // todo: log
            throw;
        }
    }

    private static async Task<bool> CriticAsync<TContext>(
        RunConfig config,
        IAgentRuntimeModifier<TContext>? runtimeModifier,
        RunHooks<TContext> hooks,
        SkillList activeSkills,
        Trajectory trajectory,
        IDisplayModelOutput? displayModelOutput,
        Agent<TContext> currentAgent,
        List<ChatMessage> originalInput,
        List<ChatMessage> generatedMessages,
        RunContextWrapper<TContext> contextWrapper,
        ILogger logger,
        Action? failureHook = null)
        where TContext : class
    {
        var chatClientMetaData = config.ChatClient.GetRequiredService<ChatClientMetadata>();
        // skip critic for reasoning model for now as it degrades the performance a lot.
        if (chatClientMetaData?.DefaultModelId?.StartsWith("gpt-5") == true)
        {
            return true;
        }

        if (currentAgent.MaxReflectionCount > 0 && trajectory.GetCriticCount(currentAgent.Name) < currentAgent.MaxReflectionCount)
        {
            // Increment CriticCount at the start of actually running the critic
            var criticCount = trajectory.IncrementCriticCount(currentAgent.Name);

            if (config.EnableDebugOutput)
            {
                if (displayModelOutput is not null)
                {
                    await displayModelOutput.OnComplete($"[DEBUG]\n\nGathering critique: Agent: {currentAgent.Name}. Turn #{criticCount}/{currentAgent.MaxReflectionCount}");
                }
            }

            await hooks.OnSummarizerStart(contextWrapper, currentAgent);

            var userQuery = await Summarizer.SummarizeUserTrajectoryAsync(
                config.ChatClient,
                originalInput);

            await hooks.OnSummarizerEnd(contextWrapper, currentAgent, userQuery);

            if (config.EnableDebugOutput
                && displayModelOutput is not null)
            {
                await displayModelOutput.OnComplete($"[DEBUG]\n\nSummarized User Query: {userQuery}");
            }

            await hooks.OnCriticStart(contextWrapper, currentAgent, criticCount);

            var trajectoryString = trajectory.GetFilteredTrajectory();

            var agentTools = await hooks.OnResolveFactoryTools(contextWrapper, currentAgent, activeSkills.AllToolNames);

            var criticResult = await Critic.CriticAsync(
                currentAgent,
                userQuery,
                trajectoryString,
                agentTools,
                config.ChatClient);

            var rejected = criticResult.Contains("\"overall_assessment\": \"FAIL\"")
                || criticResult.Contains("\"overall_assessment\":\"FAIL\"");

            // Invoke the critic end hook for tracing
            await hooks.OnCriticEnd(contextWrapper, currentAgent, userQuery, criticResult, !rejected);

            if (rejected)
            {
                logger.LogWarning("Critic result indicates failure: {CriticResult}", criticResult);

                if (config.EnableDebugOutput
                    && displayModelOutput is not null)
                {
                    await displayModelOutput.OnComplete($"[DEBUG]\n\nCritic failed the turn:\n\n{criticResult}");
                }

                if (failureHook is not null)
                {
                    failureHook();
                }

                generatedMessages.Add(new(ChatRole.User, "Good try but you missed a few things. " +
                    "Please provide a natural response in your **NotifyUserMessage** that covers the following in 4-5 sentences: " +
                    "1. Acknowledge that you're taking a step back to review your previous work " +
                    "2. Briefly summarize what you accomplished so far " +
                    "3. Identify what was missing or incomplete in your previous work" +
                    "4. Explain your next steps to address those gaps " +
                    "Make this feel conversational and authentic - avoid formulaic language. proceed with the necessary tool calls and provide a complete answer to the original question. " +
                    "Do not mention this feedback explicitly, just the major learnings. After running the feedback, ensure that you give an answer to the original user question" +
                    "Feedback:\n" + criticResult));

                trajectory.AppendCriticFeedback(criticResult);

                return false;
            }
            else
            {
                logger.LogInternalInformation("Critic approved response: {CriticResult}", criticResult);
                if (config.EnableDebugOutput
                    && displayModelOutput is not null)
                {
                    await displayModelOutput.OnComplete($"[DEBUG]\n\nCritic approved the turn.");
                }
            }
        }
        else
        {
            logger.LogInternalInformation(
                "Critic skipped for {AgentName}: MaxReflectionCount={MaxReflectionCount}, CriticCount={CriticCount}",
                currentAgent.Name,
                currentAgent.MaxReflectionCount,
                trajectory.GetCriticCount(currentAgent.Name));

            await hooks.OnCriticStart(contextWrapper, currentAgent, currentAgent.MaxReflectionCount);

            // Invoke the critic end hook for tracing when critic is skipped
            var skipReason = currentAgent.MaxReflectionCount == 0
                ? "MaxReflectionCount is 0"
                : "CriticCount exceeds MaxReflectionCount";

            await hooks.OnCriticEnd(contextWrapper, currentAgent, "", $"Critic skipped: {skipReason}", true);
        }

        return true;
    }

    public static async Task<SingleStepResult<TContext>> RunSingleStepAsync<TContext>(
        Agent<TContext> agent,
        List<ChatMessage> originalInput,
        List<ChatMessage> generatedMessages,
        RunConfig config,
        SkillList activeSkills,
        IAgentRuntimeModifier<TContext>? runtimeModifier,
        RunContextWrapper<TContext> contextWrapper,
        RunHooks<TContext> hooks,
        bool shouldRunAgentStartHooks,
        Trajectory trajectory,
        ILogger logger,
        bool allowParallelToolCalls = true,
        ToolResultCache? toolResultCache = null,
        IDisplayModelOutput? displayModelOutput = null,
        IToolOutputProcessService? toolOutputProcessService = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        logger.LogInternalInformation("Running agent {AgentName} with runtime modifier {HasRuntimeModifier}", agent.Name, runtimeModifier != null);
        // Apply runtime modifications at the beginning of each turn
        if (runtimeModifier != null)
        {
            var modeChangeMessage = await runtimeModifier.ApplyRuntimeModificationsAsync(contextWrapper, agent);
            if (modeChangeMessage != null)
            {
                generatedMessages.Add(modeChangeMessage);
            }
        }

        if (shouldRunAgentStartHooks)
        {
            await hooks.OnAgentStart(contextWrapper, agent);
        }

        var systemPrompt = agent.Instructions.ToString();

        List<AIFunction> tools = [];
        tools.AddRange(agent.Tools);
        tools.AddRange(await hooks.OnResolveFactoryTools(contextWrapper, agent, activeSkills.AllToolNames));
        tools.AddRange(agent.Handoffs);

        tools = ValidateTools(tools, logger);

        var allowMultipleToolCalls = allowParallelToolCalls && agent.AllowParallelToolCalls;

        var chatOptions = new ChatOptions
        {
            Tools = tools.Cast<AITool>().ToList(),
            ToolMode = agent.ChatToolMode,
            Temperature = agent.Temperature,
            AllowMultipleToolCalls = tools.Count > 0 ? allowMultipleToolCalls : null
        };

        var chatClient = agent.GetChatClient(config);

        // for gpt-5 allow overriding the reasoning effort level per agent
        var chatClientMetaData = chatClient.GetRequiredService<ChatClientMetadata>();
        if (chatClientMetaData?.DefaultModelId?.StartsWith("gpt-5") == true)
        {
            chatOptions.AdditionalProperties ??= [];
            chatOptions.AdditionalProperties[FrameworkConstants.ReasoningEffortKey] = agent.ReasoningEffortLevel;
        }

        List<ChatMessage> modelInput = [new ChatMessage(ChatRole.System, systemPrompt)];
        modelInput.AddRange(ParseCompactedInput(originalInput, generatedMessages));

        // Process handoff prompt override if current agent has UserPromptOverride
        // ToDo: Check if this is still needed..
        if (!string.IsNullOrEmpty(agent.UserPromptOverride))
        {
            modelInput = ProcessHandoffPromptOverride(modelInput, agent);
        }

        // add plan reminder as required (skip if ambient context provider handles it)
        if (!config.AmbientContextProvider.Enabled)
        {
            AddPlanReminderIfNeeded<TContext>(modelInput, tools);

            // Add skills reminder if required
            AddSkillsReminderIfNeeded(modelInput, agent, activeSkills);

            // tool invocations like metrics query depend on current time
            // Note: this message used to be a system message which works for openai models. But claude only supports one system message
            // Multple system messages are hoisted to a signle one by the Anthropic SDK, generating a confusing system prompt with multiple date time messages
            // Besides, it is also bad for prompt caching because the system prompt changes every time
            // So it's changed to a user message
            modelInput.Add(new ChatMessage(ChatRole.User, $"<system-reminder>The current date is {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}</system-reminder>"));
        }
        else
        {
            logger.LogInternalInformation("Adding context via IAmbientContextProvider");
            // Inject ambient context if provider is enabled
            await AddAmbientContextAsync(modelInput, config.AmbientContextProvider, cancellationToken);
        }

        await hooks.OnModelGenerationStart(contextWrapper, agent, modelInput, chatOptions);

        ChatResponse? response = null;
        object? structuredOutput = null;

        var reasoningStreamHandler = displayModelOutput is not null
            ? new ReasoningStreamContentHandler(displayModelOutput)
            : default;

        var textStreamHandler = displayModelOutput is not null
            ? new TextStreamContentHandler(displayModelOutput)
            : default;

        try
        {
            if (agent.HasStructuredOutput)
            {
                // deserialize sometimes fail with missing field, falls back to non-structured response path
                try
                {
                    var jsonStreamHandler = displayModelOutput is not null
                        ? new JsonStreamContentHandler(agent.OutputType, displayModelOutput)
                        : default;

                    (response, structuredOutput) = await chatClient.GetResponseAsync(
                        messages: modelInput,
                        outputType: agent.OutputType,
                        options: chatOptions,
                        outputJsonStreamHandler: jsonStreamHandler,
                        reasoningStreamHandler: reasoningStreamHandler,
                        cancellationToken: cancellationToken);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to deserialize the response"))
                {
                    logger.LogWarning(ex, "Failed to deserialize structured output for agent {AgentName}. Falling back to non-structured response.", agent.Name);
                    response = await chatClient.GetResponseAsync(
                        messages: modelInput,
                        options: chatOptions,
                        outputTextStreamHandler: textStreamHandler,
                        reasoningStreamHandler: reasoningStreamHandler,
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                response = await chatClient.GetResponseAsync(
                    messages: modelInput,
                    options: chatOptions,
                    outputTextStreamHandler: textStreamHandler,
                    reasoningStreamHandler: reasoningStreamHandler,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await hooks.OnModelGenerationError(contextWrapper, agent, ex);
            throw;
        }
        finally
        {
            await hooks.OnModelGenerationEnd(contextWrapper, agent, response);
        }

        trajectory.Append(response);

        if (response.Usage != null)
        {
            contextWrapper.TotalUsageDetails.Add(response.Usage);
            contextWrapper.CurrentStepUsageDetails = response.Usage;
        }

        return await ExecuteToolsAndHandoffsAsync(
            agent: agent,
            originalInput: originalInput,
            preStepItems: generatedMessages,
            modelResponse: response,
            structuredOutput: structuredOutput,
            config: config,
            runtimeModifier: runtimeModifier,
            hooks: hooks,
            contextWrapper: contextWrapper,
            runConfig: config,
            tools: tools,
            trajectory: trajectory,
            activeSkills: activeSkills,
            logger: logger,
            displayModelOutput: displayModelOutput,
            toolResultCache: toolResultCache,
            toolOutputProcessService: toolOutputProcessService,
            cancellationToken: cancellationToken
        );
    }

    private static async Task<SingleStepResult<TContext>> ExecuteToolsAndHandoffsAsync<TContext>(
        Agent<TContext> agent,
        List<ChatMessage> originalInput,
        List<ChatMessage> preStepItems,
        ChatResponse modelResponse,
        object? structuredOutput,
        RunConfig config,
        IAgentRuntimeModifier<TContext>? runtimeModifier,
        RunHooks<TContext> hooks,
        RunContextWrapper<TContext> contextWrapper,
        RunConfig runConfig,
        List<AIFunction> tools,
        Trajectory trajectory,
        SkillList activeSkills,
        ILogger logger,
        ToolResultCache? toolResultCache = null,
        IDisplayModelOutput? displayModelOutput = null,
        IToolOutputProcessService? toolOutputProcessService = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        List<ChatMessage> newStepItems = [];
        List<ManualToolCall> manualToolCalls = [];
        var anyToolsCalled = modelResponse.Messages.Any(m => m.Contents.OfType<FunctionCallContent>().Any());
        List<FunctionResultContent> functionResults = [];
        var handoffOccurred = false;
        Agent<TContext>? handoffNewAgent = null;
        SkillList newActivatedSkills = [];

        // validate tool calling
        // if parallel tool calling is enabled, handoffs must still be called alone
        if (modelResponse.Messages.Any(m => m.Contents.OfType<FunctionCallContent>().Any(f => IsAllowedHandOff(f, agent, tools)))
            && modelResponse.Messages.Any(m => m.Contents.OfType<FunctionCallContent>().Any(f => !IsAllowedHandOff(f, agent, tools))))
        {
            // invalid to have both handoff and regular tool calls in the same response
            var errorMessage = """
            Invalid response: cannot have both handoff and regular tool calls in the same response. If you need to make other tool calls, do so before calling the handoff.
            If you need to call a handoff now, do so without calling any other tools.
            """;

            newStepItems.AddRange(modelResponse.Messages);

            List<AIContent> results = [..
                modelResponse.Messages
                .Select(m => m.Contents.OfType<FunctionCallContent>())
                .SelectMany(f => f)
                .Select(f => new FunctionResultContent(f.CallId, errorMessage))];

            newStepItems.Add(new ChatMessage(ChatRole.Tool, results));

            return new SingleStepResult<TContext>
            {
                OriginalInput = originalInput,
                ModelResponse = modelResponse,
                PreStepItems = preStepItems,
                NewStepItems = newStepItems,
                NextStep = new NextStep<TContext>
                {
                    Type = NextStepType.RunAgain,
                },
                NewActivatedSkills = []
            };
        }

        // process tool calls
        // assume no parallel tool calling, so if a regular tool is called, we are not handing off to another agent
        foreach (var modelResponseMessage in modelResponse.Messages)
        {
            var functionCalls = modelResponseMessage.Contents.OfType<FunctionCallContent>();

            foreach (var functionCall in functionCalls)
            {
                var tool = tools.FirstOrDefault(t => t.Name == functionCall.Name);

                // check for cached function call result
                if (tool is not null && toolResultCache is not null && toolResultCache.TryGetValue(functionCall, out var cachedResult))
                {
                    var cachedFunctionResult = new FunctionResultContent(functionCall.CallId, cachedResult);
                    functionResults.Add(cachedFunctionResult);
                    trajectory.Append(cachedFunctionResult);

                    if (config.EnableDebugOutput
                        && displayModelOutput is not null)
                    {
                        await displayModelOutput.OnComplete($"[DEBUG]\n\nUsing cached result for tool: {functionCall.Name}");
                    }

                    await hooks.OnToolEnd(contextWrapper, agent, functionCall, tool, cachedFunctionResult);

                    continue;
                }

                if (IsAllowedHandOff(functionCall, agent, tools))
                {
                    // Check for handoff loops BEFORE processing the handoff
                    if (trajectory.HasHandoffLoop(loopThreshold: 3))
                    {
                        logger.LogWarning(
                            "Handoff loop detected for {AgentName}. {HandoffSummary}",
                            agent.Name,
                            trajectory.GetHandoffSummary());

                        var criticApproval = await CriticAsync(
                            config,
                            runtimeModifier,
                            hooks,
                            activeSkills,
                            trajectory,
                            displayModelOutput,
                            agent,
                            originalInput,
                            newStepItems,
                            contextWrapper,
                            logger,
                            failureHook: () =>
                            {
                                // Add feedback about the loop
                                var loopDetectedMessage = $"Handoff loop detected: {trajectory.GetHandoffSummary()}. " +
                                                        "There appears to be transferring between agents repeatedly without making progress. " +
                                                        "Please review the conversation history and provide a substantive response to the user's query.";

                                var errorResult = new FunctionResultContent(functionCall.CallId, loopDetectedMessage);

                                functionResults.Add(errorResult);
                            });

                        if (!criticApproval)
                        {
                            logger.LogInternalInformation("Critic intervened to break handoff loop for {AgentName}", agent.Name);

                            continue;
                        }
                    }

                    // trigger critic on handoff attempt if configured
                    if (agent.CriticOnHandOff)
                    {
                        logger.LogInternalInformation(
                            "HandOff received from {AgentName}, Critic: {criticCount}/{MaxReflectionCount}",
                            agent.Name,
                            trajectory.GetCriticCount(agent.Name),
                            agent.MaxReflectionCount);

                        var criticApproval = await CriticAsync(
                            config,
                            runtimeModifier,
                            hooks,
                            activeSkills,
                            trajectory,
                            displayModelOutput,
                            agent,
                            originalInput,
                            newStepItems,
                            contextWrapper,
                            logger,
                            failureHook: () =>
                            {
                                // add fn result to deny
                                var handOffDeniedMessage = $"HandOff denied because of unsatisfactory response.";
                                var errorResult = new FunctionResultContent(functionCall.CallId, handOffDeniedMessage);

                                functionResults.Add(errorResult);
                            });

                        if (!criticApproval)
                        {
                            logger.LogInternalInformation("Critic failure {AgentName}. Running reasoning step again.", agent.Name);

                            continue;
                        }
                    }
                }

                // handle handoff if critic passed
                if (agent.HandoffNames.Contains(functionCall.Name))
                {
                    var handoff = agent.Handoffs.First(h => h.Name == functionCall.Name);
                    var newAgent = await handoff.OnInvokeHandoff(contextWrapper);

                    var (handoffReasoning, handoffResponse) = GetHandoffTransferMessage<TContext>(functionCall);

                    await hooks.OnHandoff(contextWrapper, agent, newAgent, handoffReasoning);

                    // Call OnAgentEnd for the agent that's handing off
                    await hooks.OnAgentEnd(contextWrapper, agent, handoffResponse);

                    var handoffResult = new FunctionResultContent(functionCall.CallId, handoffResponse);

                    functionResults.Add(handoffResult);

                    if (config.EnableDebugOutput
                        && displayModelOutput is not null)
                    {
                        await displayModelOutput.OnComplete($"[DEBUG]\n\nHandoff Completed. Previous Agent: {agent.Name} -> New Agent: {newAgent.Name}.");
                    }

                    handoffOccurred = true;
                    handoffNewAgent = newAgent;

                    continue;
                }

                // handle regular tool call

                if (tool is ContextAIFunction<TContext> contextTool)
                {
                    contextTool.SetContext(contextWrapper.Context);
                }

                // track newly activated skills
                if (runConfig.SkillRegistry is not null
                    && agent.EnableSkills
                    && tool?.Name == ReadSkillFileTool<TContext>.ToolName
                    && tool is ReadSkillFileTool<TContext> readSkillFileTool)
                {
                    var skillName = functionCall.Arguments?.TryGetValue(ReadSkillFileTool<TContext>.SkillNameParam, out var skillNameObj) == true
                        ? skillNameObj?.ToString()
                        : null;

                    var filePath = functionCall.Arguments?.TryGetValue(ReadSkillFileTool<TContext>.FilePathParam, out var filePathObj) == true
                        ? filePathObj?.ToString()
                        : null;

                    if (!string.IsNullOrEmpty(skillName)
                        && !string.IsNullOrEmpty(filePath)
                        && filePath.Contains("SKILL.md") // reading top level skill file
                        && activeSkills.GetSkillByName(skillName) is null) // skill not already active
                    {
                        var skill = runConfig.SkillRegistry.GetSkillByName(skillName, agent.AddSystemSkills, agent.AllowedSkills);
                        if (skill is not null)
                        {
                            newActivatedSkills.Enqueue(skill);
                        }
                    }
                }

                if (tool is not null)
                {
                    // check runtime type AgentAsTool
                    if (tool.IsAgentAsTool())
                    {
                        var agentAsTool = (AgentAsTool<TContext>)tool;
                        agentAsTool.RunConfig = runConfig;
                        agentAsTool.RunHooks = hooks;

                        // Store CallId for streaming
                        ToolStatic.AsyncLocalFunctionCallId.Value = functionCall.CallId;

                        await hooks.OnToolStart(contextWrapper, agent, functionCall, agentAsTool, functionCall.Arguments);

                        var toolResult = await agentAsTool.InvokeAsync(new AIFunctionArguments(functionCall.Arguments));

                        await hooks.OnToolEnd(contextWrapper, agent, functionCall, agentAsTool, toolResult);

                        var result = new FunctionResultContent(functionCall.CallId, toolResult);
                        functionResults.Add(result);

                        trajectory.Append(result);

                        if (config.EnableDebugOutput
                            && displayModelOutput is not null)
                        {
                            await displayModelOutput.OnComplete($"[DEBUG]\n\nCompleted Agent Invocation as Tool: {tool.Name}");
                        }

                        continue;
                    }
                    else if (tool.GetToolMode() == ToolMode.Auto || tool.IsMcpTool())
                    {
                        // Store CallId for streaming correlation
                        ToolStatic.AsyncLocalFunctionCallId.Value = functionCall.CallId;

                        // run auto tool
                        await hooks.OnToolStart(contextWrapper, agent, functionCall, tool, functionCall.Arguments);

                        object? toolResult = null;

                        try
                        {
                            toolResult = await tool.InvokeAsync(new AIFunctionArguments(functionCall.Arguments));

                            // Process tool output for potential truncation
                            if (runConfig.EnablePartialToolOutput && toolOutputProcessService != null)
                            {
                                toolResult = await toolOutputProcessService.ProcessToolOutputAsync(contextWrapper.Context, tool, toolResult, cancellationToken);
                            }
                        }
                        catch (Exception e)
                        {
                            // TODO Need logging.
                            toolResult = GetToolErrorMessage(functionCall, e);
                        }

                        if (config.EnableDebugOutput
                                && displayModelOutput is not null)
                        {
                            await displayModelOutput.OnComplete($"[DEBUG]\n\nTool Result: {functionCall.Name}"
                                + $"\n\nParameters: {functionCall.GetSerializedArguments()}"
                                + $"\n\nOutput: {JsonSerializer.Serialize(toolResult, AIJsonUtilities.DefaultOptions)}");
                        }

                        await hooks.OnToolEnd(contextWrapper, agent, functionCall, tool, toolResult);

                        var result = new FunctionResultContent(functionCall.CallId, toolResult);
                        functionResults.Add(result);
                        trajectory.Append(result);

                        if (config.EnableDebugOutput
                            && displayModelOutput is not null)
                        {
                            await displayModelOutput.OnComplete($"[DEBUG]\n\nCompleted Auto Invoked Tool: {tool.Name}");
                        }

                        continue;
                    }
                    else
                    {
                        // Store CallId for streaming correlation
                        ToolStatic.AsyncLocalFunctionCallId.Value = functionCall.CallId;

                        // return manual tool call result
                        await hooks.OnToolStart(contextWrapper, agent, functionCall, tool, functionCall.Arguments);

                        // modelResponseMessage will be added to the context when the loop is resumed

                        manualToolCalls.Add(new ManualToolCall
                        {
                            FunctionCall = functionCall,
                            Tool = tool,
                            OriginalMessage = modelResponseMessage
                        });

                        continue;
                    }
                }
                else
                {
                    // Handle unrecognized function calls by providing an error response
                    var errorMessage = $"Function '{functionCall.Name}' is not available. Available tools are: {string.Join(", ", tools.Select(t => t.Name))}";

                    var errorResult = new FunctionResultContent(functionCall.CallId, errorMessage);

                    functionResults.Add(errorResult);
                    trajectory.Append(errorResult);

                    continue;
                }
            }
        }

        // record model response messages in new items
        newStepItems.AddRange(modelResponse.Messages);

        // record function results in new items
        foreach (var fnResult in functionResults)
        {
            newStepItems.Add(new ChatMessage(ChatRole.Tool, [fnResult]));
        }

        if (handoffOccurred && handoffNewAgent is not null)
        {
            return new SingleStepResult<TContext>
            {
                OriginalInput = originalInput,
                ModelResponse = modelResponse,
                PreStepItems = preStepItems,
                NewStepItems = newStepItems,
                NextStep = new NextStep<TContext>
                {
                    Type = NextStepType.Handoff,
                    Agent = handoffNewAgent
                },
                NewActivatedSkills = [] // activated skills are not transferred during handoff
            };
        }
        else if (anyToolsCalled)
        {
            if (manualToolCalls.Count > 0)
            {
                return new SingleStepResult<TContext>
                {
                    OriginalInput = originalInput,
                    ModelResponse = modelResponse,
                    PreStepItems = preStepItems,
                    NewStepItems = newStepItems,
                    NextStep = new NextStep<TContext>
                    {
                        Type = NextStepType.ManualTool,
                        ManualToolCalls = manualToolCalls
                    },
                    NewActivatedSkills = newActivatedSkills
                };
            }

            // if we reach here, all tool calls were automatic, can run again immediately
            return new SingleStepResult<TContext>
            {
                OriginalInput = originalInput,
                ModelResponse = modelResponse,
                PreStepItems = preStepItems,
                NewStepItems = newStepItems,
                NextStep = new NextStep<TContext>
                {
                    Type = NextStepType.RunAgain
                },
                NewActivatedSkills = newActivatedSkills
            };
        }

        // if we reach here, there were no tool calls in the response
        if (agent.HasStructuredOutput
            && structuredOutput is not null
            && structuredOutput.GetType() == agent.OutputType)
        {
            // model produced output in the expected format
            return new SingleStepResult<TContext>
            {
                OriginalInput = originalInput,
                ModelResponse = modelResponse,
                PreStepItems = preStepItems,
                NewStepItems = newStepItems,
                NextStep = new NextStep<TContext>
                {
                    Type = NextStepType.FinalOutput,
                    Output = structuredOutput
                },
                NewActivatedSkills = newActivatedSkills
            };
        }
        else
        {
            return new SingleStepResult<TContext>
            {
                OriginalInput = originalInput,
                ModelResponse = modelResponse,
                PreStepItems = preStepItems,
                NewStepItems = newStepItems,
                NextStep = new NextStep<TContext>
                {
                    Type = NextStepType.FinalOutput,
                    Output = modelResponse.Text
                },
                NewActivatedSkills = newActivatedSkills
            };
        }
    }

    private static (string HandoffReasoning, string HandoffResponse) GetHandoffTransferMessage<TContext>(
        FunctionCallContent functionCall)
        where TContext : class
    {
        var handoffReasoning = string.Empty;
        if (functionCall.Arguments is not null
            && functionCall.Arguments.TryGetValue(Handoff<TContext>.ReasoningParam, out var reasoningObject)
            && reasoningObject is JsonElement reasoningElement
            && reasoningElement.ValueKind == JsonValueKind.String)
        {
            handoffReasoning = reasoningElement.GetString()!;
        }

        return (handoffReasoning, Handoff<TContext>.GetTransferMessage(handoffReasoning));
    }

    private static string GetToolErrorMessage(FunctionCallContent functionCall, Exception ex)
    {
        var message = $"Error: Function {functionCall.Name} failed, {ex.Message}";

        if (ex.InnerException != null)
        {
            message += $" | Inner exception: {ex.InnerException.Message}";
        }

        return message;
    }

    private static bool IsAllowedHandOff<TContext>(
        FunctionCallContent functionCall,
        Agent<TContext> agent,
        List<AIFunction> tools
        ) where TContext : class
    {
        // either in agent handoffs (transfer_to_*)
        return agent.HandoffNames.Contains(functionCall.Name)
            ||
            // or calling handoffback
            (tools.FirstOrDefault(t => t.Name == functionCall.Name) is var resolvedTool
            && resolvedTool is not null
            && string.Equals(resolvedTool.UnderlyingMethod?.Name, "HandoffBack", StringComparison.OrdinalIgnoreCase));
    }

    private class HandoffTracker
    {
        private readonly List<(string action, string? agentName)> _recentHandoffs = new();

        public void AddHandoff(string action, string? agentName)
        {
            _recentHandoffs.Add((action, agentName));

            // Keep only recent handoffs to avoid unbounded growth
            if (_recentHandoffs.Count > 20)
            {
                _recentHandoffs.RemoveAt(0);
            }
        }

        public (bool isLoop, string? loopingAgentName) DetectConsecutiveLoop(int requiredRepetitions = 3)
        {
            // Need at least the pattern length
            if (_recentHandoffs.Count < requiredRepetitions * 2)
            {
                return (false, null);
            }

            // Check the most recent handoffs for consecutive pattern
            var recentPattern = _recentHandoffs.TakeLast(requiredRepetitions * 2).ToList();

            // Verify alternating pattern and same agent
            string? targetAgent = null;

            for (var i = 0; i < recentPattern.Count; i++)
            {
                if (i % 2 == 0) // Should be transfer_to_
                {
                    if (recentPattern[i].action != "transfer")
                    {
                        return (false, null);
                    }

                    if (targetAgent == null)
                    {
                        targetAgent = recentPattern[i].agentName;
                    }
                    else if (recentPattern[i].agentName != targetAgent)
                    {
                        return (false, null);
                    }
                }
                else // Should be HandoffBack
                {
                    if (recentPattern[i].action != "handoffback")
                    {
                        return (false, null);
                    }
                }
            }

            return (true, targetAgent);
        }

        public void Clear()
        {
            _recentHandoffs.Clear();
        }
    }

    private static List<ChatMessage> ProcessHandoffPromptOverride<TContext>(List<ChatMessage> modelInput, Agent<TContext> agent)
        where TContext : class
    {
        const string OverrideHeader = "[RCA_HANDOFF_OVERRIDE]";

        // Look for User messages with the override header
        for (var i = 0; i < modelInput.Count; i++)
        {
            var message = modelInput[i];

            if (message.Role == ChatRole.User &&
                message.Text != null)
            {
                if (message.Text.Contains(OverrideHeader, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the original user query from the message
                    var originalUserQuery = ExtractUserQueryFromOverrideMessage(message.Text, OverrideHeader);

                    // Replace the message content with agent's UserPromptOverride
                    var newContent = $"{agent.UserPromptOverride}\n\n{Markers.UserQuestionMarker}\n{originalUserQuery}";

                    modelInput[i] = new ChatMessage(ChatRole.User, newContent);

                    // Log the override action (using simple console for now to avoid logger factory issues)
                    Console.WriteLine($"[Runner] Applied handoff prompt override for agent {agent.Name}");
                    break;
                }
                else
                {
                    var originalUserQuery = message.Text;
                    var newContent = $"{agent.UserPromptOverride}\n\n{Markers.UserQuestionMarker}\n{originalUserQuery}";
                    modelInput[i] = new ChatMessage(ChatRole.User, newContent);
                    break;
                }
            }
        }

        return modelInput;
    }

    private static string ExtractUserQueryFromOverrideMessage(string messageText, string header)
    {
        var lines = messageText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var userQueryLines = new List<string>();
        var foundUserSection = false;

        foreach (var line in lines)
        {
            // Skip the header line
            if (line.Trim().Equals(header, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Look for "User question goes below:" section
            if (line.Contains(Markers.UserQuestionMarker, StringComparison.OrdinalIgnoreCase))
            {
                foundUserSection = true;
                continue;
            }

            // If we found the user section, collect all following lines as user query
            if (foundUserSection)
            {
                userQueryLines.Add(line);
            }
        }

        return userQueryLines.Count > 0 ? string.Join('\n', userQueryLines).Trim() : messageText;
    }

    private static List<ChatMessage> SplitLastCompactedSummary(List<ChatMessage> modelInput)
    {
        // Reverse search to find the first assistant message that starts with ChatSummaryMarker
        for (var i = modelInput.Count - 1; i >= 0; i--)
        {
            var message = modelInput[i];

            if (message.Role == ChatRole.User
                && message.Text.StartsWith(Summarizer.ChatSummaryMarker))
            {
                // Return all messages from this point onward
                return modelInput.Skip(i).ToList();
            }
        }

        // If no compacted summary found, return the original input
        return modelInput;
    }

    #region Ambient Context Injection

    /// <summary>
    /// Injects ambient context (instructions, environment, pre-query) into the model input.
    /// Replicates Copilot Chat's context injection pattern.
    /// </summary>
    private static async Task AddAmbientContextAsync(
        List<ChatMessage> modelInput,
        IAmbientContextProvider provider,
        CancellationToken ct)
    {
        // 1. Insert INSTRUCTIONS context as first system message (after main system prompt)
        await AddInstructionsContextAsync(modelInput, provider, ct);

        // 2. Insert ENVIRONMENT context as first user message
        await AddEnvironmentContextAsync(modelInput, provider, ct);

        // 3. Before the final user message, insert PRE-QUERY context
        await AddPreUserQueryContextAsync(modelInput, provider, ct);

        // 4. Wrap the user's query in <user_query> tags
        WrapUserQueryMessage(modelInput);
    }

    private static async Task AddInstructionsContextAsync(
        List<ChatMessage> modelInput,
        IAmbientContextProvider provider,
        CancellationToken ct)
    {
        var instructionsContext = await provider.GetInstructionsContextAsync(ct);

        if (!string.IsNullOrWhiteSpace(instructionsContext))
        {
            // Insert as SYSTEM message right after the main system prompt (index 1)
            modelInput.Insert(1, new ChatMessage(ChatRole.System, instructionsContext));
        }
    }

    private static async Task AddEnvironmentContextAsync(
        List<ChatMessage> modelInput,
        IAmbientContextProvider provider,
        CancellationToken ct)
    {
        var envContext = await provider.GetEnvironmentContextAsync(ct);

        if (!string.IsNullOrWhiteSpace(envContext))
        {
            // Find index after system messages (first non-system message position)
            var insertIndex = modelInput.FindIndex(m => m.Role != ChatRole.System);
            if (insertIndex < 0)
            {
                insertIndex = modelInput.Count;
            }

            modelInput.Insert(insertIndex, new ChatMessage(ChatRole.User, envContext));
        }
    }

    private static async Task AddPreUserQueryContextAsync(
        List<ChatMessage> modelInput,
        IAmbientContextProvider provider,
        CancellationToken ct)
    {
        var preQueryContext = await provider.GetPreUserQueryContextAsync(ct);

        if (!string.IsNullOrWhiteSpace(preQueryContext))
        {
            // Insert as 2ND LAST message (before the user's query)
            // Find the last user message position
            var lastUserIndex = -1;
            for (var i = modelInput.Count - 1; i >= 0; i--)
            {
                if (modelInput[i].Role == ChatRole.User)
                {
                    lastUserIndex = i;
                    break;
                }
            }

            if (lastUserIndex >= 0)
            {
                modelInput.Insert(lastUserIndex, new ChatMessage(ChatRole.User, preQueryContext));
            }
            else
            {
                // No user message found, just append
                modelInput.Add(new ChatMessage(ChatRole.User, preQueryContext));
            }
        }
    }

    private static void WrapUserQueryMessage(List<ChatMessage> modelInput)
    {
        // Find the last user message (the actual user query)
        var lastUserIndex = -1;
        for (var i = modelInput.Count - 1; i >= 0; i--)
        {
            if (modelInput[i].Role == ChatRole.User)
            {
                lastUserIndex = i;
                break;
            }
        }

        if (lastUserIndex < 0)
        {
            return;
        }

        var originalContent = modelInput[lastUserIndex].Text;

        // Don't wrap if already wrapped or if it's a system-reminder/context message
        if (originalContent.Contains("<user_query>") ||
            originalContent.StartsWith("<system-reminder>") ||
            originalContent.StartsWith("<environment_info>") ||
            originalContent.StartsWith("<context>") ||
            originalContent.StartsWith("<attachments>"))
        {
            return;
        }

        var wrappedContent = $"<user_query>\n{originalContent}\n</user_query>";
        modelInput[lastUserIndex] = new ChatMessage(ChatRole.User, wrappedContent);
    }

    #endregion

    private const string EmptyToDoReminder =
    """
    <system-reminder>
    This is a reminder that your todo list is currently empty. DO NOT mention this to the user explicitly because they are already aware. If you are working on tasks that would benefit from a todo list please use the TodoWrite tool to create one. If not, please feel free to ignore. Again do not mention this message to the user.
    </system-reminder>
    """;

    private static void AddPlanReminderIfNeeded<TContext>(
        List<ChatMessage> modelInput,
        IReadOnlyList<AIFunction> tools
    ) where TContext : class
    {
        var needsPlan = tools
            .Any(t => string.Equals(ToDoWriteTool<TContext>.ToolName, t.Name, StringComparison.OrdinalIgnoreCase));
        if (!needsPlan)
        {
            return;
        }

        var lastPlanMessage = modelInput.LastOrDefault(
            m => m.Role == ChatRole.Assistant
            && m.Contents.Any(IsToDoWriteCall<TContext>));

        // if no active plan, add reminder and return
        if (lastPlanMessage is null)
        {
            modelInput.Add(new ChatMessage(ChatRole.User, EmptyToDoReminder));
            return;
        }

        // if there is active plan, always add reminder
        var lastPlanContent = lastPlanMessage.Contents.Last(IsToDoWriteCall<TContext>) as FunctionCallContent;
        var lastPlanTodos = ToDoWriteTool<TContext>.TryExtractTodos(new(lastPlanContent!.Arguments));
        if (lastPlanTodos is not null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<system-reminder>");
            sb.AppendLine("These are the latest contents of your todo list:");
            sb.AppendLine(lastPlanTodos);
            sb.AppendLine("Continue on with the tasks at hand if applicable.");
            sb.AppendLine("Ensure that you continue to use the todo list to track your progress.");
            //sb.AppendLine("While working on a user ask, do not remove any items from the todo list, even if completed.");
            //sb.AppendLine("You may only refresh the todo list from new, if the user intent changes.");
            sb.AppendLine("</system-reminder>");

            var planReminder = sb.ToString();

            modelInput.Add(new ChatMessage(ChatRole.User, planReminder));
        }
    }

    private static bool IsToDoWriteCall<TContext>(AIContent content) where TContext : class
    {
        return content is FunctionCallContent f
            && string.Equals(f.Name, ToDoWriteTool<TContext>.ToolName, StringComparison.OrdinalIgnoreCase);
    }

    private static List<AIFunction> ValidateTools(List<AIFunction> tools, ILogger logger)
    {
        var validTools = new List<AIFunction>();

        foreach (var tool in tools)
        {
            if (string.IsNullOrWhiteSpace(tool.Name) || tool.Name.Length > 64)
            {
                logger.LogCritical("Tool with invalid name '{ToolName}' was removed from the tool list.", tool.Name);
                continue;
            }

            // There's no documentation about the length limit.
            // Per test, gpt-4.1 and gpt-5 can at least support description length of 16384. Which is a reasonable high value.
            // Remove the check here unless we see errors again.
            // if (tool.Description.Length > 1024)
            // {
            //     logger.LogCritical("Tool '{ToolName}' has a description that is too long and was removed from the tool list.", tool.Name);
            //     continue;
            // }

            validTools.Add(tool);
        }

        return validTools;
    }
    /// <summary>
    /// Compacts chat history if input tokens exceed threshold, using Summarizer.CompactChatHistoryAsync.
    /// </summary>
    private static async Task<bool> CompactChatHistory<TContext>(
        Agent<TContext> startingAgent,
        List<ChatMessage> input,
        List<ChatMessage> generatedMessages,
        RunContextWrapper<TContext> contextWrapper,
        IChatClient chatClient,
        SkillList activeSkills,
        RunHooks<TContext> hooks,
        ILogger logger,
        string additionalInstructions = "",
        bool autoHandOffEnabled = false)
        where TContext : class
    {

        if (contextWrapper.CurrentStepUsageDetails.InputTokenCount > TokenThreshold)
        {
            if (hooks != null)
            {
                await hooks.OnCompactionStart(contextWrapper, startingAgent);
            }
            // Build chat history (input + generated)
            var chatHistory = new List<ChatMessage>();
            chatHistory.AddRange(input);
            chatHistory.AddRange(generatedMessages);

            // Use Summarizer to compact
            var compacted = await Summarizer.CompactChatHistoryAsync(
                additionalInstructions,
                chatHistory,
                startingAgent.Name,
                autoHandOffEnabled,
                chatClient,
                logger);

            // Append compacted summary as a new user message
            generatedMessages.Add(new ChatMessage(ChatRole.User, compacted));

            if (hooks != null)
            {
                await hooks.OnCompactionEnd(contextWrapper, startingAgent);
            }

            // reset active skills after compact
            if (activeSkills.Count > 0)
            {
                activeSkills.Clear();
                generatedMessages.Add(new ChatMessage(ChatRole.User, """
                    <system_notice>
                    Your active skills have been cleared.
                    You must re-evaluate which skills are currently needed (if any), and re-activate them by using the 'read_skill_file' tool.
                    </system_notice>
                    """));
            }

            return true;
        }

        return false;
    }

    private static List<ChatMessage> ParseCompactedInput(List<ChatMessage> originalInput, List<ChatMessage> generatedMessages)
    {
        var compactedInput = SplitLastCompactedSummary(generatedMessages);
        if (compactedInput.Count > 0 && compactedInput[0].Text != null && compactedInput[0].Text.StartsWith(Summarizer.ChatSummaryMarker))
        {
            // Return a new list to avoid mutating the input
            return new List<ChatMessage>(compactedInput);
        }
        // Otherwise, get compacted input from originalInput and append generatedMessages, but do not mutate either
        var baseInput = SplitLastCompactedSummary(originalInput);
        var result = new List<ChatMessage>(baseInput);
        result.AddRange(generatedMessages);
        return result;
    }

    private static void ProcessActiveSkills<TContext>(
        SkillList activeSkills,
        SkillList newActivatedSkills,
        Agent<TContext> agent,
        RunConfig runConfig
    ) where TContext : class
    {
        if (activeSkills.Count == 0 && newActivatedSkills.Count == 0)
        {
            return;
        }

        foreach (var skill in newActivatedSkills)
        {
            activeSkills.Enqueue(skill);
        }

        // remove oldest active skills until under limit
        while (activeSkills.Count > runConfig.MaxActiveSkills
            || (activeSkills.AllToolNames.Count() + agent.GetAllToolsCount()) > ToolCountHardMax)
        {
            activeSkills.TryDequeue(out var removedSkill);
        }
    }

    private static void AddSkillsReminderIfNeeded<TContext>(
        List<ChatMessage> modelInput,
        Agent<TContext> agent,
        SkillList activeSkills
    ) where TContext : class
    {
        if (!agent.EnableSkills)
        {
            return;
        }

        var sb = new StringBuilder();

        sb.AppendLine("<system-reminder>");

        if (activeSkills.Count == 0)
        {
            sb.AppendLine("You currently have no active skills. If you need to use any skills to assist with the user's request, please use the 'read_skill_file' tool to activate the necessary skills.");
            sb.AppendLine("Remember to only activate skills that are relevant to the current user's request.");
        }
        else
        {
            sb.AppendLine("These are your currently active skills:");
            foreach (var skill in activeSkills)
            {
                sb.AppendLine($"- {skill.Name}: {skill.Description}");
            }
            sb.AppendLine("Make sure to use the appropriate skills as needed.");
        }

        sb.AppendLine("</system-reminder>");

        var skillsReminder = sb.ToString();

        modelInput.Add(new ChatMessage(ChatRole.User, skillsReminder));
    }
}


