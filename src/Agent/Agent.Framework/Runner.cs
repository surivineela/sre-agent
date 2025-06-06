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
        List<ManualToolCallResult> manualToolResults,
        RunConfig config,
        TContext? context = null,
        RunHooks<TContext>? hooks = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        var input = new List<ChatMessage>(previousResult.Input).Concat(previousResult.NewItems).ToList();
        var functionResultMessages = new List<ChatMessage>();

        if (previousResult.ManualToolCalls == null)
        {
            throw new Exception("No manual tool calls found");
        }

        foreach (var manualToolCall in previousResult.ManualToolCalls)
        {
            var matchingOutput = (manualToolResults.FirstOrDefault(o => o.FunctionCall.CallId == manualToolCall.FunctionCall.CallId)?.Output)
                ?? throw new Exception("No matching output found for manual tool call");

            functionResultMessages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(manualToolCall.FunctionCall.CallId, matchingOutput)]));

            previousResult.Trajectory.Append(
                null,
                manualToolResults.FirstOrDefault(o => o.FunctionCall.CallId == manualToolCall.FunctionCall.CallId)
            );

            if (hooks != null)
            {
                await hooks.OnToolEnd(previousResult.ContextWrapper, previousResult.LastAgent, manualToolCall.Tool, matchingOutput);
            }
        }

        return await RunInternalAsync(
            startingAgent: previousResult.LastAgent,
            input: input,
            config: config,
            newGeneratedItems: functionResultMessages,
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
        List<ChatMessage> input,
        RunConfig config,
        TContext? context = null,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        CancellationToken cancellationToken = default
    ) where TContext : class
    {
        return RunInternalAsync(
            startingAgent: startingAgent,
            input: input,
            config: config,
            context: context,
            maxTurns: maxTurns,
            hooks: hooks,
            cancellationToken: cancellationToken
        );
    }

    private static async Task<RunResult<TContext>> RunInternalAsync<TContext>(
        Agent<TContext> startingAgent,
        List<ChatMessage> input,
        RunConfig config,
        List<ChatMessage>? newGeneratedItems = null,
        TContext? context = null,
        int currentTurn = 0,
        int maxTurns = DefaultMaxTurns,
        RunHooks<TContext>? hooks = null,
        Trajectory? trajectory = null,
        CancellationToken cancellationToken = default // TODO: use cancellation token
    ) where TContext : class
    {
        hooks ??= new RunHooks<TContext>();

        bool shouldRunAgentStartHooks = true;
        Agent<TContext> currentAgent = startingAgent;
        List<ChatMessage> originalInput = [.. input];
        List<ChatMessage> generatedMessages = newGeneratedItems != null ? [.. newGeneratedItems] : [];
        List<ChatResponse> rawResponses = [];
        trajectory ??= new Trajectory();

        var contextWrapper = new RunContextWrapper<TContext>(context);

        var logger = config.LoggerFactory.CreateLogger("Agent.Framework.Runner");
        var criticCount = 0;

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
                    originalInput: originalInput,
                    generatedMessages: generatedMessages,
                    config: config,
                    contextWrapper: contextWrapper,
                    hooks: hooks,
                    shouldRunAgentStartHooks: shouldRunAgentStartHooks,
                    trajectory: trajectory
                );

                trajectory = turnResult.Trajectory;

                shouldRunAgentStartHooks = false;

                originalInput = turnResult.OriginalInput;
                generatedMessages = turnResult.GeneratedItems;
                rawResponses.Add(turnResult.ModelResponse);

                if (turnResult.NextStep.Type == NextStepType.FinalOutput)
                {
                    logger.LogInformation("FinalOutput received from {AgentName}, Critic: {criticCount}/{MaxReflectionCount}", currentAgent.Name, criticCount, currentAgent.MaxReflectionCount);
                    if (currentAgent.MaxReflectionCount > 0 && criticCount < currentAgent.MaxReflectionCount)
                    {
                        // todo: improve trajectory building to include whole turn
                        // todo: use summarizer to summarize
                        var agentTools = await hooks.ResolveFactoryTools(contextWrapper, currentAgent);
                        var criticResult = await Critic.CriticAsync(
                            currentAgent,
                            agentTools,
                            originalInput,
                            trajectory.ToString(),
                            config.ChatClient);
                        if (criticResult.Contains("\"overall_assessment\": \"FAIL\""))
                        {
                            criticCount++;

                            // todo: replace current interaction flow with sumary..
                            // needs changes to turnResult.OriginalInput
                            var criticEval = "Past run feedback:\n" + criticResult;
                            generatedMessages.Add(new(ChatRole.User, criticEval));

                            logger.LogWarning("Critic result indicates failure: {CriticResult}", criticResult);
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
                        Input = originalInput,
                        NewItems = generatedMessages,
                        Output = turnResult.NextStep.Output,
                        ContextWrapper = contextWrapper,
                        CurrentTurn = currentTurn,
                        MaxTurns = maxTurns,
                        RawResponses = rawResponses
                    };
                }
                else if (turnResult.NextStep.Type == NextStepType.Handoff && turnResult.NextStep.Agent != null)
                {
                    currentAgent = turnResult.NextStep.Agent;
                    shouldRunAgentStartHooks = true;
                }
                else if (turnResult.NextStep.Type == NextStepType.RunAgain)
                {
                    // do nothing, we will run the agent again
                }
                else if (turnResult.NextStep.Type == NextStepType.ManualTool && turnResult.NextStep.ManualToolCall != null)
                {
                    trajectory.ResetFunctionContent(); // reset last round of function calls to avoid large trajectory size
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
        catch (Exception)
        {
            // todo: log
            throw;
        }
    }

    public static async Task<SingleStepResult<TContext>> RunSingleTurnAsync<TContext>(
        Agent<TContext> agent,
        List<ChatMessage> originalInput,
        List<ChatMessage> generatedMessages,
        RunConfig config,
        RunContextWrapper<TContext> contextWrapper,
        RunHooks<TContext> hooks,
        bool shouldRunAgentStartHooks,
        Trajectory? trajectory = null
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
        modelInput.AddRange(generatedMessages);

        var response = await chatClient.GetResponseAsync(modelInput, chatOptions);

        if (response.Usage != null)
        {
            contextWrapper.UsageDetails.Add(response.Usage);
        }

        return await ExecuteToolsAndHandoffsAsync(
            agent: agent,
            originalInput: originalInput,
            preStepItems: generatedMessages,
            modelResponse: response,
            hooks: hooks,
            contextWrapper: contextWrapper,
            tools: tools,
            trajectory: trajectory
        );
    }

    private static async Task<SingleStepResult<TContext>> ExecuteToolsAndHandoffsAsync<TContext>(
        Agent<TContext> agent,
        List<ChatMessage> originalInput,
        List<ChatMessage> preStepItems,
        ChatResponse modelResponse,
        RunHooks<TContext> hooks,
        RunContextWrapper<TContext> contextWrapper,
        List<AIFunction> tools,
        Trajectory? trajectory = null
    ) where TContext : class
    {
        List<ChatMessage> newStepItems = [];
        newStepItems.AddRange(modelResponse.Messages);
        trajectory ??= new Trajectory();

        // process tool calls
        // assume no parallel tool calling, so if a regular tool is called, we are not handing off to another agent
        foreach (var message in modelResponse.Messages)
        {
            trajectory.Append(modelResponse, null, $"Role: {message.Role.Value}");

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

                        trajectory.Append(null, new ManualToolCallResult
                        {
                            FunctionCall = functionCall,
                            Output = toolResult
                        });

                        await hooks.OnToolEnd(contextWrapper, agent, tool, toolResult);

                        var result = new FunctionResultContent(functionCall.CallId, toolResult);
                        newStepItems.Add(new ChatMessage(ChatRole.Tool, [result]));

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
                            Trajectory = trajectory
                        };
                    }
                    else
                    {
                        // return manual tool call result

                        await hooks.OnToolStart(contextWrapper, agent, tool);

                        return new SingleStepResult<TContext>
                        {
                            OriginalInput = originalInput,
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

                    trajectory.Append(message: $"Unrecognized Function Call: {functionCall.Name} - {errorMessage}");

                    var errorResult = new FunctionResultContent(functionCall.CallId, errorMessage);
                    newStepItems.Add(new ChatMessage(ChatRole.Tool, [errorResult]));
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
                        Trajectory = trajectory
                    };
                }
            }
        }

        // if we reach here, there were no tool calls in the response
        return new SingleStepResult<TContext>
        {
            OriginalInput = originalInput,
            ModelResponse = modelResponse,
            PreStepItems = preStepItems,
            NewStepItems = newStepItems,
            Trajectory = trajectory,
            NextStep = new NextStep<TContext>
            {
                Type = NextStepType.FinalOutput,
                Output = modelResponse.Messages.Last().Contents.OfType<TextContent>().First().Text
            }
        };
    }

}
