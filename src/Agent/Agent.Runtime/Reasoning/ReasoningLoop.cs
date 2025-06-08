// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Agent.Core;
using Agent.Core.Attributes;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins.Definitions;
using Agent.Runtime.Helpers;
using Agent.Runtime.SubAgents.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Agent.Runtime.Reasoning;

public class ReasoningLoop
{
    private readonly ILogger<ReasoningLoop> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IChatClient _chatClient;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private AgentContext _context;
    private readonly IThreadRepository _threadRepository;
    private readonly Channel<ReasoningLoopMessage> _msgCh;
    private readonly SemaphoreSlim _semaphore = new(initialCount: 1, maxCount: 1);
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly ActionSettings _actionSettings;
    private readonly Tracer _tracer;
    private TelemetrySpan? _rootSpan;
    private TelemetrySpan? _currentAgentSpan;
    private TelemetrySpan? _currentToolSpan;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private List<ChatMessage>? _chatHistory;
    private Agent<AgentContext> _currentAgent;

    // Retry configuration
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) };

    public ReasoningLoop(
        ILogger<ReasoningLoop> logger,
        ILoggerFactory loggerFactory,
        IChatClient chatClient,
        IAgentOutboundCommunicationService outboundCommunicationService,
        Agent<AgentContext> startingAgent,
        IThreadRepository threadRepository,
        AgentContext context,
        IToolFactory<AgentContext> toolFactory,
        ActionSettings actionSettings,
        Tracer tracer,
        IAgentFactory<AgentContext> agentFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _chatClient = chatClient;
        _outboundCommunicationService = outboundCommunicationService;
        _msgCh = Channel.CreateUnbounded<ReasoningLoopMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = true
        });
        _threadRepository = threadRepository;
        _context = context;
        _toolFactory = toolFactory;
        _currentAgent = startingAgent;
        _actionSettings = actionSettings;
        _tracer = tracer;
        _agentFactory = agentFactory;
    }

    public async Task AppendNewUserMessageAsync(ChatMessage msg, CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("Appending new user message");
            await _msgCh.Writer.WriteAsync(new ReasoningLoopUserMessage(msg), cancellationToken);

            _ = Task.Run(async () => await RunAsync(cancellationToken), cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Channel is closed.");
        }
    }

    public async IAsyncEnumerable<RunResult<AgentContext>> AppendNewUserMessageStreamAsync(ChatMessage msg, CancellationToken cancellationToken = default)
    {
        // TODO: use queue system to iterate over events and *actually* do something with them before returning all events to user
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("Appending new user message");
            await _msgCh.Writer.WriteAsync(new ReasoningLoopUserMessage(msg), cancellationToken);

            var streamingResult = RunStreamingAsync(cancellationToken);
            await foreach (var update in streamingResult.WithCancellation(cancellationToken))
            {
                yield return update;
            }
        }
        else
        {
            throw new InvalidOperationException("Channel is closed.");
        }
    }

    public async Task AppendNewApprovalMessageAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("Appending new approval message");
            await _msgCh.Writer.WriteAsync(new ReasoningLoopApprovalMessage(approval), cancellationToken);

            _ = Task.Run(async () => await RunAsync(cancellationToken), cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Channel is closed.");
        }
    }

    public async Task LoadChatHistoryAsync()
    {
        if (_chatHistory != null)
        {
            return;
        }

        var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
        if (agentChatHistory == null)
        {
            // should never happen
            _chatHistory = [];
            return;
        }

        var reasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(_threadRepository);
        _chatHistory = reasoningMessages.GetChatMessages();
    }


    public async Task<IEnumerable<ChatMessage>> ExportChatHistory(CancellationToken cancellationToken)
    {
        //TODO - synchronization with writers. Currently only used during development so not a blocker.
        return _chatHistory?.ToArray() ?? Array.Empty<ChatMessage>();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        // Ensure that only one thread runs at a time
        if (!await _semaphore.WaitAsync(0, cancellationToken))
        {
            return;
        }

        while (_msgCh.Reader.TryRead(out var reasoningLoopMessage))
        {

            _rootSpan = _tracer.StartRootSpan(TraceOperationName.UserMessage);
            _rootSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
            try
            {
                _logger.LogInternalInformation("Received new message. Running reasoning loop...");

                AgentChatHistory agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);

                switch (reasoningLoopMessage)
                {
                    case ReasoningLoopUserMessage userMessage:
                        {
                            _logger.LogInternalInformation("Processing user message.");
                            _rootSpan.SetAttribute(TraceAttribute.MessageContent, userMessage.Message.Text);
                            if (_context.ApprovalInformation != null &&
                                _context.ApprovalInformation.PendingApprovals.Count > 0)
                            {
                                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                                    _context.ThreadId,
                                    string.Empty,
                                    new ChatMessage(ChatRole.Assistant, "You have pending approvals. Please resolve them before continuing."));
                                break;
                            }
                            var shouldStop = await HandleUnprocessedToolCallsAsync(agentChatHistory, cancellationToken);
                            if (shouldStop)
                            {
                                return;
                            }

                            await PersistReasoningMessageAsync(agentChatHistory, userMessage.Message);
                            break;
                        }
                    case ReasoningLoopApprovalMessage approvalMessage:
                        {
                            _logger.LogInternalInformation("Processing approval message.");
                            _rootSpan.SetAttribute(TraceAttribute.MessageContent, approvalMessage.Approval.Title);
                            var approval = approvalMessage.Approval;
                            var shouldStop = await ProcessNewApprovalAsync(agentChatHistory, approval, cancellationToken);
                            if (shouldStop)
                            {
                                return;
                            }
                            break;
                        }
                    default:
                        _logger.LogInternalWarning("Received unknown message type: {Type}", reasoningLoopMessage.GetType());
                        continue;
                }

                await RunInternalAsync(agentChatHistory, cancellationToken, _tracer);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "An error occurred during reasoning loop.");
            }
            finally
            {
                _rootSpan.End();
                _rootSpan = null;
            }
        }

        _semaphore.Release();
    }

    private async IAsyncEnumerable<RunResult<AgentContext>> RunStreamingAsync(CancellationToken cancellationToken)
    {
        if (!await _semaphore.WaitAsync(0, cancellationToken))
        {
            _logger.LogInternalInformation("Semaphore is already acquired by another thread. Skipping this run.");
            yield break;
        }

        while (_msgCh.Reader.TryRead(out var reasoningLoopMessage))
        {

            _logger.LogInternalInformation("Received new message. Running reasoning loop...");

            AgentChatHistory agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);

            switch (reasoningLoopMessage)
            {
                case ReasoningLoopUserMessage userMessage:
                    {
                        _logger.LogInternalInformation("Processing user message.");
                        if (_context.ApprovalInformation != null &&
                            _context.ApprovalInformation.PendingApprovals.Count > 0)
                        {
                            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                                _context.ThreadId,
                                string.Empty,
                                new ChatMessage(ChatRole.Assistant, "You have pending approvals. Please resolve them before continuing."));
                            break;
                        }
                        var shouldStop = await HandleUnprocessedToolCallsAsync(agentChatHistory, cancellationToken);
                        if (shouldStop)
                        {
                            yield break;
                        }

                        await PersistReasoningMessageAsync(agentChatHistory, userMessage.Message);
                        break;
                    }
                case ReasoningLoopApprovalMessage approvalMessage:
                    {
                        _logger.LogInternalInformation("Processing approval message.");
                        var approval = approvalMessage.Approval;
                        var shouldStop = await ProcessNewApprovalAsync(agentChatHistory, approval, cancellationToken);
                        if (shouldStop)
                        {
                            yield break;
                        }
                        break;
                    }
                default:
                    _logger.LogInternalWarning("Received unknown message type: {Type}", reasoningLoopMessage.GetType());
                    continue;
            }

            var results = RunInternalStreamingAsync(agentChatHistory, cancellationToken);

            await foreach (var result in results)
            {
                yield return result;
            }
        }

        _semaphore.Release();
        yield break;
    }

    private async Task RunInternalAsync(AgentChatHistory agentChatHistory, CancellationToken cancellationToken, Tracer tracer)
    {

        try
        {
            var runConfig = new RunConfig
            {
                ChatClient = _chatClient,
                LoggerFactory = _loggerFactory,
            };

            var runHooks = new RunHooks<AgentContext>
            {
                ResolveFactoryTools = (context, agent) =>
                {
                    List<AIFunction> tools = [];

                    foreach (var toolName in agent.FactoryTools)
                    {
                        var tool = _toolFactory.GetTool(toolName, _context.ThreadId);

                        tools.Add(tool);
                    }

                    return Task.FromResult(tools);
                },
                OnAgentStart = async (context, agent) =>
                {
                    _logger.LogInternalInformation("Trace invoke agent: {AgentName}", agent.Name);
                    _currentAgentSpan = tracer.StartActiveSpan($"invoke.agent.{agent.Name}", SpanKind.Internal, _rootSpan);
                    _currentAgentSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
                    _currentAgentSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                    _currentAgentSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.InvokeAgent);
                },
                OnAgentEnd = async (context, agent, output) =>
                {
                    _logger.LogInternalInformation("Trace Ending agent: {AgentName}", agent.Name);
                    _currentAgentSpan?.End();
                    _currentAgentSpan = null;
                },
                OnHandoff = async (context, agent, handoffAgent) =>
                {
                    _logger.LogInternalInformation("Trace Handoff from agent: {AgentName} to agent: {HandoffAgentName}", agent.Name, handoffAgent.Name);
                    _currentToolSpan = tracer.StartSpan($"handoff", SpanKind.Internal, _currentAgentSpan);
                    _currentToolSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
                    _currentToolSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Handoff);
                    _currentToolSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                    _currentToolSpan.SetAttribute(TraceAttribute.HandeOffAgentName, handoffAgent.Name);
                    _currentToolSpan.End();
                    _currentToolSpan = null;
                    _currentAgentSpan?.End();
                    _context.AgentHandoffChain.Add(handoffAgent.Name);
                    _currentAgent = handoffAgent;
                    await _threadRepository.UpdateAgentContextAsync(_context);
                },
                OnToolStart = async (context, agent, tool) =>
                {
                    _logger.LogInternalInformation("Trace Starting tool: {ToolName} for agent: {AgentName}", tool.Name, agent.Name);
                    _currentToolSpan = tracer.StartActiveSpan($"tool.{tool.Name}", SpanKind.Internal, _currentAgentSpan);
                    _currentToolSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
                    _currentToolSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Tool);
                    _currentToolSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                    _currentToolSpan.SetAttribute(TraceAttribute.ToolName, tool.Name);
                    _currentToolSpan.SetAttribute(TraceAttribute.ToolDescription, tool.Description);
                },
                OnToolEnd = async (context, agent, tool, output) =>
                {
                    _logger.LogInternalInformation("Trace Ending tool: {ToolName} for agent: {AgentName}", tool.Name, agent.Name);
                    _currentToolSpan?.SetAttribute(TraceAttribute.ToolOutput, output?.ToString() ?? string.Empty);
                    _currentToolSpan?.End();
                    _currentToolSpan = null;
                }
            };

            ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;

            var runResult = await Runner.RunAsync(
                startingAgent: _currentAgent,
                input: _chatHistory!,
                config: runConfig,
                context: _context,
                hooks: runHooks,
                cancellationToken: cancellationToken
            );

            await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);

            // handle manual tool calls
            while (runResult.ManualToolCalls != null && runResult.ManualToolCalls.Count > 0)
            {
                List<ManualToolCallResult> toolResults = [];

                var toolCall = runResult.ManualToolCalls.Single(); // Should only be one tool call at a time

                // TODO: move handoff back to Agent.Framework so we don't have to manipulate the chat history so much outside the runner
                if (toolCall.Tool.UnderlyingMethod?.Name == nameof(AgentControlFlowPluginDefinition.HandoffBack))
                {
                    if (_context.AgentHandoffChain.Count > 1)
                    {
                        // pop agent off the chain
                        _context.AgentHandoffChain.RemoveAt(_context.AgentHandoffChain.Count - 1);
                        var agentName = _context.AgentHandoffChain[^1];
                        _currentAgent = _agentFactory.GetAgent(agentName);

                        runResult = runResult.WithNewAgent(_currentAgent);

                        toolResults.Add(new ManualToolCallResult()
                        {
                            FunctionCall = toolCall.FunctionCall,
                            Output = null,
                            SkipToolCall = true // skip handoff tool calls
                        });
                    }
                    else
                    {
                        toolResults.Add(new ManualToolCallResult()
                        {
                            FunctionCall = toolCall.FunctionCall,
                            Output = "There are no agents to handoff back to, a different handoff must be used instead."
                        });
                    }
                }
                else
                {
                    var checkApprovalResult = await CheckApprovalAsync(toolCall);
                    var checkAzCliWrite = CheckAzCliWriteToolCallAsync(toolCall);
                    var checkKubectlWrite = CheckKubectlWriteToolCallAsync(toolCall);

                    if (checkAzCliWrite)
                    {
                        var functionResult = await InvokeToolWithErrorHandlingAsync(toolCall, cancellationToken);

                        var cliExecution = await _threadRepository.ListPendingAzCliExecutionAsync(_context.ThreadId);
                        if (cliExecution == null)
                        {
                            // if cliExecution is null, it means no pending execution, which means something (e.g. validation failed)
                            // we need to return the error message to LLM.
                            toolResults.Add(new ManualToolCallResult()
                            {
                                FunctionCall = toolCall.FunctionCall,
                                Output = functionResult
                            });
                        }
                        else
                        {
                            cliExecution = cliExecution with
                            {
                                AgentContextId = _context.Id,
                            };
                            await _threadRepository.UpdateAzCliExecutionAsync(_context.ThreadId, cliExecution);
                            break;
                        }
                    }
                    else if (checkKubectlWrite)
                    {
                        var functionResult = await InvokeToolWithErrorHandlingAsync(toolCall, cancellationToken);

                        var kubectlExecution = await _threadRepository.ListPendingKubectlExecutionAsync(_context.ThreadId);
                        if (kubectlExecution == null)
                        {
                            // if cliExecution is null, it means no pending execution, which means something (e.g. validation failed)
                            // we need to return the error message to LLM.
                            toolResults.Add(new ManualToolCallResult()
                            {
                                FunctionCall = toolCall.FunctionCall,
                                Output = functionResult
                            });
                        }
                        else
                        {
                            kubectlExecution = kubectlExecution with
                            {
                                AgentContextId = _context.Id,
                            };
                            await _threadRepository.UpdateKubectlExecutionAsync(_context.ThreadId, kubectlExecution);
                            break;
                        }
                    }
                    else if (checkApprovalResult.ApprovalStatus == ToolApprovalStatus.NotRequired || checkApprovalResult.ApprovalStatus == ToolApprovalStatus.AutoApproved)
                    {
                        var functionResult = await InvokeToolWithErrorHandlingAsync(toolCall, cancellationToken);
                        toolResults.Add(new ManualToolCallResult()
                        {
                            FunctionCall = toolCall.FunctionCall,
                            Output = functionResult
                        });
                    }
                    else
                    {
                        // if approval is required, stop the loop and wait for approval
                        await PersistReasoningMessageAsync(agentChatHistory, toolCall.OriginalMessage);

                        break;
                    }
                }

                runResult = await Runner.ResumeFromManualToolsAsync(
                    previousResult: runResult,
                    manualToolResults: toolResults,
                    config: runConfig,
                    context: _context,
                    hooks: runHooks,
                    cancellationToken: cancellationToken
                );

                await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);
            }

            if (runResult.Output != null)
            {
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context.ThreadId, string.Empty,
                    new ChatMessage(ChatRole.Assistant, runResult.Output?.ToString()));
            }

            _logger.LogInternalInformation("Reasoning loop completed successfully.");
            // span.SetStatus(OpenTelemetry.Trace.Status.Ok);
        }
        catch (Exception ex)
        {
            // span.SetStatus(OpenTelemetry.Trace.Status.Error.WithDescription(ex.Message));
            // span.RecordException(ex);
            _logger.LogInternalError(ex, "An error occurred during reasoning loop.");
        }
        finally
        {
            // span.End();
        }
    }

    private async IAsyncEnumerable<RunResult<AgentContext>> RunInternalStreamingAsync(AgentChatHistory agentChatHistory, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var runConfig = new RunConfig
        {
            ChatClient = _chatClient,
            LoggerFactory = _loggerFactory
        };

        var runHooks = new RunHooks<AgentContext>
        {
            ResolveFactoryTools = (context, agent) =>
            {
                List<AIFunction> tools = [];

                foreach (var toolName in agent.FactoryTools)
                {
                    var tool = _toolFactory.GetTool(toolName, _context.ThreadId);

                    tools.Add(tool);
                }

                return Task.FromResult(tools);
            }
        };

        ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;

        var runResult = await Runner.RunAsync(
            startingAgent: _currentAgent,
            input: _chatHistory!,
            config: runConfig,
            context: _context,
            hooks: runHooks,
            cancellationToken: cancellationToken
        );
        yield return runResult;

        await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);
        _currentAgent = runResult.LastAgent;
        _context = _context with { CurrentAgent = _currentAgent.Name };
        _context = await _threadRepository.UpdateAgentContextAsync(_context);

        // handle manual tool calls
        while (runResult.ManualToolCalls != null && runResult.ManualToolCalls.Count > 0)
        {
            List<ManualToolCallResult> toolResults = [];

            var toolCall = runResult.ManualToolCalls.Single(); // Should only be one tool call at a time
            var checkApprovalResult = await CheckApprovalAsync(toolCall);
            var checkAzCliWrite = CheckAzCliWriteToolCallAsync(toolCall);
            var checkKubectlWrite = CheckKubectlWriteToolCallAsync(toolCall);

            if (checkAzCliWrite)
            {
                await InvokeToolWithErrorHandlingAsync(toolCall, cancellationToken);

                var cliExecution = await _threadRepository.ListPendingAzCliExecutionAsync(_context.ThreadId);
                cliExecution = cliExecution with
                {
                    AgentContextId = _context.Id,
                };
                await _threadRepository.UpdateAzCliExecutionAsync(_context.ThreadId, cliExecution);
                break;
            }

            if (checkKubectlWrite)
            {
                await InvokeToolWithErrorHandlingAsync(toolCall, cancellationToken);

                var kubectlExecution = await _threadRepository.ListPendingKubectlExecutionAsync(_context.ThreadId);
                kubectlExecution = kubectlExecution with
                {
                    AgentContextId = _context.Id,
                };
                await _threadRepository.UpdateKubectlExecutionAsync(_context.ThreadId, kubectlExecution);
                break;
            }

            if (checkApprovalResult.ApprovalStatus == ToolApprovalStatus.NotRequired || checkApprovalResult.ApprovalStatus == ToolApprovalStatus.AutoApproved)
            {
                var functionResult = await InvokeToolWithErrorHandlingAsync(toolCall, cancellationToken);
                toolResults.Add(new ManualToolCallResult()
                {
                    FunctionCall = toolCall.FunctionCall,
                    Output = functionResult
                });
            }
            else
            {
                // if approval is required, stop the loop and wait for approval
                await PersistReasoningMessageAsync(agentChatHistory, toolCall.OriginalMessage);

                break;
            }

            runResult = await Runner.ResumeFromManualToolsAsync(
                previousResult: runResult,
                manualToolResults: toolResults,
                config: runConfig,
                context: _context,
                hooks: runHooks,
                cancellationToken: cancellationToken
            );
            yield return runResult;

            await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);
            _currentAgent = runResult.LastAgent;
            _context = _context with { CurrentAgent = _currentAgent.Name };
            _context = await _threadRepository.UpdateAgentContextAsync(_context);
        }

        if (runResult.Output != null)
        {
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context.ThreadId, string.Empty,
                new ChatMessage(ChatRole.Assistant, runResult.Output?.ToString()));
        }

        _logger.LogInternalInformation("Reasoning loop completed successfully.");
        yield break;
    }


    private string GetApprovalTitle(FunctionCallContent functionCall)
    {
        return ApprovalHelper.GenerateUniqueApprovalTitle(
            _context.ThreadId.ToString(),
            _context.Id.ToString(),
            functionCall.Name,
            functionCall.Arguments ?? new Dictionary<string, object?>());
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

    private async Task ExecuteToolAsync(
        AgentChatHistory agentChatHistory,
        AIFunction aiTool,
        FunctionCallContent functionCall,
        CancellationToken cancellationToken)
    {
        try
        {
            var functionResult = await aiTool.InvokeAsync(functionCall.Arguments, cancellationToken);
            var result = new FunctionResultContent(functionCall.CallId, functionResult);
            var functionCallMessage = new ChatMessage(ChatRole.Tool, [result]);
            await PersistReasoningMessageAsync(agentChatHistory, functionCallMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error while invoking tool: {ToolName}", functionCall.Name);
            var errorMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(functionCall.CallId, GetErrorMessage(functionCall, ex))]);
            await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
        }
    }

    private async Task<bool> HandleUnprocessedToolCallsAsync(AgentChatHistory agentChatHistory, CancellationToken cancellationToken)
    {
        var lastMessage = _chatHistory?.LastOrDefault()?.Contents?.First();
        // if lastMessage is a tool call, we need to invoke the tool first
        if (lastMessage != null && lastMessage is FunctionCallContent functionCall)
        {
            try
            {
                var aiTool = ResolveTool(functionCall.Name) ?? throw new Exception($"Tool {functionCall.Name} not found");

                if (aiTool.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>() != null)
                {
                    _logger.LogInternalInformation("Tool {ToolName} requires approval. Waiting for approval.", functionCall.Name);
                    return true;
                }

                await ExecuteToolAsync(agentChatHistory, aiTool, functionCall, cancellationToken);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error while invoking tool: {ToolName}", functionCall.Name);
                var errorMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(functionCall.CallId, GetErrorMessage(functionCall, ex))]);
                await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
            }
        }

        return false;
    }

    private AIFunction? ResolveTool(string name)
    {
        AIFunction? tool = null;

        if (_currentAgent.StandardToolNames.Contains(name))
        {
            tool = _currentAgent.Tools.FirstOrDefault(aiTool => aiTool.Name == name);
        }
        else if (_currentAgent.FactoryTools.Contains(name))
        {
            tool = _toolFactory.GetTool(name, _context.ThreadId);
        }

        return tool;
    }

    private async Task<bool> ProcessNewApprovalAsync(
        AgentChatHistory agentChatHistory,
        Approval approval,
        CancellationToken cancellationToken)
    {
        var lastMessage = _chatHistory?.LastOrDefault()?.Contents?.First();
        // if lastMessage is a tool call, we need to invoke the tool first
        if (lastMessage != null && lastMessage is FunctionCallContent functionCall)
        {
            var approvalTitle = GetApprovalTitle(functionCall);

            // If the approval title is different, it means the approval is for a different tool call than the last one
            // this is unexpected, block the loop for now
            if (approvalTitle != approval.Title)
            {
                return true;
            }

            if (approval.Status == ApprovalDecision.Approved)
            {
                try
                {
                    var aiTool = ResolveTool(functionCall.Name) ?? throw new Exception($"Tool {functionCall.Name} not found");

                    var approvalAttr = aiTool.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>();

                    if (approvalAttr != null)
                    {
                        var approvalContext = new ApprovalContext(
                            ThreadId: _context.ThreadId,
                            ApprovalId: approval.Id,
                            UseOboToken: approvalAttr.UseOboToken && _actionSettings.Mode == ActionMode.Review
                        );

                        ToolStatic.AsyncLocalApprovalContext.Value = approvalContext;
                    }

                    await ExecuteToolAsync(agentChatHistory, aiTool, functionCall, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error while invoking tool: {ToolName}", functionCall.Name);
                    var errorMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(functionCall.CallId, GetErrorMessage(functionCall, ex))]);
                    await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
                }
                finally
                {
                    // remove pending approval
                    var pendingApprovals = _context.ApprovalInformation?.PendingApprovals;
                    if (pendingApprovals != null && pendingApprovals.Contains(approval.Id))
                    {
                        pendingApprovals.Remove(approval.Id);
                        _context = _context with
                        {
                            ApprovalInformation = new ApprovalInformation(pendingApprovals)
                        };
                        _context = await _threadRepository.UpdateAgentContextAsync(_context);
                    }
                }
            }
            else if (approval.Status == ApprovalDecision.Rejected)
            {
                var result = new FunctionResultContent(functionCall.CallId, "Error: Function failed, user rejected the function call.");
                var functionCallMessage = new ChatMessage(ChatRole.Tool, [result]);
                await PersistReasoningMessageAsync(agentChatHistory, functionCallMessage);

                // remove pending approval
                var pendingApprovals = _context.ApprovalInformation?.PendingApprovals;
                if (pendingApprovals != null && pendingApprovals.Contains(approval.Id))
                {
                    pendingApprovals.Remove(approval.Id);
                    _context = _context with
                    {
                        ApprovalInformation = new ApprovalInformation(pendingApprovals)
                    };
                    _context = await _threadRepository.UpdateAgentContextAsync(_context);
                }
            }
            else  // Pending
            {
                // If there is any pending approvals, we should wait for them to be resolved before continuing
                _logger.LogInternalInformation("There are pending approvals. Waiting for them to be resolved before continuing.");
                return true;
            }
        }

        return false;
    }

    private async Task<CheckApprovalActivityOutput> CheckApprovalAsync(ManualToolCall toolCall)
    {
        try
        {
            if (toolCall.Tool == null)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            // Check if requiers approval
            var attribute = toolCall.Tool.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>();
            if (attribute == null)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            // if in agent mode, return auto approved
            if (_actionSettings.Mode == ActionMode.Autonomous)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.AutoApproved,
                };
            }

            var approvalTitle = GetApprovalTitle(toolCall.FunctionCall);

            var approval = await _threadRepository.GetApprovalAsync(_context.ThreadId, approvalTitle);

            if (approval == null ||
                (approval.Status == ApprovalDecision.Approved && string.IsNullOrEmpty(approval.OboToken) && attribute != null && attribute.UseOboToken))
            {
                var description = attribute.DisplayMessage ?? toolCall.Tool.Name;

                // Create a new approval document
                var newApproval = new Approval(
                    Id: Guid.NewGuid(),
                    ThreadId: _context.ThreadId.ToString(),
                    Title: approvalTitle,
                    Description: description,
                    Status: ApprovalDecision.Pending,
                    CreatedTimestamp: DateTime.UtcNow,
                    DecisionTimestamp: null,
                    OrchestrationId: null,
                    AgentContextId: _context.Id,
                    DecisionUser: null,
                    OboToken: null);

                await _threadRepository.CreateApprovalAsync(newApproval);

                var newPendingApprovals = _context.ApprovalInformation?.PendingApprovals ?? [];
                newPendingApprovals.Add(newApproval.Id);

                _context = _context with
                {
                    ApprovalInformation = new ApprovalInformation(newPendingApprovals)
                };

                _context = await _threadRepository.UpdateAgentContextAsync(_context);

                await _outboundCommunicationService.AppendAgentApprovalMessage(
                    _context.ThreadId,
                    newApproval);

                _logger.LogInternalInformation("Created new approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status ToolApprovalStatus.Pending", newApproval.Id, _context.ThreadId, newApproval.Title);

                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = newApproval.Id,
                    ApprovalStatus = ToolApprovalStatus.Pending,
                };
            }
            else
            {
                _logger.LogInternalInformation("Found existing approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status {Status}", approval.Id, _context.ThreadId, approval.Title, approval.Status);
                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = approval.Id,
                    ApprovalStatus = ApprovalDocument.ToToolApprovalStatus(approval.Status),
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error while checking approval: {Message}", ex.Message);
            return new CheckApprovalActivityOutput()
            {
                ApprovalStatus = ToolApprovalStatus.Pending,
            };
        }
    }

    private bool CheckAzCliWriteToolCallAsync(ManualToolCall toolCall)
    {
        if (toolCall.Tool == null)
        {
            return false;
        }

        if (toolCall.Tool.UnderlyingMethod?.Name != "RunAzCliWriteCommandsAsync")
        {
            return false;
        }

        return true;
    }

    private bool CheckKubectlWriteToolCallAsync(ManualToolCall toolCall)
    {
        if (toolCall.Tool == null)
        {
            return false;
        }

        if (toolCall.Tool.UnderlyingMethod?.Name != "RunKubectlWriteCommandAsync")
        {
            return false;
        }

        return true;
    }

    private async Task PersistReasoningMessageAsync(AgentChatHistory agentChatHistory, ChatMessage chatMessage)
    {
        _chatHistory!.Add(chatMessage);
        var reasoningMessage = chatMessage.GetReasoningMessage(_context.Id);

        await ExecuteWithRetryAsync(
            () => _threadRepository.CreateReasoningMessageAsync(reasoningMessage),
            $"CreateReasoningMessage for message {reasoningMessage.Id}");

        await ExecuteWithRetryAsync(
            () => _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage),
            $"AddReasoningMessageToChatHistory for message {reasoningMessage.Id}");
    }

    private async Task PersistReasoningMessagesAsync(AgentChatHistory agentChatHistory, IEnumerable<ChatMessage> chatMessage)
    {
        _chatHistory!.AddRange(chatMessage);
        // Calling ToList() is important here because otherwise the reasoning messages get new IDs every time
        // the reasoningMessages IEnumerable is enumerated.
        var reasoningMessages = chatMessage.Select(msg => msg.GetReasoningMessage(_context.Id)).ToList();

        foreach (var reasoningMessage in reasoningMessages)
        {
            await ExecuteWithRetryAsync(
                () => _threadRepository.CreateReasoningMessageAsync(reasoningMessage),
                $"CreateReasoningMessage for message {reasoningMessage.Id}");
        }

        await ExecuteWithRetryAsync(
            () => _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessages),
            $"AddReasoningMessagesToChatHistory for {reasoningMessages.Count} messages");
    }

    private async Task<object?> InvokeToolWithErrorHandlingAsync(ManualToolCall toolCall, CancellationToken cancellationToken)
    {
        try
        {
            ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;
            return await toolCall.Tool.InvokeAsync(toolCall.FunctionCall.Arguments, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error while calling tool {ToolName}", toolCall.Tool!.Name);
            return GetErrorMessage(toolCall.FunctionCall, ex);
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogInternalInformation("Resource already exists for {OperationName}, continuing without retry", operationName);
                return default(T)!;
            }
            catch (Exception ex) when (attempt < MaxRetryAttempts - 1)
            {
                _logger.LogInternalWarning(ex, "Attempt {Attempt} failed for {OperationName}, retrying in {Delay}ms",
                    attempt + 1, operationName, RetryDelays[attempt].TotalMilliseconds);

                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
        }

        // Final attempt without catch (except for Cosmos conflict)
        try
        {
            return await operation();
        }
        catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogInternalInformation("Resource already exists for {OperationName}, continuing without retry", operationName);
            return default(T)!;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "All retry attempts failed for {OperationName}", operationName);
            throw;
        }
    }

}
