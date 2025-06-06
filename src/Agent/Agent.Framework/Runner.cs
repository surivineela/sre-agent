// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Framework;

public static class Runner
{
    private const int DefaultMaxTurns = 50;

    public static async Task<RunResult<TContext>> ResumeFromManualToolsAsync<TContext>(
        RunResult<TContext> previousResult,
        IReadOnlyList<ManualToolCallResult> manualToolResults,
        RunConfig config,
        TContext? context = null,
        RunHooks<TContext>? hooks = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        if (previousResult.ManualToolCalls is null
            || previousResult.ManualToolCalls.Count == 0)
        {
            throw new Exception("No manual tool calls found");
        }

        var functionResultMessage = new ChatMessage(ChatRole.Tool, []);
        foreach (var manualToolCall in previousResult.ManualToolCalls)
        {
            var matchingOutput = manualToolResults
                ?.FirstOrDefault(o => o.FunctionCall.CallId == manualToolCall.FunctionCall.CallId)
                ?.Output
                ?? throw new Exception("No matching output found for manual tool call");

            var resultMessage = new FunctionResultContent(
                manualToolCall.FunctionCall.CallId,
                matchingOutput);

            functionResultMessage.Contents.Add(resultMessage);

            previousResult.Trajectory.Append(resultMessage);

            if (hooks != null)
            {
                await hooks.OnToolEnd(previousResult.ContextWrapper, previousResult.LastAgent, manualToolCall.Tool, matchingOutput);
            }
        }

        IReadOnlyList<ChatMessage> newInput = [.. previousResult.Input, .. previousResult.NewItems];
        IReadOnlyList<ChatMessage> functionResultMessages = [functionResultMessage];

        return await RunInternalAsync(
            startingAgent: previousResult.LastAgent,
            originalInput: newInput,
            config: config,
            preStepMessages: functionResultMessages,
            context: context,
            currentTurn: previousResult.CurrentTurn,
            maxTurns: previousResult.MaxTurns,
            hooks: hooks,
            previousResult.Trajectory,
            cancellationToken: cancellationToken
        );
    }

    public static Task<RunResult<TContext>> RunAsync<TContext>(
        Agent<TContext> startingAgent,
        IReadOnlyList<ChatMessage> input,
        RunConfig config,
        TContext? context = null,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        return RunInternalAsync(
            startingAgent: startingAgent,
            originalInput: input,
            config: config,
            context: context,
            maxTurns: maxTurns,
            hooks: hooks,
            cancellationToken: cancellationToken
        );
    }

