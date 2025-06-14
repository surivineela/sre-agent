// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private TelemetrySpan? _currentGenerationSpan;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private List<ChatMessage>? _chatHistory;
    private Agent<AgentContext> _currentAgent;

    // Retry configuration
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) };

    private static readonly JsonSerializerOptions _toolArgumentsJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions _chatMessageJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

    public async Task AppendNewChatMessageAsync(ChatMessage msg, CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("Appending new chat message");
            await _msgCh.Writer.WriteAsync(new ReasoningLoopChatMessage(msg), cancellationToken);

            _ = Task.Run(async () => await RunAsync(cancellationToken), cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Channel is closed.");
        }
    }

    public async Task AppendFunctionCallMessagesAsync(List<ChatMessage> msgs, CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("Appending new function call message");
            await _msgCh.Writer.WriteAsync(new ReasoningLoopFunctionCall(msgs), cancellationToken);

            _ = Task.Run(async () => await RunAsync(cancellationToken), cancellationToken);
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

    // streaming methods
    public async IAsyncEnumerable<RunResult<AgentContext>> AppendNewChatMessageStreamAsync(ChatMessage msg, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // TODO: use queue system to iterate over events and *actually* do something with them before returning all events to user
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("Appending new chat message");
            await _msgCh.Writer.WriteAsync(new ReasoningLoopChatMessage(msg), cancellationToken);

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

    public async IAsyncEnumerable<RunResult<AgentContext>> AppendFunctionCallMessagesStreamAsync(List<ChatMessage> msgs, CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("Appending new function call message");
            await _msgCh.Writer.WriteAsync(new ReasoningLoopFunctionCall(msgs), cancellationToken);

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

    public async IAsyncEnumerable<RunResult<AgentContext>> AppendNewApprovalMessageStreamAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("Appending new approval message");
            await _msgCh.Writer.WriteAsync(new ReasoningLoopApprovalMessage(approval), cancellationToken);

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

    public Task<IEnumerable<ChatMessage>> ExportChatHistoryAsync(CancellationToken cancellationToken)
    {
        //TODO - synchronization with writers. Currently only used during development so not a blocker.
        IEnumerable<ChatMessage> history = _chatHistory?.ToArray() ?? [];
        return Task.FromResult(history);
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

            ReasoningLoopIterationResult? iterationResult = null;

            if (_rootSpan == null)
            {
                // don't reset the root span if one exists (loop continuation)
                _rootSpan = _tracer.StartRootSpan(TraceOperationName.UserMessage);
                _rootSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
                _rootSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.UserMessage);
            }

            try
            {
                _logger.LogInternalInformation("Received new message. Running reasoning loop...");

                var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);

                switch (reasoningLoopMessage)
                {
                    case ReasoningLoopChatMessage chatMessage:
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("Try your best to answer the user's questions. Keep in mind:");
                            sb.AppendLine(" - If you find a suitable agent to handoff to, call transfer_to_{agentName} tool directly");
                            sb.AppendLine(" - If there's no suitable agent to handoff to, call HandoffBack directly");
                            sb.AppendLine(" - **NEVER** tell the user you're going to handoff");
                            sb.AppendLine(" - **NEVER** tell the user what you are handing off for or why you are handing off");
                            sb.AppendLine(" - **NEVER** mention anything related to handoff in your outputMessage");
                            sb.AppendLine(" - Use transfer_to_{agentName} or HandoffBack if you are done solving an issue");
                            sb.AppendLine("User question goes below:");
                            sb.AppendLine(chatMessage.Message.Text);
                            var msg = new ChatMessage(chatMessage.Message.Role, sb.ToString());

                            _logger.LogInternalInformation("Processing chat message.");
                            _rootSpan.SetAttribute(TraceAttribute.MessageContent, msg.Text);
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

                            await PersistReasoningMessageAsync(agentChatHistory, msg);
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
                    case ReasoningLoopFunctionCall functionCall:
                        {
                            _logger.LogInternalInformation("Processing function call messages.");
                            await PersistReasoningMessagesAsync(agentChatHistory, functionCall.Messages);
                            break;
                        }
                    case ReasoningLoopContinuation:
                        {
                            _logger.LogInternalInformation("Received continuation message. Running reasoning loop...");
                            break;
                        }
                    default:
                        _logger.LogInternalWarning("Received unknown message type: {Type}", reasoningLoopMessage.GetType());
                        continue;
                }

                iterationResult = await RunInternalAsync(agentChatHistory, cancellationToken);

                if (iterationResult.IsContinuation)
                {
                    if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
                    {
                        await _msgCh.Writer.WriteAsync(new ReasoningLoopContinuation(), cancellationToken);
                    }
                    else
                    {
                        // can't write to the channel, set the continuation flag to false and end the loop
                        iterationResult.IsContinuation = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "An error occurred during reasoning loop.");
            }
            finally
            {
                if (iterationResult?.IsContinuation == false)
                {
                    // only end the root span if we didn't continue the loop
                    _rootSpan?.End();
                    _rootSpan = null;
                }
            }
        }

        _semaphore.Release();
    }

    private async IAsyncEnumerable<RunResult<AgentContext>> RunStreamingAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Ensure that only one thread runs at a time
        if (!await _semaphore.WaitAsync(0, cancellationToken))
        {
            _logger.LogInternalInformation("Semaphore is already acquired by another thread. Skipping this run.");
            yield break;
        }

        while (_msgCh.Reader.TryRead(out var reasoningLoopMessage))
        {

            ReasoningLoopIterationResult? iterationResult = null;

            if (_rootSpan == null)
            {
                // don't reset the root span if one exists (loop continuation)
                _rootSpan = _tracer.StartRootSpan(TraceOperationName.UserMessage);
                _rootSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
                _rootSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.UserMessage);
            }

            AgentChatHistory? agentChatHistory = null;
            RunResult<AgentContext>? pendingApprovalsResult = null;

            try
            {
                _logger.LogInternalInformation("Received new message. Running reasoning loop...");

                agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);

                switch (reasoningLoopMessage)
                {
                    case ReasoningLoopChatMessage chatMessage:
                        {
                            _logger.LogInternalInformation("Processing chat message.");
                            _rootSpan.SetAttribute(TraceAttribute.MessageContent, chatMessage.Message.Text);
                            if (_context.ApprovalInformation != null &&
                                _context.ApprovalInformation.PendingApprovals.Count > 0)
                            {
                                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                                    _context.ThreadId,
                                    string.Empty,
                                    new ChatMessage(ChatRole.Assistant, "You have pending approvals. Please resolve them before continuing."));

                                pendingApprovalsResult = new RunResult<AgentContext>(_currentAgent)
                                {
                                    Input = _chatHistory ?? [],
                                    NewItems = [],
                                    RawResponses = [],
                                    CurrentTurn = 1,
                                    MaxTurns = 1,
                                    Output = "You have pending approvals. Please resolve them before continuing.",
                                    ContextWrapper = new RunContextWrapper<AgentContext>(_context),
                                    Trajectory = new Trajectory()
                                };
                                break;
                            }
                            var shouldStop = await HandleUnprocessedToolCallsAsync(agentChatHistory, cancellationToken);
                            if (shouldStop)
                            {
                                yield break;
                            }

                            await PersistReasoningMessageAsync(agentChatHistory, chatMessage.Message);
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
                                yield break;
                            }
                            break;
                        }
                    case ReasoningLoopFunctionCall functionCall:
                        {
                            _logger.LogInternalInformation("Processing function call messages.");
                            await PersistReasoningMessagesAsync(agentChatHistory, functionCall.Messages);
                            break;
                        }
                    case ReasoningLoopContinuation:
                        {
                            _logger.LogInternalInformation("Received continuation message. Running reasoning loop...");
                            break;
                        }
                    default:
                        _logger.LogInternalWarning("Received unknown message type: {Type}", reasoningLoopMessage.GetType());
                        continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "An error occurred during reasoning loop.");
                continue;
            }

            if (pendingApprovalsResult != null)
            {
                yield return pendingApprovalsResult;
                yield break;
            }

            await foreach (var result in RunInternalStreamingAsync(agentChatHistory, cancellationToken, r => iterationResult = r))
            {
                yield return result;

                if (iterationResult != null && iterationResult.IsContinuation)
                {
                    if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
                    {
                        await _msgCh.Writer.WriteAsync(new ReasoningLoopContinuation(), cancellationToken);
                    }
                    else
                    {
                        // can't write to the channel, set the continuation flag to false and end the loop
                        iterationResult = new ReasoningLoopIterationResult { IsContinuation = false };
                    }
                }
            }

            if (iterationResult?.IsContinuation == false)
            {
                // only end the root span if we didn't continue the loop
                _rootSpan?.End();
                _rootSpan = null;
            }
        }

        _semaphore.Release();
    }

    private async Task<ReasoningLoopIterationResult> RunInternalAsync(
        AgentChatHistory agentChatHistory,
        CancellationToken cancellationToken)
    {
        var runConfig = new RunConfig
        {
            ChatClient = _chatClient,
            LoggerFactory = _loggerFactory,
        };

        try
        {
            var runHooks = CreateRunHooks();

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

            _currentAgent = runResult.LastAgent;
            _context = _context with { CurrentAgent = _currentAgent.Name };
            _context = await _threadRepository.UpdateAgentContextAsync(_context);

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
                        var newAgent = _agentFactory.GetAgent(agentName);

                        runResult = runResult.WithNewAgent(newAgent);

                        toolResults.Add(new ManualToolCallResult()
                        {
                            FunctionCall = toolCall.FunctionCall,
                            Output = $"Handed off to agent {newAgent.Name}. Assume this persona immediately and continue with the task.",
                        });
                    }
                    else
                    {
                        var output = "There are no agents to handoff back to, a different handoff must be used instead.";
                        toolResults.Add(new ManualToolCallResult()
                        {
                            FunctionCall = toolCall.FunctionCall,
                            Output = output
                        });
                    }
                }
                else
                {
                    var checkWriteActionResult = CheckWriteActionInReadOnlyMode(toolCall);
                    if (_actionSettings.Mode == ActionMode.ReadOnly && checkWriteActionResult.NeedSkip)
                    {
                        var chatMessage = new ChatMessage(ChatRole.User, checkWriteActionResult.Prompt);
                        toolResults.Add(new ManualToolCallResult()
                        {
                            FunctionCall = toolCall.FunctionCall,
                            Output = null,
                            SkipToolCall = true,
                            ReplacementMessage = chatMessage,
                        });

                    }
                    else
                    {
                        var checkApprovalResult = await CheckApprovalAsync(toolCall);
                        var checkAzCliWrite = CheckAzCliWriteToolCall(toolCall);
                        var checkKubectlWrite = CheckKubectlWriteToolCall(toolCall);

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
                                    OriginalFunctionCall = JsonSerializer.Serialize(toolCall.FunctionCall),
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
                                    OriginalFunctionCall = JsonSerializer.Serialize(toolCall.FunctionCall),
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

                _currentAgent = runResult.LastAgent;
                _context = _context with { CurrentAgent = _currentAgent.Name };
                _context = await _threadRepository.UpdateAgentContextAsync(_context);

            }

            if (runResult.Output != null)
            {
                if (runResult.Output is string outputString)
                {
                    await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                        _context,
                        new ChatMessage(ChatRole.Assistant, runResult.Output?.ToString()));
                }
                else if (runResult.Output is AgentOutput agentOutput)
                {
                    // TODO: can we log all this info?
                    _logger.LogInternalInformation("Agent output: {AgentOutputMessage}, {IsUserInputRequired}, {RequestCompleted}, {Reasoning}",
                        agentOutput.OutputMessage, agentOutput.IsUserInputRequired, agentOutput.RequestCompleted, agentOutput.Reasoning);

                    await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                        _context,
                        new ChatMessage(ChatRole.Assistant, agentOutput.OutputMessage));

                    if (agentOutput.CannotHandleNextStep)
                    {
                        _logger.LogInternalInformation("Agent determined the request is out of scope. Handoff back");

                        if (_context.AgentHandoffChain.Count > 1)
                        {
                            // pop agent off the chain
                            _context.AgentHandoffChain.RemoveAt(_context.AgentHandoffChain.Count - 1);
                            var agentName = _context.AgentHandoffChain[^1];
                            var newAgent = _agentFactory.GetAgent(agentName);

                            // persist handoff to trace
                            await runHooks.OnHandoff(new(_context), _currentAgent, newAgent);

                            _currentAgent = newAgent;
                            _context = _context with { CurrentAgent = _currentAgent.Name };
                            _context = await _threadRepository.UpdateAgentContextAsync(_context);

                            _logger.LogInternalInformation("Handoff back to agent: {AgentName}", agentName);

                            var handoffMessage = new ChatMessage(ChatRole.Assistant, $"Handed off to agent {agentName}. Assume this persona immediately and continue with the task.");
                            await PersistReasoningMessageAsync(agentChatHistory, handoffMessage);

                            return new ReasoningLoopIterationResult()
                            {
                                IsContinuation = true
                            };
                        }
                        else
                        {
                            _logger.LogInternalInformation("AgentHandoffChain is empty or has only one agent, ending reasoning loop.");

                            return new ReasoningLoopIterationResult()
                            {
                                IsContinuation = false
                            };
                        }
                    }
                    // agent can handle the request, and it generated some messages but did not complete
                    else if (!agentOutput.RequestCompleted
                        // and reason for being incomplete is not user input requirement
                        && !agentOutput.IsUserInputRequired)
                    {
                        _logger.LogInternalInformation("Asking {AgentName} agent to continue action...", _currentAgent.Name);

                        var userPromptMessage = new ChatMessage(ChatRole.User, "You mentioned you could not complete the request. Continue taking actions to complete the request.");
                        await PersistReasoningMessageAsync(agentChatHistory, userPromptMessage);

                        return new ReasoningLoopIterationResult()
                        {
                            IsContinuation = true
                        };
                    }
                }
            }

            _logger.LogInternalInformation("Reasoning loop iteration completed.");
        }
        catch (TurnLimitReachedException<AgentContext> ex)
        {
            _logger.LogInternalWarning("Turn limit reached.", ex);

            // generate progress summary

            var result = ex.RunResult;

            await PersistReasoningMessagesAsync(agentChatHistory, result.NewItems);

            var progressSummaryAgent = _agentFactory.GetAgent("progress_summary_agent");

            var summaryResult = await Runner.RunAsync(
                startingAgent: progressSummaryAgent,
                input: [.. result.Input, .. result.NewItems],
                config: runConfig,
                context: _context,
                maxTurns: 1,
                cancellationToken: cancellationToken
            );

            var summary = summaryResult.Output?.ToString();

            if (string.IsNullOrEmpty(summary))
            {
                throw new Exception("Progress summary agent returned no output.");
            }

            var assistantMessage = new ChatMessage(ChatRole.Assistant, summary);

            await PersistReasoningMessageAsync(agentChatHistory, assistantMessage);

            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                _context,
                assistantMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "An error occurred during reasoning loop.");
        }
        finally
        {
            _currentAgentSpan?.End();
            _currentAgentSpan = null;
        }

        return new ReasoningLoopIterationResult() { IsContinuation = false };

    }

    // IAsyncEnumerable does not allow tuples with yields, instead use a callback for iteration result
    // yield return cannot be wrapped in try catch
    // broke logic into several try & catchs and returned results outside of these
    private async IAsyncEnumerable<RunResult<AgentContext>> RunInternalStreamingAsync(
        AgentChatHistory agentChatHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        Action<ReasoningLoopIterationResult> iterationResult)
    {
        var runConfig = new RunConfig
        {
            ChatClient = _chatClient,
            LoggerFactory = _loggerFactory
        };

        var runHooks = CreateRunHooks();

        ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;

        // Execute initial runner yield return outside of the runner
        RunResult<AgentContext>? runResult = null;
        RunResult<AgentContext>? turnLimitResult = null;
        bool shouldExit = false;

        try
        {
            runResult = await Runner.RunAsync(
                startingAgent: _currentAgent,
                input: _chatHistory!,
                config: runConfig,
                context: _context,
                hooks: runHooks,
                cancellationToken: cancellationToken
            );
        }
        catch (TurnLimitReachedException<AgentContext> ex)
        {
            turnLimitResult = await HandleTurnLimitExceptionStreamingAsync(ex, agentChatHistory, runConfig, cancellationToken);
            shouldExit = true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "An error occurred during reasoning loop.");
            shouldExit = true;
        }
        finally
        {
            _currentAgentSpan?.End();
            _currentAgentSpan = null;
        }

        // Handle turn limit result
        if (turnLimitResult != null)
        {
            yield return turnLimitResult;
            iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
            yield break;
        }

        // Handle other exceptions
        if (shouldExit)
        {
            iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
            yield break;
        }

        if (runResult == null)
        {
            iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
            yield break;
        }

        yield return runResult;

        bool initialProcessingFailed = false;
        try
        {
            await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);
            _currentAgent = runResult.LastAgent;
            _context = _context with { CurrentAgent = _currentAgent.Name };
            _context = await _threadRepository.UpdateAgentContextAsync(_context);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "An error occurred during initial processing.");
            initialProcessingFailed = true;
        }

        // Handle initial processing failure
        if (initialProcessingFailed)
        {
            iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
            yield break;
        }

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
                    var newAgent = _agentFactory.GetAgent(agentName);

                    runResult = runResult.WithNewAgent(newAgent);

                    toolResults.Add(new ManualToolCallResult()
                    {
                        FunctionCall = toolCall.FunctionCall,
                        Output = $"Handed off to agent {newAgent.Name}. Assume this persona immediately and continue with the task.",
                    });
                }
                else
                {
                    var output = "There are no agents to handoff back to, a different handoff must be used instead.";
                    toolResults.Add(new ManualToolCallResult()
                    {
                        FunctionCall = toolCall.FunctionCall,
                        Output = output
                    });
                }
            }
            else
            {
                var checkApprovalResult = await CheckApprovalAsync(toolCall);
                var checkAzCliWrite = CheckAzCliWriteToolCall(toolCall);
                var checkKubectlWrite = CheckKubectlWriteToolCall(toolCall);
                var checkWriteActionResult = CheckWriteActionInReadOnlyMode(toolCall);

                if (_actionSettings.Mode == ActionMode.ReadOnly && checkWriteActionResult.NeedSkip)
                {
                    var chatMessage = new ChatMessage(ChatRole.Tool, checkWriteActionResult.Prompt);
                    toolResults.Add(new ManualToolCallResult()
                    {
                        FunctionCall = toolCall.FunctionCall,
                        Output = null,
                        SkipToolCall = true,
                        ReplacementMessage = chatMessage,
                    });
                }
                else if (checkAzCliWrite)
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
                            OriginalFunctionCall = JsonSerializer.Serialize(toolCall.FunctionCall),
                        };
                        await _threadRepository.UpdateAzCliExecutionAsync(_context.ThreadId, cliExecution);
                        iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
                        yield break;
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
                            OriginalFunctionCall = JsonSerializer.Serialize(toolCall.FunctionCall),
                        };
                        await _threadRepository.UpdateKubectlExecutionAsync(_context.ThreadId, kubectlExecution);
                        iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
                        yield break;
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
                    iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
                    yield break;
                }
            }

            bool toolCallProcessingFailed = false;
            bool toolResultProcessingFailed = false;

            try
            {
                runResult = await Runner.ResumeFromManualToolsAsync(
                    previousResult: runResult,
                    manualToolResults: toolResults,
                    config: runConfig,
                    context: _context,
                    hooks: runHooks,
                    cancellationToken: cancellationToken
                );
            }
            catch (TurnLimitReachedException<AgentContext> ex)
            {
                turnLimitResult = await HandleTurnLimitExceptionStreamingAsync(ex, agentChatHistory, runConfig, cancellationToken);
                shouldExit = true;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "An error occurred during tool call processing.");
                toolCallProcessingFailed = true;
            }
            // Handle turn limit exception result
            if (turnLimitResult != null)
            {
                yield return turnLimitResult;
                iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
                yield break;
            }

            // Handle tool call processing failure (exit safely outside try-catch)
            if (toolCallProcessingFailed)
            {
                iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
                yield break;
            }

            // Yield the new result
            yield return runResult;

            try
            {
                await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);
                _currentAgent = runResult.LastAgent;
                _context = _context with { CurrentAgent = _currentAgent.Name };
                _context = await _threadRepository.UpdateAgentContextAsync(_context);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "An error occurred during tool call result processing.");
                toolResultProcessingFailed = true;
            }

            // Handle tool result processing failure (exit safely outside try-catch)
            if (toolResultProcessingFailed)
            {
                iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
                yield break;
            }
        }

        // Handle output - call outbound service, stream results were already yielded in line 1050
        if (runResult.Output != null)
        {
            if (runResult.Output is string outputString)
            {
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                    _context,
                    new ChatMessage(ChatRole.Assistant, runResult.Output?.ToString()));
            }
            else if (runResult.Output is AgentOutput agentOutput)
            {
                // TODO: can we log all this info?
                _logger.LogInternalInformation("Agent output: {AgentOutputMessage}, {IsUserInputRequired}, {RequestCompleted}, {Reasoning}",
                    agentOutput.OutputMessage, agentOutput.IsUserInputRequired, agentOutput.RequestCompleted, agentOutput.Reasoning);

                if (agentOutput.CannotHandleNextStep)
                {
                    _logger.LogInternalInformation("Agent determined the request is out of scope. Handoff back");

                    if (_context.AgentHandoffChain.Count > 1)
                    {
                        // pop agent off the chain
                        _context.AgentHandoffChain.RemoveAt(_context.AgentHandoffChain.Count - 1);
                        var agentName = _context.AgentHandoffChain[^1];
                        var newAgent = _agentFactory.GetAgent(agentName);

                        _currentAgent = newAgent;
                        _context = _context with { CurrentAgent = _currentAgent.Name };
                        _context = await _threadRepository.UpdateAgentContextAsync(_context);

                        _logger.LogInternalInformation("Handoff back to agent: {AgentName}", agentName);

                        var handoffMessage = new ChatMessage(ChatRole.Assistant, $"Handed off to agent {agentName}. Assume this persona immediately and continue with the task.");
                        await PersistReasoningMessageAsync(agentChatHistory, handoffMessage);

                        iterationResult(new ReasoningLoopIterationResult { IsContinuation = true });
                        yield break;
                    }
                    else
                    {
                        _logger.LogInternalInformation("AgentHandoffChain is empty or has only one agent, ending reasoning loop.");

                        // If the agent cannot handle the request but there is no handoff back, we just reply with the message and end the reasoning loop
                        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                            _context,
                            new ChatMessage(ChatRole.Assistant, agentOutput.OutputMessage));

                        iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
                        yield break;
                    }
                }
                else
                {
                    await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                        _context,
                        new ChatMessage(ChatRole.Assistant, agentOutput.OutputMessage));
                }
            }
        }

        _logger.LogInternalInformation("Reasoning loop iteration completed.");
        iterationResult(new ReasoningLoopIterationResult { IsContinuation = false });
    }

    private RunHooks<AgentContext> CreateRunHooks()
    {
        return new RunHooks<AgentContext>
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
            OnAgentStart = (context, agent) =>
            {
                if (_currentAgentSpan is not null)
                {
                    _currentAgentSpan.End();
                    _currentAgentSpan = null;
                }

                _logger.LogInternalInformation("Trace invoke agent: {AgentName}", agent.Name);
                _currentAgentSpan = _tracer.StartActiveSpan($"invoke.agent.{agent.Name}", SpanKind.Internal, _rootSpan);
                _currentAgentSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
                _currentAgentSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                _currentAgentSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.InvokeAgent);
                return Task.CompletedTask;
            },
            OnAgentEnd = (context, agent, output) =>
            {
                _logger.LogInternalInformation("Trace Ending agent: {AgentName}", agent.Name);
                _currentAgentSpan?.End();
                _currentAgentSpan = null;
                return Task.CompletedTask;
            },
            OnHandoff = (context, agent, handoffAgent) =>
            {
                _logger.LogInternalInformation("Trace Handoff from agent: {AgentName} to agent: {HandoffAgentName}", agent.Name, handoffAgent.Name);
                _currentToolSpan = _tracer.StartSpan($"handoff", SpanKind.Internal, _currentAgentSpan);
                _currentToolSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
                _currentToolSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Handoff);
                _currentToolSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                _currentToolSpan.SetAttribute(TraceAttribute.HandeOffAgentName, handoffAgent.Name);
                _currentToolSpan.End();
                _currentToolSpan = null;
                _currentAgentSpan?.End();
                _context.AgentHandoffChain.Add(handoffAgent.Name);
                //_currentAgent = handoffAgent;
                //return _threadRepository.UpdateAgentContextAsync(_context);
                return Task.CompletedTask;
            },
            OnToolStart = (context, agent, tool, input) =>
            {
                _logger.LogInternalInformation("Trace Starting tool: {ToolName} for agent: {AgentName}", tool.Name, agent.Name);
                _currentToolSpan = _tracer.StartActiveSpan($"tool.{tool.Name}", SpanKind.Internal, _currentAgentSpan);
                _currentToolSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
                _currentToolSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Tool);
                _currentToolSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                _currentToolSpan.SetAttribute(TraceAttribute.ToolName, tool.Name);
                _currentToolSpan.SetAttribute(TraceAttribute.ToolInput, FormatToolArguments(input));
                _currentToolSpan.SetAttribute(TraceAttribute.ModelTemperature, agent.Temperature.ToString());
                _currentToolSpan.SetAttribute(TraceAttribute.ToolDescription, tool.Description);
                return Task.CompletedTask;
            },
            OnToolEnd = (context, agent, tool, output) =>
            {
                _logger.LogInternalInformation("Trace Ending tool: {ToolName} for agent: {AgentName}", tool.Name, agent.Name);
                _currentToolSpan?.SetAttribute(TraceAttribute.ToolOutput, output?.ToString() ?? string.Empty);
                _currentToolSpan?.End();
                _currentToolSpan = null;
                return Task.CompletedTask;
            },
            OnModelGenerationStart = (context, agent, messages, chatOptions) =>
            {
                _logger.LogInternalInformation("Trace Starting model generation for agent: {AgentName}", agent.Name);
                _currentGenerationSpan = _tracer.StartActiveSpan($"model_generation", SpanKind.Internal, _currentAgentSpan);
                _currentGenerationSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
                _currentGenerationSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
                _currentGenerationSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.ModelGeneration);
                _currentGenerationSpan.SetAttribute(TraceAttribute.ModelInput, FormatChatMessages(messages));
                return Task.CompletedTask;
            },
            OnModelGenerationEnd = (context, agent, response) =>
            {
                _logger.LogInternalInformation("Trace Ending model generation for agent: {AgentName}", agent?.Name ?? "Unknown");
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelOutput, FormatChatMessages(response?.Messages ?? []));
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelInputTokensCount, response?.Usage?.InputTokenCount?.ToString() ?? string.Empty);
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelOutputTokensCount, response?.Usage?.OutputTokenCount?.ToString() ?? string.Empty);
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelTotalTokensCount, response?.Usage?.TotalTokenCount?.ToString() ?? string.Empty);
                _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelTemperature, agent?.Temperature.ToString() ?? string.Empty);
                _currentGenerationSpan?.End();
                _currentGenerationSpan = null;
                return Task.CompletedTask;
            }
        };
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
            var functionResult = await aiTool.InvokeAsync(new AIFunctionArguments(functionCall.Arguments), cancellationToken);
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
                    OboToken: null,
                    OboTokenScope: attribute.Scope);

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

    private static bool CheckAzCliWriteToolCall(ManualToolCall toolCall)
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

    private static bool CheckKubectlWriteToolCall(ManualToolCall toolCall)
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

    private async Task PersistReasoningMessagesAsync(AgentChatHistory agentChatHistory, IEnumerable<ChatMessage> chatMessages)
    {
        _chatHistory!.AddRange(chatMessages);
        // Calling ToList() is important here because otherwise the reasoning messages get new IDs every time
        // the reasoningMessages IEnumerable is enumerated.
        var reasoningMessages = chatMessages.Select(msg => msg.GetReasoningMessage(_context.Id)).ToList();

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
            return await toolCall.Tool.InvokeAsync(new AIFunctionArguments(toolCall.FunctionCall.Arguments), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error while calling tool {ToolName}", toolCall.Tool!.Name);
            return GetErrorMessage(toolCall.FunctionCall, ex);
        }
    }

    private WriteActionActivityOutput CheckWriteActionInReadOnlyMode(ManualToolCall toolCall)
    {
        try
        {
            if (toolCall.FunctionCall == null)
            {
                return new WriteActionActivityOutput()
                {
                    IsWriteAction = false,
                };
            }

            var attribute = toolCall.Tool.UnderlyingMethod?.GetCustomAttribute<WriteActionAttribute>();
            if (attribute == null)
            {
                return new WriteActionActivityOutput()
                {
                    IsWriteAction = false,
                };
            }

            var prompt = $"You are in read-only mode. You MUST NOT perform any write action. You should ONLY provide suggestions to user for what to do next using NotifyUser. " +
            "Please format your suggestions in a user-friendly way:\n" +
            "- If suggesting CLI commands (like az cli, kubectl, shell scripts, etc.), format them using markdown ```shell code blocks``` for easy copy-paste\n" +
            "- If the command is accurate and ready to use, tell the user they can copy and paste it directly\n" +
            "- Provide clear explanations of what each suggested action will do\n" +
            "- Use bullet points or numbered lists to organize multiple suggestions\n" +
            "- Always wait for user confirmation before proceeding\n" +
            "- Only proceed with next steps if user explicitly tells you the actions have been taken.";

            prompt += $"\nThe suggestion is to call Function '{toolCall.FunctionCall.Name}' with arguments: {System.Text.Json.JsonSerializer.Serialize(toolCall.FunctionCall.Arguments)}. " +
                        "Please format this as a clear, actionable instruction to user" +
                        "Before providing suggestions, think through:\n" +
                        "1. Context Analysis: What is the user trying to achieve? What's the current state?\n" +
                        "2. Risk Assessment: Are there any potential issues or prerequisites the user should know about?\n" +
                        "3. Alternative Approaches: Are there multiple ways to accomplish this goal?\n" +
                        "4. Success Criteria: How will the user know if the suggested action worked?\n\n" +
                        "## Chain of Thought Structure:\n" +
                        "Follow this reasoning pattern:\n" +
                        "- Understand: Explain what the function call would do and why it's needed\n" +
                        "- Prepare: Identify any prerequisites or setup steps\n" +
                        "- Execute: Provide the specific action with clear formatting\n" +
                        "- Verify: Suggest how to confirm the action was successful\n" +
                        "- Next Steps: Indicate what should happen after completion";

            return new WriteActionActivityOutput()
            {
                IsWriteAction = true,
                Prompt = prompt,
                NeedSkip = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error while writing action in read-only mode: {ToolName}", toolCall.Tool.Name);
            return new WriteActionActivityOutput
            {
                ModifiedFunctionCall = null,
                IsWriteAction = false,
                Prompt = GetErrorMessage(toolCall.FunctionCall, ex),
                NeedSkip = true
            };
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
                return default!;
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
            return default!;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "All retry attempts failed for {OperationName}", operationName);
            throw;
        }
    }

    // Helper method to format tool input arguments into a readable string
    private static string FormatToolArguments(IEnumerable<KeyValuePair<string, object?>>? arguments)
    {
        if (arguments == null)
        {
            return string.Empty;
        }

        try
        {
            var argsDict = arguments.ToDictionary(kv => kv.Key, kv => kv.Value);
            return JsonSerializer.Serialize(argsDict, _toolArgumentsJsonOptions);
        }
        catch (Exception)
        {
            return string.Join(", ", arguments.Select(kv => $"{kv.Key}: {kv.Value?.ToString() ?? "null"}"));
        }
    }

    private static string FormatChatMessages(IEnumerable<ChatMessage> messages)
    {
        if (messages == null || !messages.Any())
        {
            return string.Empty;
        }

        try
        {
            var formattedMessages = messages.Select(message => new
            {
                Role = message.Role.ToString(),
                Contents = message.Contents?.Select(content => new
                {
                    Type = content.GetType().Name,
                    Value = content
                })
            }).ToList();

            return JsonSerializer.Serialize(formattedMessages, _chatMessageJsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error formatting messages: {ex.Message}\n" +
                   string.Join("\n", messages.Select(m => $"{m.Role}: {m.Text?[..50]}..."));
        }
    }

    // helper method to handle TurnLimitReachedException in a streaming manner
    private async Task<RunResult<AgentContext>?> HandleTurnLimitExceptionStreamingAsync(
        TurnLimitReachedException<AgentContext> ex,
        AgentChatHistory agentChatHistory,
        RunConfig runConfig,
        CancellationToken cancellationToken)
    {
        _logger.LogInternalWarning("Turn limit reached.", ex);

        var result = ex.RunResult;
        await PersistReasoningMessagesAsync(agentChatHistory, result.NewItems);

        var progressSummaryAgent = _agentFactory.GetAgent("progress_summary_agent");

        var summaryResult = await Runner.RunAsync(
            startingAgent: progressSummaryAgent,
            input: [.. result.Input, .. result.NewItems],
            config: runConfig,
            context: _context,
            maxTurns: 1,
            cancellationToken: cancellationToken
        );

        var summary = summaryResult.Output?.ToString();

        if (string.IsNullOrEmpty(summary))
        {
            throw new Exception("Progress summary agent returned no output.");
        }

        var assistantMessage = new ChatMessage(ChatRole.Assistant, summary);
        await PersistReasoningMessageAsync(agentChatHistory, assistantMessage);
        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, assistantMessage);

        // Also return RunResult for streaming
        return new RunResult<AgentContext>(_currentAgent)
        {
            Input = _chatHistory ?? [],
            NewItems = [assistantMessage],
            RawResponses = [],
            CurrentTurn = result.CurrentTurn,
            MaxTurns = result.MaxTurns,
            Output = summary,
            ContextWrapper = new RunContextWrapper<AgentContext>(_context),
            Trajectory = new Trajectory()
        };
    }
}

