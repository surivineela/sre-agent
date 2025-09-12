// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
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

    public static async Task<RunResult<TContext>> ResumeFromManualToolsAsync<TContext>(
        RunResult<TContext> previousResult,
        List<ManualToolCallResult> manualToolResults,
        RunConfig config,
        IAgentRuntimeModifier<TContext>? runtimeModifier = null,
        TContext? context = null,
        RunHooks<TContext>? hooks = null,
        Func<string, Task>? displayModelOutput = null,
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

            if (config.EnableDebugOutput)
            {
                if (displayModelOutput is not null)
                {
                    await displayModelOutput($"[DEBUG]\n\nCompleted Manually Invoked Tool: {manualToolCall.FunctionCall.Name}");
                }
            }

            // todo: review: this may remove the text reasoning produced by the model
            if (!matchingResult.SkipToolCall)
            {
                var resultContent = new FunctionResultContent(manualToolCall.FunctionCall.CallId, matchingResult.Output);

                functionCallMessages.Add(manualToolCall.OriginalMessage);
                functionCallMessages.Add(new ChatMessage(ChatRole.Tool, [resultContent]));

                previousResult.Trajectory.Append(resultContent);
            }
            else if (matchingResult.ReplacementMessage != null)
            {
                functionCallMessages.Add(matchingResult.ReplacementMessage);
            }

            if (hooks != null)
            {
                await hooks.OnToolEnd(previousResult.ContextWrapper, previousResult.LastAgent, manualToolCall.Tool, matchingResult.Output);
            }
        }

        return await RunInternalAsync(
            startingAgent: previousResult.LastAgent,
            input: input,
            config: config,
            runtimeModifier: runtimeModifier,
            newGeneratedItems: functionCallMessages,
            context: context,
            currentTurn: previousResult.CurrentTurn,
            maxTurns: previousResult.MaxTurns,
            hooks: hooks,
            trajectory: previousResult.Trajectory,
            displayModelOutput: displayModelOutput,
            cancellationToken: cancellationToken,
            _shouldRunAgentStartHooks: previousResult.AgentChanged()
        );
    }

    public static Task<RunResult<TContext>> RunAsync<TContext>(
        Agent<TContext> startingAgent,
        List<ChatMessage> input,
        RunConfig config,
        IAgentRuntimeModifier<TContext>? runtimeModifier = null,
        TContext? context = null,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        Func<string, Task>? displayModelOutput = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        return RunInternalAsync(
            startingAgent: startingAgent,
            input: input,
            config: config,
            runtimeModifier: runtimeModifier,
            context: context,
            maxTurns: maxTurns,
            hooks: hooks,
            displayModelOutput: displayModelOutput,
            cancellationToken: cancellationToken,
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
        IAgentRuntimeModifier<TContext>? runtimeModifier = null,
        TContext? context = null,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        Func<string, Task>? displayModelOutput = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        var handoffDetected = false;
        string? handoffTargetAgent = null;

        // Create custom hooks that detect handoff
        var handoffDetectionHooks = hooks ?? new RunHooks<TContext>();
        var originalOnHandoff = handoffDetectionHooks.OnHandoff;

        handoffDetectionHooks.OnHandoff = async (context, fromAgent, toAgent, handoffReasoning) =>
        {
            handoffDetected = true;
            handoffTargetAgent = toAgent.Name;

            // Call original hook if it exists
            if (originalOnHandoff != null)
            {
                await originalOnHandoff(context, fromAgent, toAgent, handoffReasoning);
            }
        };

        try
        {
            var runResult = await RunInternalAsync(
                startingAgent: startingAgent,
                input: input,
                config: config,
                runtimeModifier: runtimeModifier,
                context: context,
                maxTurns: maxTurns,
                hooks: handoffDetectionHooks,
                displayModelOutput: displayModelOutput,
                cancellationToken: cancellationToken,
                _shouldRunAgentStartHooks: true
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
        IAgentRuntimeModifier<TContext>? runtimeModifier = null,
        List<ChatMessage>? newGeneratedItems = null,
        TContext? context = null,
        int currentTurn = 0,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        Trajectory? trajectory = null,
        Func<string, Task>? displayModelOutput = null,
        bool _shouldRunAgentStartHooks = true,
        CancellationToken cancellationToken = default // TODO: use cancellation token
    ) where TContext : class
    {
        hooks ??= new RunHooks<TContext>();
        bool shouldRunAgentStartHooks = _shouldRunAgentStartHooks;
        Agent<TContext> currentAgent = startingAgent;
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
                    };

                    throw new TurnLimitReachedException<TContext>(
                        "Max turns reached",
                        result
                    );
                }

                var turnResult = await RunSingleTurnAsync(
                    agent: currentAgent,
                    originalInput: originalInput,
                    generatedMessages: generatedMessages,
                    config: config,
                    runtimeModifier: runtimeModifier,
                    contextWrapper: contextWrapper,
                    hooks: hooks,
                    shouldRunAgentStartHooks: shouldRunAgentStartHooks,
                    trajectory: trajectory,
                    logger: logger,
                    displayModelOutput: displayModelOutput
                );

                shouldRunAgentStartHooks = false;
                originalInput = turnResult.OriginalInput;
                generatedMessages = turnResult.GeneratedItems;
                rawResponses.Add(turnResult.ModelResponse);

                if (displayModelOutput is not null)
                {
                    foreach (var message in turnResult.ModelResponse.Messages)
                    {
                        foreach (var content in message.Contents.OfType<TextContent>())
                        {
                            var text = Summarizer.ExtractNotifyUserMessage(content.Text);
                            await displayModelOutput(text);
                        }
                    }
                }

                if (config.EnableDebugOutput)
                {
                    if (displayModelOutput is not null)
                    {
                        foreach (var message in turnResult.ModelResponse.Messages)
                        {
                            foreach (var content in message.Contents)
                            {
                                if (content is TextContent t)
                                {
                                    await displayModelOutput($"[DEBUG]\n\nAgent: {currentAgent.Name}\n\nResponse:{t.Text}");
                                }
                                else if (content is FunctionCallContent f)
                                {
                                    await displayModelOutput($"[DEBUG]\n\nAgent: {currentAgent.Name}"
                                        + $"\n\nFunction Call: {f.Name}"
                                        + $"\n\nParameters: {(f.RawRepresentation as OpenAI.Chat.ChatToolCall)!.FunctionArguments.ToString()}");
                                }
                            }
                        }
                    }
                }

                if (turnResult.NextStep.Type == NextStepType.FinalOutput)
                {
                    logger.LogInformation(
                        "FinalOutput received from {AgentName}, Critic: {criticCount}/{MaxReflectionCount}",
                        currentAgent.Name,
                        trajectory.GetCriticCount(currentAgent.Name),
                        currentAgent.MaxReflectionCount);

                    var criticApproval = await CriticAsync(
                        config,
                        runtimeModifier,
                        hooks,
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
                    };
                }
                else if (turnResult.NextStep.Type == NextStepType.Handoff
                    && turnResult.NextStep.Agent is not null)
                {
                    currentAgent = turnResult.NextStep.Agent;
                    shouldRunAgentStartHooks = true;
                    // we should not reset the trajectory, as it may contain important information for critic as handoff is very cheap frequent behavior.
                }
                else if (turnResult.NextStep.Type == NextStepType.RunAgain)
                {
                    // do nothing, we will run the agent again
                }
                else if (turnResult.NextStep.Type == NextStepType.ManualTool
                    && turnResult.NextStep.ManualToolCall is not null)
                {
                    return new RunResult<TContext>(currentAgent)
                    {
                        Input = originalInput,
                        NewItems = generatedMessages,
                        Output = turnResult.NextStep.Output,
                        ContextWrapper = contextWrapper,
                        ManualToolCalls = [turnResult.NextStep.ManualToolCall],
                        CurrentTurn = currentTurn,
                        MaxTurns = maxTurns,
                        RawResponses = rawResponses,
                        Trajectory = trajectory
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
            logger.LogInformation("Operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError($"Exception thrown while executing reasoning loop: {ex.Message}");
            // todo: log
            throw;
        }
    }

    private static async Task<bool> CriticAsync<TContext>(
        RunConfig config,
        IAgentRuntimeModifier<TContext>? runtimeModifier,
        RunHooks<TContext> hooks,
        Trajectory trajectory,
        Func<string, Task>? displayModelOutput,
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
                    await displayModelOutput($"[DEBUG]\n\nGathering critique: Agent: {currentAgent.Name}. Turn #{criticCount}/{currentAgent.MaxReflectionCount}");
                }
            }

            await hooks.OnSummarizerStart(contextWrapper, currentAgent);

            var userQuery = await Summarizer.SummarizeUserTrajectoryAsync(
                config.ChatClient,
                originalInput);

            await hooks.OnSummarizerEnd(contextWrapper, currentAgent, userQuery);

            if (config.EnableDebugOutput)
            {
                if (displayModelOutput is not null)
                {
                    await displayModelOutput($"[DEBUG]\n\nSummarized User Query: {userQuery}");
                }
            }

            await hooks.OnCriticStart(contextWrapper, currentAgent, criticCount);

            var trajectoryString = trajectory.GetFilteredTrajectory();

            var agentTools = await hooks.ResolveFactoryTools(contextWrapper, currentAgent);

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

                if (config.EnableDebugOutput)
                {
                    if (displayModelOutput is not null)
                    {
                        await displayModelOutput($"[DEBUG]\n\nCritic failed the turn:\n\n{criticResult}");
                    }
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
                logger.LogInformation("Critic approved response: {CriticResult}", criticResult);
                if (config.EnableDebugOutput)
                {
                    if (displayModelOutput is not null)
                    {
                        await displayModelOutput($"[DEBUG]\n\nCritic approved the turn.");
                    }
                }
            }
        }
        else
        {
            logger.LogInformation(
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

    public static async Task<SingleStepResult<TContext>> RunSingleTurnAsync<TContext>(
        Agent<TContext> agent,
        List<ChatMessage> originalInput,
        List<ChatMessage> generatedMessages,
        RunConfig config,
        IAgentRuntimeModifier<TContext>? runtimeModifier,
        RunContextWrapper<TContext> contextWrapper,
        RunHooks<TContext> hooks,
        bool shouldRunAgentStartHooks,
        Trajectory trajectory,
        ILogger logger,
        Func<string, Task>? displayModelOutput = null
    ) where TContext : class
    {
        logger.LogInformation("Running agent {AgentName} with runtime modifier {HasRuntimeModifier}", agent.Name, runtimeModifier != null);
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

        // Use original instructions without DI additions if DisableCommonPrompts is true
        var systemPrompt = agent.DisableCommonPrompts
            ? agent.Instructions.GetOriginalText()
            : agent.Instructions.ToString();

        List<AIFunction> tools = [];
        tools.AddRange(agent.Tools);
        tools.AddRange(await hooks.ResolveFactoryTools(contextWrapper, agent));
        tools.AddRange(agent.Handoffs);

        var chatOptions = new ChatOptions
        {
            Tools = tools.Cast<AITool>().ToList(),
            ToolMode = agent.ChatToolMode,
            Temperature = agent.Temperature,
            AllowMultipleToolCalls = tools.Count > 0
                ? false // agent.AllowParallelToolCalls TODO: not supported yet
                : null // if there are no tools this value needs to be null, not false
        };

        var chatClient = agent.GetChatClient(config);

        List<ChatMessage> modelInput = [new ChatMessage(ChatRole.System, systemPrompt)];
        modelInput.AddRange(originalInput);
        modelInput.AddRange(generatedMessages);

        // Process handoff prompt override if current agent has UserPromptOverride
        if (!string.IsNullOrEmpty(agent.UserPromptOverride))
        {
            modelInput = ProcessHandoffPromptOverride(modelInput, agent);
        }

        // Add plan reminder if required
        AddPlanReminderIfNeeded(modelInput, tools);

        // tool invocations like metrics query depend on current time
        modelInput.Add(new ChatMessage(ChatRole.System, $"The current date is {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}"));

        await hooks.OnModelGenerationStart(contextWrapper, agent, modelInput, chatOptions);

        ChatResponse response;
        object? structuredOutput = null;

        if (agent.HasStructuredOutput)
        {
            (response, structuredOutput) = await chatClient.GetResponseAsync(modelInput, agent.OutputType, chatOptions);
        }
        else
        {
            response = await chatClient.GetResponseAsync(modelInput, chatOptions);
        }

        await hooks.OnModelGenerationEnd(contextWrapper, agent, response);

        trajectory.Append(response);

        if (response.Usage != null)
        {
            contextWrapper.UsageDetails.Add(response.Usage);
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
            logger: logger,
            displayModelOutput: displayModelOutput
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
        ILogger logger,
        Func<string, Task>? displayModelOutput = null
    ) where TContext : class
    {
        List<ChatMessage> newStepItems = [];

        // process tool calls
        // assume no parallel tool calling, so if a regular tool is called, we are not handing off to another agent
        foreach (var modelResponseMessage in modelResponse.Messages)
        {
            var functionCalls = modelResponseMessage.Contents.OfType<FunctionCallContent>();

            foreach (var functionCall in functionCalls)
            {
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
                                newStepItems.Add(modelResponseMessage);
                                var errorResult = new FunctionResultContent(functionCall.CallId, loopDetectedMessage);
                                newStepItems.Add(new ChatMessage(ChatRole.Tool, [errorResult]));
                            });

                        if (!criticApproval)
                        {
                            logger.LogInformation("Critic intervened to break handoff loop for {AgentName}", agent.Name);
                            return new SingleStepResult<TContext>
                            {
                                OriginalInput = originalInput,
                                ModelResponse = modelResponse,
                                PreStepItems = preStepItems,
                                NewStepItems = newStepItems,
                                NextStep = new NextStep<TContext>
                                {
                                    Type = NextStepType.RunAgain,
                                }
                            };
                        }
                    }

                    // trigger critic on handoff attempt if configured
                    if (agent.CriticOnHandOff)
                    {
                        logger.LogInformation(
                            "HandOff received from {AgentName}, Critic: {criticCount}/{MaxReflectionCount}",
                            agent.Name,
                            trajectory.GetCriticCount(agent.Name),
                            agent.MaxReflectionCount);

                        var criticApproval = await CriticAsync(
                            config,
                            runtimeModifier,
                            hooks,
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
                                newStepItems.Add(modelResponseMessage);
                                var errorResult = new FunctionResultContent(functionCall.CallId, handOffDeniedMessage);
                                newStepItems.Add(new ChatMessage(ChatRole.Tool, [errorResult]));
                            });

                        if (!criticApproval)
                        {
                            logger.LogInformation("Critic failure {AgentName}. Running reasoning step again.", agent.Name);

                            return new SingleStepResult<TContext>
                            {
                                OriginalInput = originalInput,
                                ModelResponse = modelResponse,
                                PreStepItems = preStepItems,
                                NewStepItems = newStepItems,
                                NextStep = new NextStep<TContext>
                                {
                                    Type = NextStepType.RunAgain,
                                }
                            };
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

                    var handoffResult = new FunctionResultContent(functionCall.CallId, handoffResponse);
                    newStepItems.Add(modelResponseMessage);
                    newStepItems.Add(new ChatMessage(ChatRole.Tool, [handoffResult]));

                    if (config.EnableDebugOutput)
                    {
                        if (displayModelOutput is not null)
                        {
                            await displayModelOutput($"[DEBUG]\n\nHandoff Completed. Previous Agent: {agent.Name} -> New Agent: {newAgent.Name}.");
                        }
                    }

                    return new SingleStepResult<TContext>
                    {
                        OriginalInput = originalInput,
                        ModelResponse = modelResponse,
                        PreStepItems = preStepItems,
                        NewStepItems = newStepItems,
                        NextStep = new NextStep<TContext>
                        {
                            Type = NextStepType.Handoff,
                            Agent = newAgent
                        }
                    };
                }

                // handle regular tool call
                AIFunction? tool = tools.FirstOrDefault(t => t.Name == functionCall.Name);

                if (tool is ContextAIFunction<TContext> contextTool)
                {
                    contextTool.SetContext(contextWrapper.Context);
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

                        await hooks.OnToolStart(contextWrapper, agent, agentAsTool, functionCall.Arguments);

                        var toolResult = await agentAsTool.InvokeAsync(new AIFunctionArguments(functionCall.Arguments));

                        await hooks.OnToolEnd(contextWrapper, agent, agentAsTool, toolResult);

                        var result = new FunctionResultContent(functionCall.CallId, toolResult);
                        newStepItems.Add(modelResponseMessage);
                        newStepItems.Add(new ChatMessage(ChatRole.Tool, [result]));
                        trajectory.Append(result);

                        if (config.EnableDebugOutput)
                        {
                            if (displayModelOutput is not null)
                            {
                                await displayModelOutput($"[DEBUG]\n\nCompleted Agent Invocation as Tool: {tool.Name}");
                            }
                        }

                        return new SingleStepResult<TContext>
                        {
                            OriginalInput = originalInput,
                            ModelResponse = modelResponse,
                            PreStepItems = preStepItems,
                            NewStepItems = newStepItems,
                            NextStep = new NextStep<TContext>
                            {
                                Type = NextStepType.RunAgain
                            }
                        };
                    }
                    else if (tool.GetToolMode() == ToolMode.Auto)
                    {
                        // Store CallId for streaming correlation
                        ToolStatic.AsyncLocalFunctionCallId.Value = functionCall.CallId;

                        // run auto tool
                        await hooks.OnToolStart(contextWrapper, agent, tool, functionCall.Arguments);

                        object? toolResult = null;

                        try
                        {
                            toolResult = await tool.InvokeAsync(new AIFunctionArguments(functionCall.Arguments));
                        }
                        catch (Exception e)
                        {
                            // TODO Need logging.
                            toolResult = GetToolErrorMessage(functionCall, e);
                        }

                        await hooks.OnToolEnd(contextWrapper, agent, tool, toolResult);

                        var result = new FunctionResultContent(functionCall.CallId, toolResult);
                        newStepItems.Add(modelResponseMessage);
                        newStepItems.Add(new ChatMessage(ChatRole.Tool, [result]));
                        trajectory.Append(result);

                        if (config.EnableDebugOutput)
                        {
                            if (displayModelOutput is not null)
                            {
                                await displayModelOutput($"[DEBUG]\n\nCompleted Auto Invoked Tool: {tool.Name}");
                            }
                        }

                        return new SingleStepResult<TContext>
                        {
                            OriginalInput = originalInput,
                            ModelResponse = modelResponse,
                            PreStepItems = preStepItems,
                            NewStepItems = newStepItems,
                            NextStep = new NextStep<TContext>
                            {
                                Type = NextStepType.RunAgain
                            }
                        };
                    }
                    else
                    {
                        // Store CallId for streaming correlation
                        ToolStatic.AsyncLocalFunctionCallId.Value = functionCall.CallId;

                        // return manual tool call result
                        await hooks.OnToolStart(contextWrapper, agent, tool, functionCall.Arguments);

                        // modelResponseMessage will be added to the context when the loop is resumed

                        return new SingleStepResult<TContext>
                        {
                            OriginalInput = originalInput,
                            ModelResponse = modelResponse,
                            PreStepItems = preStepItems,
                            NewStepItems = newStepItems,
                            NextStep = new NextStep<TContext>
                            {
                                Type = NextStepType.ManualTool,
                                ManualToolCall = new ManualToolCall
                                {
                                    FunctionCall = functionCall,
                                    Tool = tool,
                                    OriginalMessage = modelResponseMessage
                                }
                            }
                        };
                    }
                }
                else
                {
                    // Handle unrecognized function calls by providing an error response
                    var errorMessage = $"Function '{functionCall.Name}' is not available. Available tools are: {string.Join(", ", tools.Select(t => t.Name))}";

                    var errorResult = new FunctionResultContent(functionCall.CallId, errorMessage);

                    newStepItems.Add(modelResponseMessage);
                    newStepItems.Add(new ChatMessage(ChatRole.Tool, [errorResult]));
                    trajectory.Append(errorResult);

                    return new SingleStepResult<TContext>
                    {
                        OriginalInput = originalInput,
                        ModelResponse = modelResponse,
                        PreStepItems = preStepItems,
                        NewStepItems = newStepItems,
                        NextStep = new NextStep<TContext>
                        {
                            Type = NextStepType.RunAgain
                        }
                    };
                }
            }
        }

        if (agent.HasStructuredOutput)
        {
            if (structuredOutput is not null && structuredOutput.GetType() == agent.OutputType)
            {
                newStepItems.AddRange(modelResponse.Messages);

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
                    }
                };
            }
            else
            {
                // model produced output in an unexpected format
                throw new Exception("Model produced output in an unexpected format");
            }
        }
        else
        {
            // if we reach here, there were no tool calls in the response
            newStepItems.AddRange(modelResponse.Messages);

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
                }
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

            for (int i = 0; i < recentPattern.Count; i++)
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
        for (int i = 0; i < modelInput.Count; i++)
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
        bool foundUserSection = false;

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

    private static void AddPlanReminderIfNeeded(List<ChatMessage> modelInput, IReadOnlyList<AIFunction> tools)
    {
        const string ToDoReminder =
        """
        <system-reminder>
        This is a reminder that your todo list is currently empty. DO NOT mention this to the user explicitly because they are already aware. If you are working on tasks that would benefit from a todo list please use the TodoWrite tool to create one. If not, please feel free to ignore. Again do not mention this message to the user.
        </system-reminder>
        """;

        var needsPlan = tools
            .Any(t => string.Equals(ToDoWriteTool.ToolName, t.Name, StringComparison.OrdinalIgnoreCase));
        if (needsPlan)
        {
            var hasPlan = modelInput.Any(m => m.Role == ChatRole.Assistant
                && m.Contents.Any(c => c is FunctionCallContent f
                && string.Equals(f.Name, ToDoWriteTool.ToolName, StringComparison.OrdinalIgnoreCase)));
            if (!hasPlan)
            {
                modelInput.Add(new ChatMessage(ChatRole.User, ToDoReminder));
            }
        }
    }
}