    private static async Task<RunResult<TContext>> RunInternalAsync<TContext>(
        Agent<TContext> startingAgent,
        IReadOnlyList<ChatMessage> originalInput,
        RunConfig config,
        IReadOnlyList<ChatMessage>? preStepMessages = null,
        TContext? context = null,
        int currentTurn = 0,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        Trajectory? trajectory = null,
        CancellationToken cancellationToken = default // TODO: use cancellation token
    ) where TContext : class
    {
        hooks ??= new RunHooks<TContext>();

        var shouldRunAgentStartHooks = true;
        var currentAgent = startingAgent;
        List<ChatMessage> generatedMessages = preStepMessages is not null
            ? [.. preStepMessages]
            : [];
        List<ChatMessage> input = [.. originalInput];
        trajectory ??= new Trajectory();

        var contextWrapper = new RunContextWrapper<TContext>(context);

        var logger = config.LoggerFactory.CreateLogger("Agent.Framework.Runner");

        try
        {
            while (true)
            {
                currentTurn += 1;

                if (currentTurn > maxTurns)
                {
                    throw new Exception("Max turns reached");
                }

                var turnResult = await RunSingleTurnAsync(
                    agent: currentAgent,
                    originalInput: input,
                    preStepMessages: generatedMessages,
                    config: config,
                    contextWrapper: contextWrapper,
                    hooks: hooks,
                    shouldRunAgentStartHooks: shouldRunAgentStartHooks,
                    trajectory: trajectory
                );

                shouldRunAgentStartHooks = false;

                generatedMessages = turnResult.GeneratedItems;

                if (turnResult.NextStep.Type == NextStepType.FinalOutput)
                {
                    logger.LogInformation(
                        "FinalOutput received from {AgentName}, Critic: {criticCount}/{MaxReflectionCount}",
                        currentAgent.Name,
                        trajectory.CriticCount,
                        currentAgent.MaxReflectionCount);

                    if (currentAgent.MaxReflectionCount > 0 && trajectory.CriticCount < currentAgent.MaxReflectionCount)
                    {
                        var userQuery = await Summarizer.SummarizeUserMessagesAsync(
                            config.ChatClient,
                            input);

                        var trajectoryString = trajectory.Close();

                        var agentTools = await hooks.ResolveFactoryTools(contextWrapper, currentAgent);

                        var criticResult = await Critic.CriticAsync(
                            currentAgent,
                            userQuery,
                            trajectoryString,
                            agentTools,
                            config.ChatClient);

                        if (criticResult.Contains("\"overall_assessment\": \"FAIL\""))
                        {
                            logger.LogWarning("Critic result indicates failure: {CriticResult}", criticResult);

                            // todo: compact the history so far..

                            //var trajectorySummary = await Summarizer.SummarizeActorTrajectoryAsync(
                            //    userQuery,
                            //    trajectoryString,
                            //    config.ChatClient);

                            //input = [.. input, .. generatedMessages];
                            //generatedMessages = [];

                            //var handoffTranfer = Handoff<TContext>.GetTransferMessage(currentAgent.Name);
                            //var lastHandoffIndex = input
                            //    .FindLastIndex(m => m.Role == ChatRole.Tool
                            //        && m.Contents.Count == 1
                            //        && m.Contents[0] is FunctionResultContent f
                            //        && string.Equals(handoffTranfer, f.Result?.ToString(), StringComparison.OrdinalIgnoreCase));
                            //var messagesToKeep = lastHandoffIndex == -1
                            //    ? 1 // the original user message
                            //    : (lastHandoffIndex + 1 // story until the handoff message
                            //    + (trajectory.CriticCount - 1) * 2); // criticCount * 2 (1 summary message, 1 feedback message)

                            //var feedBack = new List<ChatMessage>()
                            //{
                            //    new(ChatRole.Assistant, "Past run summary:\n" + trajectorySummary),
                            //    new(ChatRole.User, "Past run feedback:\n" + criticResult),
                            //};

                            //input = input
                            //    .Take(messagesToKeep)
                            //    .Concat(feedBack)
                            //    .ToList();

                            generatedMessages.Add(new(ChatRole.User, "Unsatisfactory response. Try again with new tool calls as needed. Feedback:\n" + criticResult));

                            continue;
                        }
                        else
                        {
                            logger.LogInformation("Critic approved response: {CriticResult}", criticResult);
                        }
                    }

                    await hooks.OnAgentEnd(contextWrapper, currentAgent, turnResult.NextStep.Output);

                    return new RunResult<TContext>(currentAgent)
                    {
                        Input = input,
                        NewItems = generatedMessages,
                        Output = turnResult.NextStep.Output,
                        ContextWrapper = contextWrapper,
                        CurrentTurn = currentTurn,
                        MaxTurns = maxTurns,
                        Trajectory = trajectory,
                    };
                }
                else if (turnResult.NextStep.Type == NextStepType.Handoff && turnResult.NextStep.Agent != null)
                {
                    currentAgent = turnResult.NextStep.Agent;
                    shouldRunAgentStartHooks = true;
                    // clear out the past trajectory.. we track new path for new agent
                    trajectory = new Trajectory();
                }
                else if (turnResult.NextStep.Type == NextStepType.RunAgain)
                {
                    // do nothing, we will run the agent again
                }
                else if (turnResult.NextStep.Type == NextStepType.ManualTool && turnResult.NextStep.ManualToolCall != null)
                {
                    return new RunResult<TContext>(currentAgent)
                    {
                        Input = input,
                        NewItems = generatedMessages,
                        Output = turnResult.NextStep.Output,
                        ContextWrapper = contextWrapper,
                        ManualToolCalls = [turnResult.NextStep.ManualToolCall],
                        CurrentTurn = currentTurn,
                        MaxTurns = maxTurns,
                        Trajectory = trajectory
                    };
                }
                else
                {
                    throw new Exception("Unknown next step type");
                }
            }
        }
        catch (Exception)
        {
            // todo: log
            throw;
        }
    }

    public static async Task<SingleStepResult<TContext>> RunSingleTurnAsync<TContext>(
        Agent<TContext> agent,
        IReadOnlyList<ChatMessage> originalInput,
        IReadOnlyList<ChatMessage> preStepMessages,
        RunConfig config,
        RunContextWrapper<TContext> contextWrapper,
        RunHooks<TContext> hooks,
        bool shouldRunAgentStartHooks,
        Trajectory trajectory
    ) where TContext : class
    {
        if (shouldRunAgentStartHooks)
        {
            await hooks.OnAgentStart(contextWrapper, agent);
        }

        var systemPrompt = agent.Instructions;

        List<AIFunction> tools = [];
        tools.AddRange(agent.Tools);
        tools.AddRange(await hooks.ResolveFactoryTools(contextWrapper, agent));
        tools.AddRange(agent.Handoffs);

        var chatOptions = new ChatOptions
        {
            Tools = tools.Cast<AITool>().ToList(),
            ToolMode = agent.ChatToolMode,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["AllowParallelToolCalls"] = agent.AllowParallelToolCalls,
            },
            Temperature = agent.Temperature
        };

        var chatClient = agent.GetChatClient(config);

        List<ChatMessage> modelInput = [new ChatMessage(ChatRole.System, systemPrompt)];
        modelInput.AddRange(originalInput);
        modelInput.AddRange(preStepMessages);

        var response = await chatClient.GetResponseAsync(modelInput, chatOptions);
        trajectory.Append(response);

        if (response.Usage != null)
        {
            contextWrapper.UsageDetails.Add(response.Usage);
        }

        return await ExecuteToolsAndHandoffsAsync(
            agent: agent,
            originalInput: originalInput,
            preStepItems: preStepMessages,
            modelResponse: response,
            hooks: hooks,
            contextWrapper: contextWrapper,
            tools: tools,
            trajectory: trajectory
        );
    }

    private static async Task<SingleStepResult<TContext>> ExecuteToolsAndHandoffsAsync<TContext>(
        Agent<TContext> agent,
        IReadOnlyList<ChatMessage> originalInput,
        IReadOnlyList<ChatMessage> preStepItems,
        ChatResponse modelResponse,
        RunHooks<TContext> hooks,
        RunContextWrapper<TContext> contextWrapper,
        List<AIFunction> tools,
        Trajectory trajectory
    ) where TContext : class
    {
        List<ChatMessage> newStepItems = [.. modelResponse.Messages];

        // process tool calls
        // assume no parallel tool calling, so if a regular tool is called, we are not handing off to another agent
        foreach (var message in modelResponse.Messages)
        {
            var functionCalls = message.Contents.OfType<FunctionCallContent>();

            foreach (var functionCall in functionCalls)
            {
                // handle handoff
                if (agent.HandoffNames.Contains(functionCall.Name))
                {
                    var handoff = agent.Handoffs.First(h => h.Name == functionCall.Name);
                    var newAgent = await handoff.OnInvokeHandoff(contextWrapper);

                    await hooks.OnHandoff(contextWrapper, agent, newAgent);

                    // review: don't really see value in adding handoff as new messages
                    // openai probably did it cause that's how they are forced to expose tool calls..
                    // for us it just adds tokens
                    newStepItems.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(functionCall.CallId, handoff.TransferMessage)]));

                    return new SingleStepResult<TContext>
                    {
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

                if (tool != null)
                {
                    if (tool is ContextAIFunction<TContext> contextTool)
                    {
                        contextTool.SetContext(contextWrapper.Context);
                    }

                    if (tool.GetToolMode() == ToolMode.Auto)
                    {
                        // run auto tool
                        await hooks.OnToolStart(contextWrapper, agent, tool);

                        var toolResult = await tool.InvokeAsync(functionCall.Arguments);

                        await hooks.OnToolEnd(contextWrapper, agent, tool, toolResult);

                        var result = new FunctionResultContent(functionCall.CallId, toolResult);

                        newStepItems.Add(new ChatMessage(ChatRole.Tool, [result]));
                        trajectory.Append(result);

                        return new SingleStepResult<TContext>
                        {
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
                        // return manual tool call result
                        await hooks.OnToolStart(contextWrapper, agent, tool);

                        return new SingleStepResult<TContext>
                        {
                            ModelResponse = modelResponse,
                            PreStepItems = preStepItems,
                            NewStepItems = newStepItems,
                            NextStep = new NextStep<TContext>
                            {
                                Type = NextStepType.ManualTool,
                                ManualToolCall = new ManualToolCall { FunctionCall = functionCall, Tool = tool }
                            }
                        };
                    }
                }
                else
                {
                    // Handle unrecognized function calls by providing an error response
                    var errorMessage = $"Function '{functionCall.Name}' is not available. Available tools are: {string.Join(", ", tools.Select(t => t.Name))}";

                    var errorResult = new FunctionResultContent(functionCall.CallId, errorMessage);

                    newStepItems.Add(new ChatMessage(ChatRole.Tool, [errorResult]));
                    trajectory.Append(errorResult);

                    return new SingleStepResult<TContext>
                    {
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

        // if we reach here, there were no tool calls in the response
        return new SingleStepResult<TContext>
        {
            ModelResponse = modelResponse,
            PreStepItems = preStepItems,
            NewStepItems = newStepItems,
            NextStep = new NextStep<TContext>
            {
                Type = NextStepType.FinalOutput,
                Output = modelResponse.Messages.Last().Contents.OfType<TextContent>().First().Text
            }
        };
    }

}
