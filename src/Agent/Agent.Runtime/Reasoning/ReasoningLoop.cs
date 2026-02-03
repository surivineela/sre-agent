// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Agent.Common.Services;
using Agent.Core.Attributes;
using Agent.Core.Configuration;
using Agent.Core.Exceptions;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.AgentMemory;
using Agent.Framework;
using Agent.Framework.Hooks;
using Agent.Framework.Skills;
using Agent.Logging;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.AgentTasks.Handlers;
using Agent.Runtime.ConversationModifiers;
using Agent.Runtime.Helpers;
using Agent.Runtime.SubAgents.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using TodoItem = Agent.Core.Models.Api.v1.TodoItem;

namespace Agent.Runtime.Reasoning;

public class ReasoningLoop : IDisposable
{
    private readonly ILogger<ReasoningLoop> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IStreamingMessageRepository _streamingMessageRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly IAgentProvider<AgentContext> _agentProvider;
    private readonly ActionSettings _actionSettings;
    private readonly Tracer _tracer;
    private readonly CustomerLogger _customerLogger;
    private readonly bool _enableReasoningDebugOutput;
    private readonly ISearchEndpointService _searchEndpointService;
    private readonly SearchHelper _searchHelper;
    private readonly FeatureConfigModel _featureConfig;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly bool _modeSwitchEnabled;
    private readonly ModeSwitchHandler? _modeSwitchHandler; // encapsulates /mode conversation|workflow switching (feature-flag gated)
    private readonly ISkillRegistry _skillRegistry;

    // feature properties
    private readonly bool _enableDocumentRetrieval;
    private readonly bool _agentMemoryEnabled;
    private readonly bool _autoHandOffEnabled;

    private readonly Channel<ReasoningLoopMessage> _msgCh;
    private AgentContext _context;
    private Agent<AgentContext> _defaultStartingAgent;
    private Agent<AgentContext> _currentAgent;
    private List<ChatMessage>? _chatHistory;

    private TelemetrySpan? _rootSpan;
    private TelemetrySpan? _currentAgentSpan;
    private readonly ConcurrentDictionary<string, TelemetrySpan?> _toolSpans = new();
    private readonly ConcurrentDictionary<string, (McpToolExecution Execution, Guid MessageId)> _mcpExecutions = new();
    private TelemetrySpan? _currentGenerationSpan;
    private Stopwatch? _currentGenerationStopwatch;
    private Exception? _currentException;
    private TelemetrySpan? _currentSummarizerSpan;
    private TelemetrySpan? _currentCriticSpan;
    private TelemetrySpan? _currentCompactionSpan;
    private readonly IAgentRuntimeModifier<AgentContext> _agentRuntimeModifier;
    private readonly object _userCancellationTokenSourceLock = new();
    private CancellationTokenSource _userCancellationTokenSource = new();
    private readonly SemaphoreSlim _semaphore = new(initialCount: 1, maxCount: 1);
    private bool _disposed = false;

    // Store todo arguments for processing in OnToolEnd
    private IEnumerable<KeyValuePair<string, object?>>? _currentTodoArguments = null;

    // Track which missing tools have already been warned about per subagent to avoid duplicate notifications
    // Key: SubAgentName, Value: HashSet of warned tool names
    private readonly Dictionary<string, HashSet<string>> _warnedMissingToolsPerSubAgent = new();

    // Retry configuration
    private const int MaxRetryAttempts = 3;

    // Maximum number of iterations to run the reasoning loop
    private const int MaxIterations = 10;
    private readonly IAgentMemoryClient _agentMemoryClient;
    private readonly ISearchIndexService _searchIndexService;
    private readonly AgentMemorySettings _agentMemorySettings;
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)];

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

    private const string RetrieveMarker = "#retrieve";
    private const string RememberMarker = "#remember";
    private const string ForgetMarker = "#forget";
    private const string CompactMarker = "/compact";
    private const string IncidentRetroModeMarker = "/incidentRetroMode";

    // user-required action tracking
    private ReasoningLoopIterationResult? LastIterationResult { get; set; } = null;

    // tool output processing
    private readonly IToolOutputProcessService _toolOutputProcessService;

    private readonly IAgentFileStorageService _agentFileStorageService;

    // ambient context provider for VS Code tools integration
    private readonly IAmbientContextProvider _ambientContextProvider;

    // hook manager for executing agent hooks
    private readonly HookManager _hookManager;

    public ReasoningLoop(
        ILoggerFactory loggerFactory,
        IChatClientProvider chatClientProvider,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IStreamingMessageRepository streamingMessageRepository,
        Agent<AgentContext> defaultStartingAgent, // for autohandoff
        Agent<AgentContext> startingAgent,
        IThreadRepository threadRepository,
        AgentContext context,
        IToolFactory<AgentContext> toolFactory,
        ActionSettings actionSettings,
        Tracer tracer,
        CustomerLogger customerLogger,
        IAgentProvider<AgentContext> agentProvider,
        bool enableReasoningDebugOutput,
        ISearchEndpointService searchEndpointService,
        SearchHelper searchHelper,
        IAgentMemoryClient agentMemoryClient,
        ISearchIndexService searchIndexService,
        AgentMemorySettings agentMemorySettings,
        FeatureConfigModel featureConfig,
        IAgentRuntimeModifier<AgentContext> agentRuntimeModifier,
        ISkillRegistry skillRegistry,
        IToolOutputProcessService toolOutputProcessService,
        IAgentFileStorageService agentFileStorageService,
        IHostEnvironment hostEnvironment,
        IAmbientContextProvider ambientContextProvider,
        bool modeSwitchEnabled,
        HookManager hookManager)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<ReasoningLoop>();
        _chatClientProvider = chatClientProvider;
        _outboundCommunicationService = outboundCommunicationService;
        _streamingMessageRepository = streamingMessageRepository;
        _msgCh = Channel.CreateUnbounded<ReasoningLoopMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = true
        });
        _threadRepository = threadRepository;
        _context = context;
        _toolFactory = toolFactory;
        _defaultStartingAgent = defaultStartingAgent;
        _currentAgent = startingAgent;
        _actionSettings = actionSettings;
        _tracer = tracer;
        _customerLogger = customerLogger;
        _agentProvider = agentProvider;
        _enableReasoningDebugOutput = enableReasoningDebugOutput;
        _searchEndpointService = searchEndpointService;
        _searchHelper = searchHelper ?? throw new ArgumentNullException(nameof(searchHelper));
        _agentMemoryClient = agentMemoryClient;
        _searchIndexService = searchIndexService;
        _agentMemorySettings = agentMemorySettings;
        _featureConfig = featureConfig;
        _hostEnvironment = hostEnvironment;
        _autoHandOffEnabled = featureConfig.AutoHandoffEnabled;
        _enableDocumentRetrieval = featureConfig.RegionalSearchEnabled;
        _agentMemoryEnabled = featureConfig.AgentMemoryEnabled;
        _agentRuntimeModifier = agentRuntimeModifier;
        _modeSwitchEnabled = modeSwitchEnabled;
        _skillRegistry = skillRegistry;
        _ambientContextProvider = ambientContextProvider;
        _hookManager = hookManager;
        if (_modeSwitchEnabled)
        {
            // Initialize handler only when feature flag enabled to keep overhead minimal for other agents
            _modeSwitchHandler = new ModeSwitchHandler(_threadRepository, _outboundCommunicationService, enabled: true);
        }

        var globalDefaultMode = actionSettings.Mode.ToString() ?? AgentModes.Review;
        if (!string.IsNullOrEmpty(context.AgentMode) && !string.Equals(context.AgentMode, globalDefaultMode, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInternalInformation("Setting agent mode to {AgentMode} for thread {ThreadId} (global default: {GlobalMode})",
                context.AgentMode, context.ThreadId, globalDefaultMode);
            _ = Task.Run(async () => await _agentRuntimeModifier.SetAgentMode(context, context.AgentMode, notifyUser: false));
        }

        _logger.LogInternalInformation("Experimental Flag: AgentMemoryEnabled: {agentMemoryEnabled}", _agentMemoryEnabled);
        _logger.LogInternalInformation("Experimental Flag: AutoHandOffEnabled: {autoHandOffEnabled}", _autoHandOffEnabled);
        _logger.LogInternalInformation("[{ThreadId}] Active Experiment Variants: {experimentVariants}",
            _context.ThreadId,
            FormatExperimentVariants(_agentProvider.GetActiveVariants(_context.ThreadId.ToString())));
        _toolOutputProcessService = toolOutputProcessService;
        _agentFileStorageService = agentFileStorageService;
    }

    public virtual void CancelCurrentOperation()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReasoningLoop));
        }

        if (_context.ContextState == ContextStateEnum.Idle || _context.ContextState == ContextStateEnum.PendingApproval)
        {
            // If the context is idle or pending approval, there's no operation to cancel
            // This is a no-op, but we log it for clarity
            _logger.LogInternalInformation("[{threadId}]No operation to cancel, agent context is idle or pending approval.", _context.ThreadId);
            return;
        }

        lock (_userCancellationTokenSourceLock)
        {
            _logger.LogInternalInformation("[{threadId}]Cancelling current operation.", _context.ThreadId);
            _userCancellationTokenSource.Cancel();
        }
    }

    public virtual async Task SetHomeAgent(string agentName)
    {
        if (string.IsNullOrEmpty(agentName))
        {
            return;
        }

        try
        {
            var currentAgent = _agentProvider.GetAgent(agentName, _context.ThreadId.ToString());
            _context = _context with
            {
                CurrentAgent = agentName.ToLower(),
                AgentHandoffChain = [agentName.ToLower()]
            };
            _currentAgent = currentAgent;
            _defaultStartingAgent = currentAgent;  // Set as home agent. during auto handoff, the agent will return to this agent
            _context = await _threadRepository.UpdateAgentContextAsync(_context);

            _logger.LogInternalInformation("Successfully set home agent to '{AgentName}' for thread {ThreadId}", agentName, _context.ThreadId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to set home agent '{AgentName}' for thread {ThreadId}. Continuing with current agent '{CurrentAgent}'.",
                agentName, _context.ThreadId, _currentAgent.Name);
        }
    }

    public virtual async Task AppendNewUserMessageAsync(
        ChatMessage msg,
        ConversationModifierEnum? conversationModifier = null,
        CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("[{threadId}]Appending new chat message", _context.ThreadId);
            await _msgCh.Writer.WriteAsync(new ReasoningLoopChatMessage(msg, conversationModifier), cancellationToken);

            _ = Task.Run(RunWithUserCancellationAsync, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Channel is closed.");
        }
    }

    public virtual async Task AppendFunctionCallMessagesAsync(List<ChatMessage> msgs, CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("[{threadId}]Appending new function call message", _context.ThreadId);
            await _msgCh.Writer.WriteAsync(new ReasoningLoopFunctionCall(msgs), cancellationToken);

            _ = Task.Run(RunWithUserCancellationAsync, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Channel is closed.");
        }
    }

    public virtual async Task AppendNewApprovalMessageAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
        {
            _logger.LogInternalInformation("[{threadId}]Appending new approval message", _context.ThreadId);
            await _msgCh.Writer.WriteAsync(new ReasoningLoopApprovalMessage(approval), cancellationToken);

            _ = Task.Run(RunWithUserCancellationAsync, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Channel is closed.");
        }
    }

    /// <summary>
    /// Early completion for /mode switch path: set Idle + signal processing complete so UI stops spinner.
    /// Safe to call multiple times.
    /// </summary>
    private async Task CompleteEarlyModeSwitchAsync(CancellationToken ct)
    {
        try
        {
            if (_context.ContextState != ContextStateEnum.Idle)
            {
                await ChangeAgentContextStateAsync(ContextStateEnum.Idle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "[{threadId}]Failed to set Idle after mode switch.", _context.ThreadId);
        }

        try
        {
            await _outboundCommunicationService.SignalProcessingComplete(_context.ThreadId, cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("[{threadId}]SignalProcessingComplete canceled after mode switch.", _context.ThreadId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "[{threadId}]Failed to signal completion after mode switch.", _context.ThreadId);
        }
    }

    public virtual async Task LoadChatHistoryAsync()
    {
        if (_chatHistory != null)
        {
            return;
        }

        var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
        if (agentChatHistory == null)
        {
            _logger.LogInternalError("[{threadId}]No chat history found for agent context {agentContextId}, this should never happen.", _context.ThreadId, _context.Id);
            // should never happen
            _chatHistory = [];
            return;
        }

        var reasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(_threadRepository);
        _chatHistory = reasoningMessages.GetChatMessages();
    }

    public virtual Task<IEnumerable<ChatMessage>> ExportChatHistoryAsync(CancellationToken cancellationToken)
    {
        //TODO - synchronization with writers. Currently only used during development so not a blocker.
        IEnumerable<ChatMessage> history = _chatHistory?.ToArray() ?? [];
        return Task.FromResult(history);
    }

    private async Task RunWithUserCancellationAsync()
    {
        if (!_context.IsIncidentTestModeEnabled.HasValue)
        {
            var thread = await _threadRepository.GetThreadAsync(_context.ThreadId);
            _context = _context with
            {
                IsIncidentTestModeEnabled = thread?.IsIncidentTestModeEnabled ?? false
            };
        }

        ThreadContextAccessor.SetThreadContext(_context);

        // Ensure that only one thread runs at a time
        if (!await _semaphore.WaitAsync(0))
        {
            return;
        }

        try
        {
            RefreshUserCancellationTokenSource();
            await RunAsync(_userCancellationTokenSource.Token);
        }
        catch (OperationCanceledException e)
        {
            if (e.CancellationToken == _userCancellationTokenSource.Token)
            {
                _logger.LogInternalInformation("[{threadId}]{RunInternalAsync} was canceled by user.", _context.ThreadId, nameof(RunInternalAsync));
            }
            else
            {
                _logger.LogInternalWarning("[{threadId}]{RunInternalAsync} was unexpectedly canceled", _context.ThreadId, nameof(RunInternalAsync));
            }

            // todo: do we need to cleanup existing approvals?
            if (_context.ContextState != ContextStateEnum.Idle)
            {
                await ChangeAgentContextStateAsync(ContextStateEnum.Idle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[{threadId}]An error occurred while running the reasoning loop with user cancellation.", _context.ThreadId);
        }
        finally
        {
            await _outboundCommunicationService.SignalProcessingComplete(_context.ThreadId, cancellationToken: _userCancellationTokenSource.Token);
            _semaphore.Release();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (_msgCh.Reader.TryRead(out var reasoningLoopMessage))
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReasoningLoopIterationResult? iterationResult = null;
            uint currentIterationCount = 0;

            if (_rootSpan == null)
            {
                // don't reset the root span if one exists (loop continuation)
                _rootSpan = _tracer.StartReasoningLoopRootSpan(
                    _context.ThreadId.ToString(),
                    WebJsonSerializer.Serialize(_featureConfig),
                    FormatExperimentVariants(_agentProvider.GetActiveVariants(_context.ThreadId.ToString())));
            }

            try
            {
                _logger.LogInternalInformation("[{threadId}]Received new message. Running reasoning loop...", _context.ThreadId);

                var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);

                if (_context.ContextState != ContextStateEnum.Processing)
                {
                    await ChangeAgentContextStateAsync(ContextStateEnum.Processing);
                }

                if (agentChatHistory == null)
                {
                    _logger.LogInternalError("[{threadId}] AgentChatHistory is null", _context.ThreadId);
                    throw new InvalidOperationException("AgentChatHistory is null");
                }

                switch (reasoningLoopMessage)
                {
                    case ReasoningLoopChatMessage chatMessage:
                        {
                            // If there are pending approvals or CLI executions, cancel them and continue with the new message
                            if (await HasPendingApprovalsOrCliExecutionsAsync())
                            {
                                _logger.LogInternalInformation("[{threadId}]Cancelling all pending approvals and CLI executions due to new user message.", _context.ThreadId);
                                await CancelAllPendingApprovalsAndCliExecutionsAsync(agentChatHistory);
                            }

                            // Unified /mode handling (RCA router only). Minimal surface area: single call; early-return if handled.
                            if (_modeSwitchEnabled && _modeSwitchHandler != null)
                            {
                                var (handled, updatedCtx) = await _modeSwitchHandler.HandleAsync(_context, chatMessage.Message.Text, cancellationToken);
                                if (handled)
                                {
                                    _context = updatedCtx;
                                    await CompleteEarlyModeSwitchAsync(cancellationToken);
                                    return; // Mode switch processed; stop normal loop work this turn
                                }
                            }

                            var shouldStop = await HandleUnprocessedToolCallsAsync(agentChatHistory, cancellationToken);
                            if (shouldStop)
                            {
                                return;
                            }

                            // process #remember command
                            if (chatMessage.Message.Text.StartsWith(RememberMarker, StringComparison.OrdinalIgnoreCase) && _agentMemoryEnabled)
                            {
                                await HandleRememberCommandAsync(agentChatHistory, chatMessage.Message.Text, cancellationToken);
                                return;
                            }

                            // process #retrieve command
                            if (chatMessage.Message.Text.StartsWith(RetrieveMarker, StringComparison.OrdinalIgnoreCase) && _agentMemoryEnabled)
                            {

                                await HandleRetrieveCommandAsync(agentChatHistory, chatMessage.Message.Text, cancellationToken);
                                return;
                            }

                            // process #forget command
                            if (chatMessage.Message.Text.StartsWith(ForgetMarker, StringComparison.OrdinalIgnoreCase) && _agentMemoryEnabled)
                            {
                                await HandleForgetCommandAsync(agentChatHistory, chatMessage.Message.Text, cancellationToken);
                                return;
                            }

                            // process /compact command
                            if (chatMessage.Message.Text.StartsWith(CompactMarker, StringComparison.OrdinalIgnoreCase))
                            {
                                await HandleCompactCommandAsync(chatMessage.Message.Text, cancellationToken);
                                return;
                            }

                            // process /incidentRetroMode command
                            if (chatMessage.Message.Text.Trim().Equals(IncidentRetroModeMarker, StringComparison.OrdinalIgnoreCase))
                            {
                                await HandleIncidentRetroModeCommandAsync();
                                return;
                            }

                            var sb = ConstructUserMessage(chatMessage);
                            var msg = new ChatMessage(chatMessage.Message.Role, sb.ToString());

                            _logger.LogInternalInformation("[{threadId}]Processing chat message.", _context.ThreadId);

                            _tracer.RecordUserMessageSpan(_context.ThreadId.ToString(), chatMessage.Message.Text, _rootSpan);

                            await PersistReasoningMessageAsync(agentChatHistory, msg);

                            // Process conversation modifier if present
                            if (chatMessage.ConversationModifier.HasValue)
                            {
                                var modificationResult = await ProcessConversationModifierAsync(chatMessage.ConversationModifier.Value, chatMessage.Message.Text, cancellationToken);
                                if (!modificationResult.PassToMainLoop)
                                {
                                    // Modifier handled the message, no need to continue with main loop
                                    TracerExtensions.EndAndClear(ref _rootSpan);
                                    return;
                                }
                            }

                            break;
                        }
                    case ReasoningLoopApprovalMessage approvalMessage:
                        {
                            _logger.LogInternalInformation("[{threadId}]Processing approval message.", _context.ThreadId);

                            if (_context.ContextState != ContextStateEnum.PendingApproval)
                            {
                                _logger.LogInternalWarning("[{threadId}]Received approval message while not in PendingApproval state, but in state: {State}", _context.ThreadId, _context.ContextState);
                            }

                            _tracer.RecordUserApprovalSpan(
                                _context.ThreadId.ToString(),
                                approvalMessage.Approval.Description.ToString(),
                                approvalMessage.Approval.Status.ToString(),
                                _rootSpan);

                            var approval = approvalMessage.Approval;
                            var shouldStop = await ProcessNewApprovalAsync(agentChatHistory, approval, cancellationToken);
                            if (shouldStop || await HasPendingApprovalsOrCliExecutionsAsync())
                            {
                                return;
                            }

                            break;
                        }
                    case ReasoningLoopFunctionCall functionCall:
                        {
                            _logger.LogInternalInformation("[{threadId}]Processing function call messages.", _context.ThreadId);

                            // Parse function call and result from the messages
                            FunctionCallContent? functionCallContent = null;
                            FunctionResultContent? functionResultContent = null;
                            foreach (var message in functionCall.Messages)
                            {
                                if (message.Role == ChatRole.Assistant)
                                {
                                    functionCallContent = message.Contents.OfType<FunctionCallContent>().FirstOrDefault();
                                }
                                else if (message.Role == ChatRole.Tool)
                                {
                                    functionResultContent = message.Contents.OfType<FunctionResultContent>().FirstOrDefault();
                                }
                            }

                            string? toolName = null;
                            string? toolInput = null;
                            string? toolOutput = null;

                            if (functionCallContent != null)
                            {
                                toolName = functionCallContent.Name;
                                toolInput = JsonSerializer.Serialize(functionCallContent.Arguments, _toolArgumentsJsonOptions);
                            }

                            if (functionResultContent != null)
                            {
                                var resultString = functionResultContent.Result?.ToString() ?? "null";
                                toolOutput = resultString.Substring(0, Math.Min(500, resultString.Length));
                            }

                            _tracer.RecordUserContinueToolSpan(_context.ThreadId.ToString(), toolName, toolInput, toolOutput, _rootSpan);

                            var toolMessages = functionCall.Messages.Where(m => m.Role == ChatRole.Tool);
                            var toolResults = new List<ChatMessage>();
                            if (_featureConfig.PartialOutputEnabled)
                            {
                                foreach (var toolMessage in toolMessages)
                                {
                                    var functionResult = toolMessage.Contents.OfType<FunctionResultContent>().FirstOrDefault();
                                    if (functionResult != null)
                                    {
                                        var funcCallName = functionCall.Messages.Where(m => m.Role == ChatRole.Assistant)
                                            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                                            .Where(m => m.CallId == functionResult.CallId).FirstOrDefault()?.Name ?? "";
                                        var processedOutput = await _toolOutputProcessService.ProcessToolOutputAsync(
                                            _context.ThreadId,
                                            funcCallName,
                                            functionResult.CallId,
                                            functionResult.Result?.ToString() ?? "",
                                            cancellationToken);

                                        toolResults.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent(functionResult.CallId, processedOutput)]));
                                    }
                                }
                            }
                            else
                            {
                                toolResults.AddRange(toolMessages);
                            }

                            await PersistReasoningMessagesAsync(agentChatHistory, toolResults);

                            if (await HasPendingApprovalsOrCliExecutionsAsync())
                            {
                                _logger.LogInternalInformation("[{threadId}]Pending approvals or CLI executions exist after function call. Halting reasoning loop.", _context.ThreadId);
                                return;
                            }

                            break;
                        }
                    case ReasoningLoopContinuation continuation:
                        {
                            _logger.LogInternalInformation("[{threadId}]Received continuation message. current iteration {currentIterationCount} Running reasoning loop...", _context.ThreadId, continuation.CurrentIterationCount);
                            currentIterationCount = continuation.CurrentIterationCount;
                            break;
                        }
                    default:
                        _logger.LogInternalWarning("[{threadId}]Received unknown message type: {Type}", _context.ThreadId, reasoningLoopMessage.GetType());
                        continue;
                }

                if (LastIterationResult != null && !LastIterationResult.AreUserActionsCompleted)
                {
                    // there are pending user actions from the last iteration, do not continue
                    return;
                }

                iterationResult = await RunInternalAsync(agentChatHistory, cancellationToken);
                currentIterationCount++;

                if (iterationResult.IsContinuation)
                {
                    _logger.LogInternalInformation("[{threadId}]Iteration result indicates continuation. Preparing for next iteration.", _context.ThreadId);

                    if (currentIterationCount >= MaxIterations)
                    {
                        _logger.LogInternalWarning("[{threadId}] Maximum iterations reached ({maxIterations}). Ending reasoning loop.", _context.ThreadId, MaxIterations);

                        var assistantMessage = new ChatMessage(ChatRole.Assistant,
                            "I've been working on your request for a while. Would you like me to keep going, or do you want to provide more details or guidance?");

                        await PersistReasoningMessageAsync(agentChatHistory, assistantMessage);

                        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                            _context,
                            assistantMessage);

                        iterationResult.IsContinuation = false;
                        return;
                    }

                    if (await _msgCh.Writer.WaitToWriteAsync(cancellationToken))
                    {
                        await _msgCh.Writer.WriteAsync(new ReasoningLoopContinuation(currentIterationCount), cancellationToken);
                    }
                    else
                    {
                        // can't write to the channel, set the continuation flag to false and end the loop
                        iterationResult.IsContinuation = false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "[{threadId}]An error occurred during reasoning loop.", _context.ThreadId);
            }
            finally
            {
                if (iterationResult?.IsContinuation == false)
                {
                    await _outboundCommunicationService.SignalProcessingComplete(_context.ThreadId, cancellationToken: _userCancellationTokenSource.Token);
                    // only end the root span if we didn't continue the loop
                    TracerExtensions.EndAndClear(ref _rootSpan);
                }

                var thread = await _threadRepository.GetThreadAsync(_context.ThreadId);
                if (thread != null && thread.Source == ThreadSource.ScheduledTask)
                {
                    await CompactChatAsync(
                        compactInstructions: "",
                        compactReason: CompactReason.ScheduledTaskAutoCompact,
                        notifyUser: false,
                        cancellationToken);
                }

                if (_context.ContextState == ContextStateEnum.Processing)
                {
                    await ChangeAgentContextStateAsync(ContextStateEnum.Idle);
                }

                LastIterationResult = iterationResult;
            }
        }
    }

    private async ValueTask<bool> HasPendingApprovalsOrCliExecutionsAsync()
    {
        if (await HasPendingApprovalsAsync())
        {
            return true;
        }

        var (azCliExecution, kubectlExecution, psqlExecution) = await ListPendingExecutions(_context.ThreadId);

        if (azCliExecution != null || kubectlExecution != null || psqlExecution != null)
        {
            _logger.LogInternalInformation("[{threadId}]Pending cli executions exist.", _context.ThreadId);
            return true;
        }

        return false;
    }

    private ValueTask<bool> HasPendingApprovalsAsync()
    {
        var hasPending = _context.ApprovalInformation != null &&
            _context.ApprovalInformation.PendingApprovals.Count > 0;

        if (hasPending)
        {
            _logger.LogInternalInformation("[{threadId}]Pending approvals exist.", _context.ThreadId);
        }

        return ValueTask.FromResult(hasPending);
    }

    private string ConstructUserMessage(ReasoningLoopChatMessage chatMessage)
    {
        var sb = new StringBuilder();

        var prependedInstructions = false;

        // only add prompts in vanilla mode
        if (!_currentAgent.EnableVanillaMode)
        {
            // Check for user prompt override
            if (chatMessage.ConversationModifier.HasValue
                && Modifiers.TryGet(chatMessage.ConversationModifier.Value, out var modifier)
                && !string.IsNullOrEmpty(modifier?.UserPromptOverride))
            {
                _logger.LogInternalInformation("[{threadId}]Using UserPromptOverride from modifier {modifierName}",
                    _context.ThreadId, modifier.DisplayName);
                sb.AppendLine(modifier.UserPromptOverride);
                prependedInstructions = true;
            }
            else
            {
                // Default behavior
                sb.AppendLine("Try your best to answer the user's questions. Keep in mind:");
                sb.AppendLine(" - If you find a suitable agent to handoff to, call transfer_to_{agentName} tool directly");
                sb.AppendLine(" - If there's no suitable agent to handoff to, call HandoffBack directly");
                //sb.AppendLine(" - **NEVER** tell the user you're going to handoff");
                //sb.AppendLine(" - **NEVER** tell the user what you are handing off for or why you are handing off");
                //sb.AppendLine(" - **NEVER** mention anything related to handoff in your notifyUserMessage");
                sb.AppendLine(" - Use transfer_to_{agentName} or HandoffBack if you are done solving an issue");
                prependedInstructions = true;
            }
        }
        else if (!string.IsNullOrEmpty(_currentAgent.UserPromptOverride))
        {
            _logger.LogInternalInformation("[{threadId}]Using UserPromptOverride from agent {agentName}",
                _context.ThreadId, _currentAgent.Name);
            sb.AppendLine(_currentAgent.UserPromptOverride);
            prependedInstructions = true;
        }

        if (prependedInstructions)
        {
            // if we added other user instructions, then we wanna mark where the user query goes
            sb.AppendLine(Markers.UserQuestionMarker);
            sb.AppendLine(chatMessage.Message.Text);
            return sb.ToString();
        }
        else
        {
            return chatMessage.Message.Text;
        }
    }

    private async Task ChangeAgentContextStateAsync(ContextStateEnum newState)
    {
        var oldState = _context.ContextState;
        try
        {
            if (oldState != newState)
            {
                _context = _context with { ContextState = newState };
                await _threadRepository.UpdateAgentContextAsync(_context);
                _logger.LogInternalInformation($"Changed agent context state from {oldState} to {newState}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[{threadId}]Failed to change agent context state from {oldState} to {newState}", _context.ThreadId, oldState, newState);
        }
    }

    /// <summary>
    /// Scans the chat history for function calls that don't have corresponding tool results
    /// and adds placeholder error messages for them. This prevents 400 errors from the API
    /// when previous tool calls were cancelled or didn't return output in time.
    /// </summary>
    private async Task AddPlaceholderResultsForOrphanedToolCallsAsync(AgentChatHistory agentChatHistory)
    {
        if (_chatHistory == null || _chatHistory.Count == 0)
        {
            return;
        }

        // Collect all function call IDs from assistant messages
        var functionCallIds = _chatHistory
            .Where(m => m.Role == ChatRole.Assistant)
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(fc => fc.CallId)
            .ToHashSet();

        // Collect all function result IDs from tool messages
        var functionResultIds = _chatHistory
            .Where(m => m.Role == ChatRole.Tool)
            .SelectMany(m => m.Contents.OfType<FunctionResultContent>())
            .Select(fr => fr.CallId)
            .ToHashSet();

        // Find orphaned function calls (calls without results)
        var orphanedCallIds = functionCallIds.Except(functionResultIds).ToList();

        if (orphanedCallIds.Count == 0)
        {
            return;
        }

        _logger.LogInternalWarning(
            "[{threadId}] Found {count} orphaned tool calls without results. Adding placeholder results to prevent API errors.",
            _context.ThreadId,
            orphanedCallIds.Count);

        // Get the actual FunctionCallContent objects for orphaned calls
        var orphanedCalls = _chatHistory
            .Where(m => m.Role == ChatRole.Assistant)
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Where(fc => orphanedCallIds.Contains(fc.CallId))
            .ToList();

        // Add placeholder tool result messages for each orphaned call
        var placeholderMessages = new List<ChatMessage>();
        foreach (var orphanedCall in orphanedCalls)
        {
            _logger.LogInternalInformation(
                "[{threadId}] Adding placeholder result for orphaned tool call: {toolName} (CallId: {callId})",
                _context.ThreadId,
                orphanedCall.Name,
                orphanedCall.CallId);

            var placeholderMessage = new ChatMessage(
                role: ChatRole.Tool,
                contents:
                [
                    new FunctionResultContent(orphanedCall.CallId, "Error: Tool execution was interrupted before completion (likely due to a system restart or unexpected termination). You may retry this operation. But if the same operation continues to fail multiple times, do not retry again—inform the user there was a system issue and consider an alternative approach.")
                ]
            );

            placeholderMessages.Add(placeholderMessage);
            _chatHistory.Add(placeholderMessage);
        }

        // Persist the placeholder messages
        if (placeholderMessages.Count > 0)
        {
            await PersistReasoningMessagesAsync(agentChatHistory, placeholderMessages);
        }
    }

    private async Task<ReasoningLoopIterationResult> RunInternalAsync(
        AgentChatHistory agentChatHistory,
        CancellationToken cancellationToken)
    {
        var runConfig = new RunConfig
        {
            ChatClient = _chatClientProvider.GeneralPurposeModel,
            LoggerFactory = _loggerFactory,
            EnableDebugOutput = _enableReasoningDebugOutput,
            ThreadId = _context.ThreadId,
            SkillRegistry = _skillRegistry,
            EnablePartialToolOutput = _featureConfig.PartialOutputEnabled,
            AmbientContextProvider = _ambientContextProvider,
            ChatClientProvider = _chatClientProvider,
            HookManager = _hookManager
        };

        List<UserActionRequiredResult> userActionRequiredResults = [];

        try
        {
            var runHooks = CreateRunHooks();

            Core.ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInternalInformation(
                "[{threadId}] Starting agent runner for agent {agentName}, isExtended: {isExtended}, skillsEnabled: {skillsEnabled}",
                _context.ThreadId,
                _currentAgent.Name,
                _currentAgent.IsExtended,
                _currentAgent.EnableSkills);

            // Fix any orphaned tool calls (function calls without corresponding tool results)
            // This prevents 400 errors from the API when previous tool calls were cancelled or didn't return output
            // await AddPlaceholderResultsForOrphanedToolCallsAsync(agentChatHistory);

            var runResult = await Runner.RunAsync(
                startingAgent: _currentAgent,
                input: _chatHistory!,
                config: runConfig,
                runtimeModifier: _agentRuntimeModifier,
                context: _context,
                hooks: runHooks,
                displayModelOutput: new ChatMessageOutput(_outboundCommunicationService, _streamingMessageRepository, _context, Guid.NewGuid()),
                activeSkills: GetActiveSkills(_currentAgent),
                toolOutputProcessService: _toolOutputProcessService,
                cancellationToken: cancellationToken
            );

            await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);

            _currentAgent = runResult.LastAgent;
            _context = _context with { CurrentAgent = _currentAgent.Name, ActiveSkills = [.. runResult.ActiveSkills.Select(s => s.Name)] };
            _context = await _threadRepository.UpdateAgentContextAsync(_context) ?? _context with { CurrentAgent = _currentAgent.Name, ActiveSkills = [.. runResult.ActiveSkills.Select(s => s.Name)] }; // avoid context is null

            // handle manual tool calls
            while (runResult.ManualToolCalls != null && runResult.ManualToolCalls.Count > 0)
            {
                _logger.LogInternalInformation("[{threadId}]Processing {toolCallCount} manual tool calls: {tools}", _context.ThreadId, runResult.ManualToolCalls.Count, string.Join(", ", runResult.ManualToolCalls.Select(tc => tc.Tool.Name)));

                var toolCallMessageId = Guid.NewGuid();

                await _outboundCommunicationService.AppendAgentManualToolCallMessage(
                    _context.ThreadId,
                    runResult.ManualToolCalls,
                    toolCallMessageId);

                List<ManualToolCallResult> toolResults = [];

                // Currently, multiple tools are executed seqentially. Because multiple tools may have have implicit dependencies on each other.
                // For example, 1st az cli tool restarts a container app while 2nd one checks the app's status.
                foreach (var toolCall in runResult.ManualToolCalls)
                {
                    // TODO: move handoff back to Agent.Framework so we don't have to manipulate the chat history so much outside the runner
                    if (toolCall.Tool.UnderlyingMethod?.Name == nameof(AgentControlFlowPluginDefinition.HandoffBack)
                        || toolCall.Tool.UnderlyingMethod?.Name == nameof(AgentReasoningControlFlowPluginDefinition.HandoffBack))
                    {
                        var handoffOutput = GetHandoffBackTransferMessage(toolCall);
                        if (_context.AgentHandoffChain.Count > 1)
                        {
                            // pop agent off the chain
                            _context.AgentHandoffChain.RemoveAt(_context.AgentHandoffChain.Count - 1);
                            var agentName = _context.AgentHandoffChain[^1];
                            var newAgent = _agentProvider.GetAgent(agentName, _context.ThreadId.ToString());

                            runResult = runResult.WithNewAgent(newAgent);
                        }
                        else
                        {
                            // Handoff to the default starting agent when no other agents are in the chain
                            runResult = runResult.WithNewAgent(_defaultStartingAgent);

                            // Update the context to reflect the handoff to default agent
                            _context = _context with
                            {
                                AgentHandoffChain = [_defaultStartingAgent.Name]
                            };
                        }

                        _context = await _threadRepository.UpdateAgentContextAsync(_context);
                        toolResults.Add(new ManualToolCallResult()
                        {
                            FunctionCall = toolCall.FunctionCall,
                            Output = handoffOutput
                        });
                    }
                    else if (toolCall.Tool.UnderlyingMethod?.Name == nameof(ViewImagePluginDefinition.ViewImage))
                    {
                        // Handle ViewImage tool - intercept and load image from thread storage
                        var viewImageResult = await HandleViewImageToolAsync(toolCall, cancellationToken);
                        toolResults.Add(viewImageResult);
                    }
                    else
                    {
                        var checkWriteActionResult = CheckWriteActionInReadOnlyMode(toolCall);
                        var currentAgentMode = _agentRuntimeModifier.GetThreadAgentMode(_context);
                        if (string.Equals(currentAgentMode, ActionMode.ReadOnly.ToString(), StringComparison.OrdinalIgnoreCase) &&
                           checkWriteActionResult.NeedSkip)
                        {
                            toolResults.Add(new ManualToolCallResult()
                            {
                                FunctionCall = toolCall.FunctionCall,
                                Output = checkWriteActionResult.Prompt
                            });

                        }
                        else
                        {
                            var checkApprovalResult = await CheckApprovalAsync(toolCall);
                            var shouldStop = checkApprovalResult.ApprovalStatus == ToolApprovalStatus.Pending;

                            if (checkApprovalResult.ApprovalStatus == ToolApprovalStatus.NotRequired || checkApprovalResult.ApprovalStatus == ToolApprovalStatus.AutoApproved)
                            {
                                try
                                {
                                    var functionResult = await InvokeToolWithErrorHandlingAsync(toolCall, cancellationToken);

                                    var isCliTool = CliTools.ContainsKey(toolCall.Tool.Name);

                                    CliToolExecutionResult? cliToolExecutionResult = null;

                                    var isValidCliToolResult = isCliTool && TryGetCliToolExecutionResult(functionResult, out cliToolExecutionResult);

                                    CliToolExecution? cliExecution = null;
                                    if (isValidCliToolResult && cliToolExecutionResult?.ExecutionId is not null)
                                    {
                                        cliExecution = await GetCliToolExecution(_context.ThreadId, CliTools[toolCall.Tool.Name], cliToolExecutionResult.ExecutionId.Value);
                                    }

                                    // Return immediately if not a CLI tool, or if result is null/invalid
                                    if (!isCliTool
                                        || cliExecution is null
                                        || !cliExecution.IsPending)
                                    {
                                        if (_featureConfig.PartialOutputEnabled)
                                        {
                                            // Unwrap cli tool output for better truncation handling
                                            object? result = isValidCliToolResult ? cliToolExecutionResult!.CliExecutionResult.Output : functionResult;
                                            var processedOutput = await _toolOutputProcessService.ProcessToolOutputAsync(
                                                _context.ThreadId,
                                                toolCall.Tool,
                                                toolCall.FunctionCall.CallId,
                                                result,
                                                cancellationToken);

                                            toolResults.Add(new ManualToolCallResult()
                                            {
                                                FunctionCall = toolCall.FunctionCall,
                                                Output = processedOutput
                                            });
                                        }
                                        else
                                        {
                                            toolResults.Add(new ManualToolCallResult()
                                            {
                                                FunctionCall = toolCall.FunctionCall,
                                                Output = functionResult
                                            });
                                        }
                                    }
                                    else
                                    {
                                        // For pending CLI tools, the tool starts execution asynchronously after the user clicks "Run" on UI
                                        // So there's no tool execution result is added to the chat history.
                                        var cliToolType = CliTools[toolCall.Tool.Name];

                                        if (cliExecution.AzCliExecution is not null)
                                        {
                                            var azCliExecution = cliExecution.AzCliExecution with
                                            {
                                                AgentContextId = _context.Id,
                                                OriginalFunctionCall = JsonSerializer.Serialize(toolCall.FunctionCall),
                                            };
                                            await _threadRepository.UpdateAzCliExecutionAsync(_context.ThreadId, azCliExecution);
                                            var contextWrapper = new RunContextWrapper<AgentContext>(_context);
                                            await runHooks.OnToolEnd(contextWrapper, _currentAgent, toolCall.FunctionCall, toolCall.Tool, functionResult);
                                        }
                                        else if (cliExecution.KubectlExecution is not null)
                                        {
                                            var kubectlExecution = cliExecution.KubectlExecution with
                                            {
                                                AgentContextId = _context.Id,
                                                OriginalFunctionCall = JsonSerializer.Serialize(toolCall.FunctionCall),
                                            };
                                            await _threadRepository.UpdateKubectlExecutionAsync(_context.ThreadId, kubectlExecution);
                                            var contextWrapper = new RunContextWrapper<AgentContext>(_context);
                                            await runHooks.OnToolEnd(contextWrapper, _currentAgent, toolCall.FunctionCall, toolCall.Tool, functionResult);
                                        }
                                        else if (cliExecution.PsqlExecution is not null)
                                        {
                                            var psqlExecution = cliExecution.PsqlExecution with
                                            {
                                                AgentContextId = _context.Id,
                                                OriginalFunctionCall = JsonSerializer.Serialize(toolCall.FunctionCall),
                                            };
                                            await _threadRepository.UpdatePsqlExecutionAsync(_context.ThreadId, psqlExecution);
                                            var contextWrapper = new RunContextWrapper<AgentContext>(_context);
                                            await runHooks.OnToolEnd(contextWrapper, _currentAgent, toolCall.FunctionCall, toolCall.Tool, functionResult);
                                        }
                                    }
                                }
                                catch (ToolExecutionUnauthorizedException ex)
                                {
                                    try
                                    {
                                        await HandleToolExecutionUnauthorized(ex, toolCall.Tool, toolCall.FunctionCall);
                                        shouldStop = true;
                                    }
                                    catch (Exception ex2)
                                    {
                                        toolResults.Add(new ManualToolCallResult()
                                        {
                                            FunctionCall = toolCall.FunctionCall,
                                            Output = GetErrorMessage(toolCall.FunctionCall, ex2),
                                        });
                                    }
                                }
                            }

                            if (shouldStop)
                            {
                                // Either it needs approval or authorization
                                await ChangeAgentContextStateAsync(ContextStateEnum.PendingApproval);
                                var contextWrapper = new RunContextWrapper<AgentContext>(_context);
                                var pendingApprovalMessage = "Tool execution is waiting for approval";
                                await runHooks.OnToolEnd(contextWrapper, _currentAgent, toolCall.FunctionCall, toolCall.Tool, pendingApprovalMessage);
                            }
                        }
                    }
                }

                if (toolResults.Count > 0)
                {
                    if (toolResults.Count == runResult.ManualToolCalls?.Count)
                    {
                        _logger.LogInternalInformation(
                            "[{threadId}] Resuming agent runner with agent {agentName}, isExtended: {isExtended}, skillsEnabled: {skillsEnabled}",
                            _context.ThreadId,
                            runResult.LastAgent.Name,
                            runResult.LastAgent.IsExtended,
                            runResult.LastAgent.EnableSkills);

                        // all tools executed, clear manual tool calls
                        runResult = await Runner.ResumeFromManualToolsAsync(
                            previousResult: runResult,
                            manualToolResults: toolResults,
                            config: runConfig,
                            context: _context,
                            hooks: runHooks,
                            displayModelOutput: new ChatMessageOutput(_outboundCommunicationService, _streamingMessageRepository, _context, Guid.NewGuid()),
                            toolOutputProcessService: _toolOutputProcessService,
                            cancellationToken: cancellationToken
                        );

                        await _outboundCommunicationService.AppendAgentManualToolCallResult(
                            _context.ThreadId,
                            toolResults,
                            toolCallMessageId);

                        await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);
                        _currentAgent = runResult.LastAgent;
                        _context = _context with { CurrentAgent = _currentAgent.Name, ActiveSkills = [.. runResult.ActiveSkills.Select(s => s.Name)] };
                        _context = await _threadRepository.UpdateAgentContextAsync(_context);
                    }
                    else
                    {
                        _logger.LogInternalInformation("[{threadId}]Not all manual tool calls have results. executed {count} out of {total}", _context.ThreadId, toolResults.Count, runResult.ManualToolCalls?.Count);

                        await _outboundCommunicationService.AppendAgentManualToolCallResult(
                            _context.ThreadId,
                            toolResults,
                            toolCallMessageId);

                        var toolResultMessages = toolResults.Select(tr => new ChatMessage(
                            role: ChatRole.Tool,
                            contents:
                            [
                                new FunctionResultContent(tr.FunctionCall.CallId, tr.Output)
                            ]
                        )).ToList();

                        await PersistReasoningMessagesAsync(agentChatHistory, toolResultMessages);

                        // Because we couldn't resume the runner yet, we need to break out of the manual tool call processing loop
                        // Orphaned tool calls will be handled by AddPlaceholderResultsForOrphanedToolCallsAsync on the next iteration
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            var endingState = AgentProcessingState.Unknown;
            if (runResult.Output != null)
            {
                if (runResult.Output is IAgentOutput agentOutput)
                {
                    endingState = Enum.TryParse<AgentProcessingState>(agentOutput.State, out var parsed)
                        ? parsed
                        : AgentProcessingState.Unknown;

                    var needsHandOff = endingState == AgentProcessingState.HandOff_OutOfScope
                        || endingState == AgentProcessingState.HandOff_Continue;

                    var needsReiteration = endingState == AgentProcessingState.Processing;

                    if (needsHandOff)
                    {
                        _logger.LogInternalInformation("Agent determined the request is out of scope. Handoff back");

                        if (_context.AgentHandoffChain.Count > 1)
                        {
                            _logger.LogInternalInformation("Agent set handoff state without handoff tool call. AgentHandoffChain has more agents, asking agent to do the right handoff.");

                            ChatMessage userPromptMessage;
                            if (endingState == AgentProcessingState.HandOff_Continue)
                            {
                                userPromptMessage = new ChatMessage(ChatRole.User,
                                    "You mentioned the request is in state HandOff_Continue, but did not actually perform any handoffs (transfer_to_*). " +
                                    "Reflect if any more processing work is required *based on your responsibility*. If yes, set the state to Processing and continue taking actions in your scope. " +
                                    "Otherwise if you are actually done, then call the right handoff tool.");
                            }
                            else
                            {
                                userPromptMessage = new ChatMessage(ChatRole.User,
                                    $"You mentioned the request is in state HandOff_OutOfScope, but did not actually perform any handoffs (transfer_to_* or HandOffBack). " +
                                    $"Reflect if any more processing work is required. If yes, set the state to {AgentProcessingState.Processing} and continue taking actions in your scope. " +
                                    $"Otherwise if you are actually done, then call the right handoff tool.");
                            }

                            await PersistReasoningMessageAsync(agentChatHistory, userPromptMessage);

                            return new ReasoningLoopIterationResult
                            {
                                IsContinuation = true,
                                UserActionRequiredResults = userActionRequiredResults
                            };
                        }
                        else
                        {
                            _logger.LogInternalInformation("AgentHandoffChain is empty or has only one agent, asking agent to seek user help.");

                            var reloopPromptMessage = new ChatMessage(ChatRole.User,
                                "It seems you are stuck. Briefly mention to user what you are trying to solve, what you did so far and where you need guidance.");
                            await PersistReasoningMessageAsync(agentChatHistory, reloopPromptMessage);
                        }

                        return new ReasoningLoopIterationResult
                        {
                            IsContinuation = true,
                            UserActionRequiredResults = userActionRequiredResults
                        };
                    }
                    else if (needsReiteration)
                    {
                        _logger.LogInternalInformation("Asking {subAgentName} agent to continue action...", _currentAgent.Name);

                        var userPromptMessage = new ChatMessage(ChatRole.User, $"You mentioned request is {AgentProcessingState.Processing}. " +
                            $"Continue taking actions to complete the request.");
                        await PersistReasoningMessageAsync(agentChatHistory, userPromptMessage);

                        return new ReasoningLoopIterationResult
                        {
                            IsContinuation = true,
                            UserActionRequiredResults = userActionRequiredResults
                        };
                    }
                }

                // auto handoff to our default starting agent, if previous turn was completed successfully
                if (_autoHandOffEnabled
                    && endingState == AgentProcessingState.CompletedSuccessfully)
                {
                    _logger.LogInternalInformation("Autohandoff is enabled. Resetting control to {startAgent}. Previous ending agent: {lastAgent}", _defaultStartingAgent.Name, _currentAgent.Name);

                    // todo: add a user message to show handoff path to previous agent
                    // if(_currentAgent.Name != _defaultStartingAgent.Name)

                    _currentAgent = _defaultStartingAgent;
                    // clear the handoff chain
                    _context = _context with
                    {
                        CurrentAgent = _currentAgent.Name,
                        AgentHandoffChain = [_currentAgent.Name]
                    };
                    _context = await _threadRepository.UpdateAgentContextAsync(_context);
                }

                await ChangeAgentContextStateAsync(ContextStateEnum.Idle);
            }

            _logger.LogInternalInformation("Reasoning loop iteration completed.");
        }
        catch (OperationCanceledException ex)
        {
            _currentException = ex;
            var parentSpan = _currentAgentSpan ?? _rootSpan;
            _tracer.RecordErrorSpan(ex, $"Operation canceled: {ex.GetType()}: {ex.Message}", _context.ThreadId.ToString(), parentSpan);
            throw;
        }
        catch (TurnLimitReachedException<AgentContext> ex)
        {
            _currentException = ex;
            var parentSpan = _currentAgentSpan ?? _rootSpan;
            _tracer.RecordErrorSpan(ex, $"Turn limit reached: {ex.GetType()}: {ex.Message}", _context.ThreadId.ToString(), parentSpan);

            _logger.LogInternalWarning("[{threadId}]Turn limit reached.", _context.ThreadId, ex);

            // generate progress summary

            var result = ex.RunResult;

            await PersistReasoningMessagesAsync(agentChatHistory, result.NewItems);

            var progressSummaryAgent = _agentProvider.GetAgent("progress_summary_agent", _context.ThreadId.ToString());

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
        catch (RateLimitExceededException ex)
        {
            _currentException = ex;
            var parentSpan = _currentAgentSpan ?? _rootSpan;
            var errorSpan = _tracer.StartActiveSpan("error", SpanKind.Internal, parentSpan);
            errorSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
            errorSpan.SetAttribute(TraceAttribute.OperationName, "error");
            errorSpan.SetAttribute("error.message", $"Model Rate-limit exceeded: {ex.GetType()}: {ex.Message}");
            errorSpan.SetAttribute("error.stacktrace", ex.StackTrace);
            errorSpan.End();

            _logger.LogInternalWarning(ex, "[{threadId}]Rate limit encountered during reasoning loop.", _context.ThreadId);

            var retryAfterMessage = ex.RetryAfterSeconds.HasValue
                ? $"{ex.RetryAfterSeconds.Value} seconds"
                : "a few moments";
            var errorMessage = string.Format(Agent.Core.Constants.ErrorMessages.RateLimitExceeded, retryAfterMessage);
            var message = new ChatMessage(ChatRole.Assistant, errorMessage);

            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                _context,
                message
            );
        }
        catch (System.ClientModel.ClientResultException ex)
            when (RateLimitChatClient.IsRateLimit(ex))
        {
            _currentException = ex;
            var parentSpan = _currentAgentSpan ?? _rootSpan;
            _tracer.RecordErrorSpan(ex, $"Model Rate-limit exceeded: {ex.GetType()}: {ex.Message}", _context.ThreadId.ToString(), parentSpan);

            _logger.LogInternalWarning(ex, "[{threadId}]Rate limit encountered during reasoning loop.", _context.ThreadId);
            var message = new ChatMessage(ChatRole.Assistant, Agent.Core.Constants.ErrorMessages.RateLimitExceeded);
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                _context,
                message
            );
        }
        catch (System.ClientModel.ClientResultException ex)
            when (ex.Status == 400
                && (ex.Message?.Contains("content_filter", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            _currentException = ex;
            var parentSpan = _currentAgentSpan ?? _rootSpan;
            _tracer.RecordErrorSpan(ex, $"Content filter triggered: {ex.GetType()}: {ex.Message}", _context.ThreadId.ToString(), parentSpan);

            _logger.LogInternalWarning(ex, "[{threadId}]Content filter triggered during reasoning loop.", _context.ThreadId);
            var message = new ChatMessage(ChatRole.Assistant, Agent.Core.Constants.ErrorMessages.ContentFilterTriggered);
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                _context,
                message
            );
        }
        catch (Exception ex)
        {
            _currentException = ex;
            var parentSpan = _currentAgentSpan ?? _rootSpan;
            _tracer.RecordErrorSpan(ex, $"{ex.GetType()}: {ex.Message}", _context.ThreadId.ToString(), parentSpan);

            _logger.LogInternalError(ex, "[{threadId}]An error occurred during reasoning loop.", _context.ThreadId);
            var message = new ChatMessage(ChatRole.Assistant, "I am unable to fully address your request due to an internal error. Please retry to continue the conversation!");
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                _context,
                message
            );
        }
        finally
        {
            // Ensure _currentGenerationSpan is always closed with appropriate error context
            if (_currentGenerationSpan != null)
            {
                if (_currentException != null)
                {
                    // We're in an exception context - add specific error details
                    _currentGenerationSpan.SetAttribute("error.message", $"{_currentException.GetType()}: {_currentException.Message}");
                    _currentGenerationSpan.SetAttribute("error.type", "exception");
                    _currentGenerationSpan.SetAttribute("completion.status", "failed");
                }
                else
                {
                    // Normal completion but span wasn't closed - likely interrupted
                    _currentGenerationSpan.SetAttribute("completion.status", "interrupted");
                }

                _currentGenerationSpan.End();
                _currentGenerationSpan = null;
            }

            // Reset exception state for next iteration
            _currentException = null;

            _currentAgentSpan?.End();
            _currentAgentSpan = null;
        }

        return new ReasoningLoopIterationResult
        {
            IsContinuation = false,
            UserActionRequiredResults = userActionRequiredResults
        };
    }

    private static string GetHandoffBackTransferMessage(ManualToolCall toolCall)
    {
        var handoffReasoning = string.Empty;
        if (toolCall.FunctionCall.Arguments is not null
            && toolCall.FunctionCall.Arguments.TryGetValue(AgentReasoningControlFlowPluginDefinition.ReasoningParam, out var reasoningObject)
            && reasoningObject is JsonElement reasoningElement
            && reasoningElement.ValueKind == JsonValueKind.String)
        {
            handoffReasoning = reasoningElement.GetString()!;
        }

        return Handoff<AgentContext>.GetTransferMessage(handoffReasoning);
    }

    /// <summary>
    /// Handles the ViewImage tool by downloading the image from thread storage and creating
    /// a ManualToolCallResult with the image injected as additional messages.
    /// </summary>
    private async Task<ManualToolCallResult> HandleViewImageToolAsync(ManualToolCall toolCall, CancellationToken cancellationToken)
    {
        string? fileName = null;

        // Extract fileName from tool arguments
        if (toolCall.FunctionCall.Arguments is not null
            && toolCall.FunctionCall.Arguments.TryGetValue("fileName", out var fileNameObject))
        {
            if (fileNameObject is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
            {
                fileName = jsonElement.GetString();
            }
            else if (fileNameObject is string fileNameStr)
            {
                fileName = fileNameStr;
            }
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new ManualToolCallResult()
            {
                FunctionCall = toolCall.FunctionCall,
                Output = "Error: No fileName parameter provided to ViewImage tool."
            };
        }

        try
        {
            // Download the image from thread storage
            var filePath = await _agentFileStorageService.DownloadThreadFileAsync(
                _context.ThreadId,
                fileName,
                cancellationToken);

            if (filePath == null)
            {
                return new ManualToolCallResult()
                {
                    FunctionCall = toolCall.FunctionCall,
                    Output = $"Error: File '{fileName}' not found in thread storage."
                };
            }

            // Read the image bytes
            var imageBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var mimeType = GetImageMimeType(fileName);

            // Create a user message with the image content
            var imageContent = new List<AIContent>
            {
                new TextContent($"Image '{fileName}':"),
                new DataContent(imageBytes, mimeType)
            };

            var imageMessage = new ChatMessage(ChatRole.User, imageContent);

            return new ManualToolCallResult()
            {
                FunctionCall = toolCall.FunctionCall,
                Output = $"Image '{fileName}' loaded.",
                AdditionalMessages = [imageMessage]
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to load image '{FileName}' for ViewImage tool", fileName);
            return new ManualToolCallResult()
            {
                FunctionCall = toolCall.FunctionCall,
                Output = $"Error: Failed to load image '{fileName}': {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets the MIME type for an image based on its file extension.
    /// </summary>
    private static string GetImageMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream" // Default for unknown types
        };
    }

    // private Task DisplayModelResponse(string t, ModelOutputType responseType)
    // {
    //     if (responseType == ModelOutputType.IntermediateOutput || responseType == ModelOutputType.Debug || responseType == ModelOutputType.ReasoningSummary)
    //     {
    //         return _outboundCommunicationService.NotifyIntermediateUpdate(
    //             _context.ThreadId,
    //             t);
    //     }

    //     return _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
    //         _context,
    //         new ChatMessage(ChatRole.Assistant, t));
    // }

    private RunHooks<AgentContext> CreateRunHooks()
    {
        var hooks = new RunHooks<AgentContext>();

        hooks.CompactionStart += async (context, agent) =>
        {
            _logger.LogInternalInformation("Trace starting Compaction for agent: {subAgentName}.", agent.Name);
            _currentCompactionSpan = _tracer.StartSpan($"compaction", SpanKind.Internal, _currentAgentSpan);
            _currentCompactionSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
            _currentCompactionSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
            _currentCompactionSpan.SetAttribute(TraceAttribute.OperationName, "Compaction");

            // Stream compaction feedback to the frontend with shimmer effect
            var compactionFunctionCall = new FunctionCallContent(Guid.NewGuid().ToString(), "CompactConversation");
            await _outboundCommunicationService.AppendAgentToolCallMessage(
                _context.ThreadId,
                compactionFunctionCall);
        };

        hooks.CompactionEnd += (context, agent) =>
        {
            _logger.LogInternalInformation("Trace ending Compaction for agent: {subAgentName}.", agent.Name);
            _currentCompactionSpan?.End();
            _currentCompactionSpan = null;
            return Task.CompletedTask;
        };

        hooks.ResolveFactoryTools += async (context, agent, additionalToolNames) =>
        {
            List<AIFunction> tools = [];
            List<string> missingTools = [];

            List<string> allToolNames = [.. agent.FactoryTools, .. additionalToolNames];

            foreach (var toolName in allToolNames.Distinct())
            {
                // Skip disabled tools (those that don't meet EnabledIf condition)
                if (_toolFactory.IsToolDisabled(toolName))
                {
                    _logger.LogInternalDebug("Skipping disabled tool {toolName} for agent {agentName}", toolName, agent.Name);
                    continue;
                }

                if (!_toolFactory.HasTool(toolName))
                {
                    missingTools.Add(toolName);
                    _logger.LogInternalWarning(
                        "ReasoningLoop: Tool '{ToolName}' not found for agent '{AgentName}', ThreadId: {ThreadId}. Continuing without this tool.",
                        toolName, agent.Name, _context.ThreadId);
                    continue;
                }

                var tool = _toolFactory.GetTool(toolName, _context.ThreadId, agent);
                tools.Add(tool);
            }

            if (missingTools.Count != 0)
            {
                _logger.LogInternalWarning(
                    "ReasoningLoop: Agent '{AgentName}' has missing tools for ThreadId: {ThreadId}. MissingTools: {MissingTools}. Continuing with {AvailableToolCount} available tools.",
                    agent.Name, _context.ThreadId, string.Join(", ", missingTools), tools.Count);

                if (!_warnedMissingToolsPerSubAgent.TryGetValue(agent.Name, out var warnedTools))
                {
                    warnedTools = new HashSet<string>();
                    _warnedMissingToolsPerSubAgent[agent.Name] = warnedTools;
                }

                var newMissingTools = missingTools.Where(tool => !warnedTools.Contains(tool)).ToList();
                if (newMissingTools.Count != 0)
                {
                    // we warn only once per missing tool per subagent
                    foreach (var tool in newMissingTools)
                    {
                        warnedTools.Add(tool);
                    }

                    var warningMessage = $"Configuration Warning: the following tool(s) are not available: {string.Join(", ", newMissingTools)}. I will continue helping you without these tool(s). '{agent.Name}' may have been configured with tool(s) that have since been removed or disabled. Please remove any references to these tool(s).";

                    await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                        _context.ThreadId,
                        new ChatMessage(ChatRole.Assistant, warningMessage)
                    );
                }
            }

            return [.. tools.DistinctBy(t => t.Name)];
        };

        hooks.AgentStart += (context, agent) =>
        {
            _currentAgentSpan?.End();
            _currentAgentSpan = null;

            _logger.LogInternalInformation("Trace invoke agent: {subAgentName}", agent.Name);
            _currentAgentSpan = _tracer.StartActiveSpan($"invoke.agent.{agent.Name}", SpanKind.Internal, _rootSpan);
            _currentAgentSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
            _currentAgentSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
            _currentAgentSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.InvokeAgent);

            var parameterObj = new { agentName = agent.Name, isExtended = agent.IsExtended };
            _logger.LogAgentAction(
                action: AgentActionEvents.InvokeAgent,
                parameter: WebJsonSerializer.Serialize(parameterObj),
                status: AgentActionStatus.Success,
                duration: 0,
                threadId: _context.ThreadId.ToString(),
                subAgentName: agent.Name,
                featureConfig: WebJsonSerializer.Serialize(_featureConfig),
                activeExperiments: WebJsonSerializer.Serialize(_agentProvider.GetActiveVariants(_context.ThreadId.ToString())));
            return Task.CompletedTask;
        };

        hooks.AgentEnd += (context, agent, output) =>
        {
            _logger.LogInternalInformation("Trace Ending agent: {subAgentName}", agent.Name);
            _currentAgentSpan?.End();
            _currentAgentSpan = null;
            return Task.CompletedTask;
        };

        hooks.Handoff += async (context, agent, handoffAgent, handoffReasoning) =>
        {
            _logger.LogInternalInformation("Trace Handoff from agent: {subAgentName} to agent: {HandoffAgentName}", agent.Name, handoffAgent.Name);
            var currentToolSpan = _tracer.StartSpan($"handoff", SpanKind.Internal, _currentAgentSpan);
            currentToolSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
            currentToolSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Handoff);
            currentToolSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
            currentToolSpan.SetAttribute(TraceAttribute.HandoffAgentName, handoffAgent.Name);
            currentToolSpan.SetAttribute(TraceAttribute.HandoffReasoning, handoffReasoning);
            currentToolSpan.End();

            _currentAgentSpan?.End();
            _context.AgentHandoffChain.Add(handoffAgent.Name);
            _context = await _threadRepository.UpdateAgentContextAsync(_context);
        };

        hooks.ToolStart += async (context, agent, functionCall, tool, input) =>
        {
            _logger.LogInternalInformation("Trace Starting tool: {ToolName} for agent: {subAgentName}", tool.Name, agent.Name);
            var currentToolSpan = _tracer.StartActiveSpan($"tool.{tool.Name}", SpanKind.Internal, _currentAgentSpan);
            currentToolSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
            currentToolSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Tool);
            currentToolSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
            currentToolSpan.SetAttribute(TraceAttribute.ToolName, tool.Name);
            currentToolSpan.SetAttribute(TraceAttribute.ToolInput, FormatToolArguments(input));
            currentToolSpan.SetAttribute(TraceAttribute.ModelTemperature, agent.Temperature.ToString());
            currentToolSpan.SetAttribute(TraceAttribute.ToolDescription, tool.Description);

            _toolSpans[functionCall.CallId] = currentToolSpan;

            _logger.LogAgentAction(
                action: AgentActionEvents.InvokeTool,
                parameter: tool.Name,
                status: AgentActionStatus.Success,
                duration: 0,
                threadId: _context.ThreadId.ToString(),
                subAgentName: agent.Name,
                featureConfig: WebJsonSerializer.Serialize(_featureConfig),
                activeExperiments: WebJsonSerializer.Serialize(_agentProvider.GetActiveVariants(_context.ThreadId.ToString())));

            // Store todo arguments if this is a ToDoWrite tool
            if (tool.Name == ToDoWriteTool<AgentContext>.ToolName)
            {
                _currentTodoArguments = input;
            }

            // Stream auto tools to avoid missing them (manual tools are handled separately)
            if (tool.GetToolMode() == ToolMode.Auto)
            {
                var callId = ToolStatic.AsyncLocalFunctionCallId.Value;
                if (!string.IsNullOrEmpty(callId))
                {
                    _logger.LogInternalInformation("Streaming auto tool call: {ToolName} with CallId: {CallId}", tool.Name, callId);
                    var toolCallMessageId = Guid.NewGuid();
                    await _outboundCommunicationService.AppendAgentToolCallMessage(_context.ThreadId, functionCall, toolCallMessageId);
                    // Store the message ID for OnToolEnd to use
                    Framework.ToolStatic.AsyncLocalToolCallMessageId.Value = toolCallMessageId;
                }
            }

            // Stream MCP tool execution for special UI display
            if (tool.IsMcpTool())
            {
                var mcpExecution = McpToolExecutionHelper.CreateFromFunctionCall(functionCall);
                var mcpMessageId = Guid.NewGuid(); // Use a dedicated message ID for MCP execution
                await _outboundCommunicationService.NotifyMcpToolExecution(_context.ThreadId, mcpExecution, mcpMessageId);
                // Store execution with message ID for ToolEnd to update status
                _mcpExecutions[functionCall.CallId] = (mcpExecution, mcpMessageId);
            }
        };

        hooks.ToolEnd += async (context, agent, functionCallContent, tool, output) =>
        {
            _logger.LogInternalInformation("Trace Ending tool: {ToolName} for agent: {subAgentName}", tool.Name, agent.Name);
            var currentToolSpan = _toolSpans.GetValueOrDefault(functionCallContent.CallId);
            currentToolSpan?.SetAttribute(TraceAttribute.ToolOutput, output?.ToString() ?? string.Empty);
            currentToolSpan?.End();

            _toolSpans.Remove(functionCallContent.CallId, out var _);

            // Stream auto tool results to complete the streaming flow
            if (tool.GetToolMode() == ToolMode.Auto)
            {
                var callId = Framework.ToolStatic.AsyncLocalFunctionCallId.Value;
                var toolCallMessageId = Framework.ToolStatic.AsyncLocalToolCallMessageId.Value;
                if (!string.IsNullOrEmpty(callId) && toolCallMessageId.HasValue)
                {
                    _logger.LogInternalInformation("Streaming auto tool result: {ToolName} with CallId: {CallId}", tool.Name, callId);
                    var result = new FunctionResultContent(callId, output);
                    await _outboundCommunicationService.AppendAgentToolCallResult(_context.ThreadId, result, toolCallMessageId.Value);
                    // Clear the stored IDs for next tool
                    Framework.ToolStatic.AsyncLocalFunctionCallId.Value = null;
                    Framework.ToolStatic.AsyncLocalToolCallMessageId.Value = null;
                }
            }

            // Update MCP tool execution status to completed
            if (_mcpExecutions.TryRemove(functionCallContent.CallId, out var mcpData))
            {
                var (mcpExecution, mcpMessageId) = mcpData;
                mcpExecution.Status = McpToolExecutionStatus.Completed;
                mcpExecution.CompletedAt = DateTime.UtcNow;
                mcpExecution.Result = output?.ToString();
                await _outboundCommunicationService.NotifyMcpToolExecution(_context.ThreadId, mcpExecution, mcpMessageId);
            }

            LogToolExecution(tool, output);

            // Handle todo plan persistence and streaming if this was a ToDoWrite tool
            if (tool.Name == ToDoWriteTool<AgentContext>.ToolName && _currentTodoArguments != null)
            {
                var currentTodoArgument = _currentTodoArguments;
                _ = Task.Run(async () => await ProcessTodoPersistenceAndStreamingAsync(currentTodoArgument, _context.ThreadId));
                _currentTodoArguments = null; // Clear for next tool
            }
        };

        hooks.ModelGenerationStart += (context, agent, messages, chatOptions) =>
        {
            _logger.LogInternalInformation("Trace Starting model generation for agent: {subAgentName}", agent.Name);
            _currentGenerationSpan = _tracer.StartActiveSpan($"model_generation", SpanKind.Internal, _currentAgentSpan);

            // start timing the model generation
            try
            {
                _currentGenerationStopwatch = Stopwatch.StartNew();
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to start generation stopwatch");
                _currentGenerationStopwatch = null;
            }

            _currentGenerationSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
            _currentGenerationSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
            _currentGenerationSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.ModelGeneration);
            _currentGenerationSpan.SetAttribute(TraceAttribute.ModelInput, FormatChatMessages(messages));
            _currentGenerationSpan.SetAttribute(TraceAttribute.ModelTools, FormatToolsByType(chatOptions.Tools));
            _currentGenerationSpan.SetAttribute(TraceAttribute.ModelHandoffs, FormatHandoffs(chatOptions.Tools));
            _currentGenerationSpan.SetAttribute(TraceAttribute.ModelAgentAsTools, FormatAgentAsTools(chatOptions.Tools));
            _currentGenerationSpan.SetAttribute(TraceAttribute.ModelPromptStarters, FormatPromptStarters(agent.Instructions));
            _currentGenerationSpan.SetAttribute(TraceAttribute.ModelAgentPrompt, FormatAgentPrompt(agent.Instructions));
            _currentGenerationSpan.SetAttribute(TraceAttribute.ModelCommonPrompts, FormatCommonPrompts(agent.Instructions));
            _currentGenerationSpan.SetAttribute(TraceAttribute.ModelPromptEnders, FormatPromptEnders(agent.Instructions));
            _currentGenerationSpan.SetAttribute(TraceAttribute.ModelResultSummarizationPrompt, FormatResultSummarizationPrompt(agent.ResultSummarizationPrompt));
            return Task.CompletedTask;
        };

        hooks.ModelGenerationEnd += (context, agent, response) =>
        {
            _logger.LogInternalInformation("Trace Ending model generation for agent: {subAgentName}", agent?.Name ?? "Unknown");
            _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelOutput, FormatChatMessages(response?.Messages ?? []));
            _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelInputTokensCount, response?.Usage?.InputTokenCount?.ToString() ?? string.Empty);
            _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelOutputTokensCount, response?.Usage?.OutputTokenCount?.ToString() ?? string.Empty);
            _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelTotalTokensCount, response?.Usage?.TotalTokenCount?.ToString() ?? string.Empty);
            _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelTemperature, agent?.Temperature.ToString() ?? string.Empty);
            _currentGenerationSpan?.SetAttribute(TraceAttribute.ModelId, response?.ModelId?.ToString() ?? string.Empty);

            // stop the stopwatch and capture duration (ms)
            long durationMs = 0;

            try
            {
                if (_currentGenerationStopwatch != null)
                {
                    _currentGenerationStopwatch.Stop();
                    durationMs = (long)_currentGenerationStopwatch.Elapsed.TotalMilliseconds;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to stop/read generation stopwatch");
                durationMs = 0;
            }

            _currentGenerationSpan?.End();
            _currentGenerationSpan = null;
            _currentGenerationStopwatch = null;

            // Build token usage JSON including cached token count if available
            var cachedTokenCount = response?.Usage?.CachedInputTokenCount ?? 0L;

            // Build token usage JSON including cached token count if available
            var reasoningTokenCount = response?.Usage?.ReasoningTokenCount ?? 0L;

            // get the effective reasoning effort for the request
            var effectiveReasoningEffort = ReasoningConstants.NonReasoningModel;
            if (ChatOptionsExtensions.IsReasoningModel(response?.ModelId))
            {
                effectiveReasoningEffort = string.IsNullOrEmpty(agent?.ReasoningEffortLevel)
                    ? ChatOptionsExtensions.DefaultReasoningEffort
                    : agent.ReasoningEffortLevel;
            }

            var tokenUsageObj = new TokenUsageParameters(
                InputTokenCount: response?.Usage?.InputTokenCount ?? 0,
                OutputTokenCount: response?.Usage?.OutputTokenCount ?? 0,
                CachedTokenCount: cachedTokenCount,
                ReasoningTokenCount: reasoningTokenCount,
                ReasoningEffort: effectiveReasoningEffort
            );

            _logger.LogAgentAction(
                action: AgentActionEvents.GenerateModelResponse,
                parameter: response?.ModelId?.ToString() ?? string.Empty,
                status: response is not null ? AgentActionStatus.Success : AgentActionStatus.Fail,
                duration: durationMs,
                threadId: _context.ThreadId.ToString(),
                subAgentName: agent?.Name ?? "Unknown",
                inputToken: response?.Usage?.InputTokenCount ?? 0,
                outputToken: response?.Usage?.OutputTokenCount ?? 0,
                cachedToken: cachedTokenCount,
                reasoningToken: reasoningTokenCount,
                featureConfig: WebJsonSerializer.Serialize(_featureConfig),
                actionMetadata: WebJsonSerializer.Serialize(tokenUsageObj),
                activeExperiments: WebJsonSerializer.Serialize(_agentProvider.GetActiveVariants(_context.ThreadId.ToString())));

            return Task.CompletedTask;
        };

        hooks.SummarizerStart += (context, agent) =>
        {
            _logger.LogInternalInformation("Trace starting Summarizer for agent: {subAgentName}.", agent.Name);
            _currentSummarizerSpan = _tracer.StartSpan($"summarizer", SpanKind.Internal, _currentAgentSpan);
            _currentSummarizerSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
            _currentSummarizerSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
            _currentSummarizerSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Summarizer);
            return Task.CompletedTask;
        };

        hooks.SummarizerEnd += (context, agent, extractedUserIntent) =>
        {
            _logger.LogInternalInformation("Trace ending Summarizer for agent: {subAgentName}.", agent.Name);
            _currentSummarizerSpan?.SetAttribute("summarizer.extracted_user_query", extractedUserIntent);
            _currentSummarizerSpan?.End();
            _currentSummarizerSpan = null;
            return Task.CompletedTask;
        };

        hooks.CriticStart += (context, agent, currentTurn) =>
        {
            var maxTurns = agent.MaxReflectionCount;
            _logger.LogInternalInformation("Trace starting Critic for agent: {subAgentName}. Turn# {CurrentTurn}/{MaxTurns}", agent.Name, currentTurn, maxTurns);
            _currentCriticSpan = _tracer.StartSpan($"critic", SpanKind.Internal, _currentAgentSpan);
            _currentCriticSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
            _currentCriticSpan.SetAttribute(TraceAttribute.AgentName, agent.Name);
            _currentCriticSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Critic);
            _currentCriticSpan.SetAttribute("critic.turn_index", currentTurn.ToString());
            _currentCriticSpan.SetAttribute("critic.max_turns", maxTurns.ToString());
            _currentCriticSpan.SetAttribute("critic.reflection_note", agent.CustomReflectionNote);
            return Task.CompletedTask;
        };

        hooks.CriticEnd += (context, agent, userQuery, criticResult, wasApproved) =>
        {
            _logger.LogInternalInformation("Trace ending Critic for agent: {subAgentName}, Approved: {WasApproved}", agent.Name, wasApproved);
            _currentCriticSpan?.SetAttribute("critic.user_query", userQuery);
            _currentCriticSpan?.SetAttribute("critic.result", criticResult);
            _currentCriticSpan?.SetAttribute("critic.was_approved", wasApproved.ToString());
            _currentCriticSpan?.End();
            _currentCriticSpan = null;

            _logger.LogAgentAction(
                action: AgentActionEvents.CriticEvaluation,
                parameter: wasApproved ? "Approved" : "Failed",
                status: AgentActionStatus.Success,
                duration: 0,
                threadId: _context.ThreadId.ToString(),
                subAgentName: agent.Name,
                featureConfig: WebJsonSerializer.Serialize(_featureConfig),
                activeExperiments: WebJsonSerializer.Serialize(_agentProvider.GetActiveVariants(_context.ThreadId.ToString())));
            return Task.CompletedTask;
        };

        // Add CustomerLogger hooks for telemetry (first-party check)
        if (FirstPartyHelper.IsFirstPartyTenant())
        {
            var customerLoggerHelper = new CustomerLoggerHelper(
                _customerLogger,
                _context.ThreadId.ToString(),
                "ReasoningLoop",
                _tracer
            );
            var customerLoggerHooks = customerLoggerHelper.GetCustomerLoggerHooks();

            // Subscribe CustomerLogger hooks to main hooks
            hooks.ToolStart += customerLoggerHooks.OnToolStart;
            hooks.ToolEnd += customerLoggerHooks.OnToolEnd;
            hooks.AgentStart += customerLoggerHooks.OnAgentStart;
            hooks.AgentEnd += customerLoggerHooks.OnAgentEnd;
            hooks.Handoff += customerLoggerHooks.OnHandoff;
            hooks.ModelGenerationStart += customerLoggerHooks.OnModelGenerationStart;
            hooks.ModelGenerationEnd += customerLoggerHooks.OnModelGenerationEnd;
            hooks.ModelGenerationError += customerLoggerHooks.OnModelGenerationError;
        }

        // Add Task tool (parallel subagent) streaming hooks
        var taskToolStreamingHelper = new TaskToolStreamingHelper(_outboundCommunicationService, _context.ThreadId);
        taskToolStreamingHelper.SubscribeTo(hooks);

        return hooks;
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

    public sealed record TokenUsageParameters(
        long InputTokenCount,
        long OutputTokenCount,
        long CachedTokenCount,
        long ReasoningTokenCount,
        string ReasoningEffort);

    public sealed record ExecutionResult(
        bool RequiresApproval,
        string AgentMode,
        ToolMode ToolMode,
        bool IsWriteAction,
        string? Failure);

    // Unified helper to log tool executions
    private void LogToolExecution(AIFunction aiTool, object? result)
    {
        if (aiTool == null)
        {
            return;
        }

        // Some tools require approval even if they don't have the RequiresApproval attribute
        var requiresApprovalByName = string.Equals(aiTool.Name, "RunKubectlWriteCommand", StringComparison.OrdinalIgnoreCase)
            || string.Equals(aiTool.Name, "RunAzCliWriteCommands", StringComparison.OrdinalIgnoreCase);

        var requireApproval = requiresApprovalByName || (aiTool.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>() != null);

        // Determine if the tool is marked as a write action
        var isWriteAction = false;
        try
        {
            var writeAttr = aiTool.UnderlyingMethod?.GetCustomAttribute<WriteActionAttribute>();
            if (writeAttr != null)
            {
                isWriteAction = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to inspect WriteAction attribute for tool {ToolName}", aiTool.Name);
        }

        // Determine status using similar logic as CalculateToolCallMetrics
        var executionStatus = DetermineToolExecutionStatus(aiTool.Name, result);

        var executionObj = new ExecutionResult(
            RequiresApproval: requireApproval,
            AgentMode: _agentRuntimeModifier.GetThreadAgentMode(_context),
            ToolMode: aiTool.GetToolMode(),
            IsWriteAction: isWriteAction,
            Failure: executionStatus.Failure);

        try
        {
            _logger.LogAgentAction(
                action: AgentActionEvents.ToolExecution,
                parameter: aiTool.Name,
                status: executionStatus.ActionResult,
                duration: 0,
                threadId: _context.ThreadId.ToString(),
                subAgentName: _currentAgent?.Name ?? string.Empty,
                inputToken: 0,
                outputToken: 0,
                threadSource: string.Empty,
                featureConfig: WebJsonSerializer.Serialize(_featureConfig),
                actionMetadata: WebJsonSerializer.Serialize(executionObj),
                activeExperiments: WebJsonSerializer.Serialize(_agentProvider.GetActiveVariants(_context.ThreadId.ToString())));
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to emit LogAgentAction for ToolExecution");
        }
    }

    private enum CliToolType
    {
        AzCli,
        Kubectl,
        Psql
    }

    private static readonly IReadOnlyDictionary<string, CliToolType> CliTools = new Dictionary<string, CliToolType>
    {
        [EvaluationHelper.GetToolCallName(nameof(ArmPluginDefinition.RunAzCliReadCommandsAsync))] = CliToolType.AzCli,
        [EvaluationHelper.GetToolCallName(nameof(ArmPluginDefinition.RunAzCliWriteCommandsAsync))] = CliToolType.AzCli,
        [EvaluationHelper.GetToolCallName(nameof(KubePluginDefinition.RunKubectlReadCommandAsync))] = CliToolType.Kubectl,
        [EvaluationHelper.GetToolCallName(nameof(KubePluginDefinition.RunKubectlWriteCommandAsync))] = CliToolType.Kubectl,
        [EvaluationHelper.GetToolCallName(nameof(PostgreSQLAutomationPluginDefinition.RunPsqlReadCommandAsync))] = CliToolType.Psql
    };

    // Helper method to determine tool execution status using similar logic as CalculateToolCallMetrics
    private static (string ActionResult, string? Failure) DetermineToolExecutionStatus(string toolName, object? result)
    {
        // Convert result to string for analysis
        var output = result?.ToString() ?? string.Empty;

        // Check for basic "Error: Function" pattern
        if (output.StartsWith("Error: Function", StringComparison.OrdinalIgnoreCase))
        {
            return (AgentActionStatus.Fail, output);
        }

        var normalizedToolName = EvaluationHelper.GetToolCallName(toolName);
        // Check AzCli & Kubectl tools
        if (CliTools.ContainsKey(normalizedToolName)
            && output.Contains(ExternalProcessCommand.ProcessFailureMessage, StringComparison.OrdinalIgnoreCase))
        {
            return (AgentActionStatus.Fail, output);
        }

        return (AgentActionStatus.Success, null);
    }

    private async Task ExecuteToolAsync(
        AgentChatHistory agentChatHistory,
        AIFunction aiTool,
        FunctionCallContent functionCall,
        CancellationToken cancellationToken)
    {
        // Set the cancellation token for plugins to use
        Agent.Core.ToolStatic.AsyncLocalCancellationToken.Value = cancellationToken;
        var toolCallMessageId = Guid.NewGuid();

        // Create a span for this tool execution
        var toolSpan = _tracer.StartActiveSpan($"tool.{aiTool.Name}", SpanKind.Internal, _currentAgentSpan);
        toolSpan.SetAttribute(TraceAttribute.ThreadId, _context.ThreadId.ToString());
        toolSpan.SetAttribute(TraceAttribute.OperationName, TraceOperationName.Tool);
        toolSpan.SetAttribute(TraceAttribute.AgentName, _currentAgent.Name);
        toolSpan.SetAttribute(TraceAttribute.ToolName, aiTool.Name);
        toolSpan.SetAttribute(TraceAttribute.ToolInput, FormatToolArguments(functionCall.Arguments));
        toolSpan.SetAttribute(TraceAttribute.ToolDescription, aiTool.Description);

        try
        {
            await _outboundCommunicationService.AppendAgentToolCallMessage(_context.ThreadId, functionCall, toolCallMessageId);
            var functionResult = await aiTool.InvokeAsync(new AIFunctionArguments(functionCall.Arguments), cancellationToken);

            FunctionResultContent result;
            if (_featureConfig.PartialOutputEnabled)
            {
                var processedOutput = await _toolOutputProcessService.ProcessToolOutputAsync(_context.ThreadId, aiTool, functionCall.CallId, functionResult, cancellationToken);
                result = new FunctionResultContent(functionCall.CallId, processedOutput);
            }
            else
            {
                result = new FunctionResultContent(functionCall.CallId, functionResult);
            }
            var functionCallMessage = new ChatMessage(ChatRole.Tool, [result]);
            // Set the tool output in the span
            toolSpan.SetAttribute(TraceAttribute.ToolOutput, functionResult?.ToString() ?? string.Empty);

            var outboundResult = new FunctionResultContent(functionCall.CallId, functionResult);
            await _outboundCommunicationService.AppendAgentToolCallResult(_context.ThreadId, outboundResult, toolCallMessageId);
            await PersistReasoningMessageAsync(agentChatHistory, functionCallMessage);
        }
        finally
        {
            toolSpan.End();
        }
    }

    private async Task<bool> ExecuteToolWithOboFlowFallbackAsync(
        AgentChatHistory agentChatHistory,
        AIFunction aiTool,
        FunctionCallContent functionCall,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await ExecuteToolAsync(agentChatHistory, aiTool, functionCall, cancellationToken);
            return false;
        }
        catch (ToolExecutionUnauthorizedException ex)
        {
            await HandleToolExecutionUnauthorized(ex, aiTool, functionCall);
            return true;
        }
    }

    private async Task<bool> HandleUnprocessedToolCallsAsync(AgentChatHistory agentChatHistory, CancellationToken cancellationToken)
    {
        var lastMessage = _chatHistory?.LastOrDefault();
        // Check if lastMessage exists and has contents before accessing First()
        var lastContent = lastMessage?.Contents?.FirstOrDefault();

        if (agentChatHistory == null)
        {
            _logger.LogInternalError("[{threadId}] AgentChatHistory is null", _context.ThreadId);
            throw new InvalidOperationException("AgentChatHistory is null");
        }

        // if lastContent is a tool call, we need to invoke the tool first
        if (lastContent != null && lastContent is FunctionCallContent functionCall)
        {
            try
            {
                var aiTool = ResolveTool(functionCall.Name) ?? throw new Exception($"Tool {functionCall.Name} not found");

                return await ExecuteToolWithOboFlowFallbackAsync(agentChatHistory, aiTool, functionCall, cancellationToken);
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

        var skills = GetActiveSkills(_currentAgent);

        if (_currentAgent.StandardToolNames.Contains(name))
        {
            tool = _currentAgent.Tools.FirstOrDefault(aiTool => aiTool.Name == name);
        }
        else if (_currentAgent.FactoryTools.Contains(name) || skills.AllToolNames.Contains(name))
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
        var lastMessage = _chatHistory?.LastOrDefault()?.Contents?.Last();
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

            switch (approval.Status)
            {
                case ApprovalDecision.Pending:
                case ApprovalDecision.PendingAuthorization:
                    _logger.LogInternalInformation($"Approval {approval.Id} is {approval.Status}. Waiting for user to respond.");
                    return true; // Wait for user to approve or reject
                case ApprovalDecision.Approved:
                    {
                        _logger.LogInternalInformation($"Approval {approval.Id} is approved. Executing tool: {functionCall.Name}");
                        try
                        {
                            var aiTool = ResolveTool(functionCall.Name) ?? throw new Exception($"Tool {functionCall.Name} not found");
                            return await ExecuteToolWithOboFlowFallbackAsync(agentChatHistory, aiTool, functionCall, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalError(ex, "Error while invoking tool: {ToolName}", functionCall.Name);
                            var errorMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(functionCall.CallId, GetErrorMessage(functionCall, ex))]);
                            await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
                        }
                        finally
                        {
                            await RemovePendingApprovalAsync(approval.Id);
                        }

                        return false;
                    }
                case ApprovalDecision.Authorized:
                    {
                        _logger.LogInternalInformation($"Approval {approval.Id} is authorized by user. Executing tool with obo token: {functionCall.Name}");
                        try
                        {
                            var aiTool = ResolveTool(functionCall.Name) ?? throw new Exception($"Tool {functionCall.Name} not found");
                            var approvalContext = new ApprovalContext(
                                                    ThreadId: _context.ThreadId,
                                                    ApprovalId: approval.Id,
                                                    UseOboToken: true
                                                );

                            Core.ToolStatic.AsyncLocalApprovalContext.Value = approvalContext;
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
                            await RemovePendingApprovalAsync(approval.Id);
                        }

                        return false;
                    }
                case ApprovalDecision.Cancelled:
                    {
                        _logger.LogInternalInformation($"Approval {approval.Id} is cancelled by user.");
                        var result = new FunctionResultContent(functionCall.CallId, "Error: Function failed, user cancelled the function call.");
                        var functionCallMessage = new ChatMessage(ChatRole.Tool, [result]);
                        await PersistReasoningMessageAsync(agentChatHistory, functionCallMessage);

                        await RemovePendingApprovalAsync(approval.Id);
                        return false;
                    }
                default:
                    _logger.LogInternalWarning($"Approval {approval.Id}  Unknown approval status: {approval.Status}");
                    return true; // Unknown status, block the loop
            }
        }

        return false;
    }

    // remove pending approval
    private async Task RemovePendingApprovalAsync(Guid approvalId)
    {
        var pendingApprovals = _context.ApprovalInformation?.PendingApprovals;
        if (pendingApprovals != null && pendingApprovals.Contains(approvalId))
        {
            pendingApprovals.Remove(approvalId);
            _context = _context with
            {
                ApprovalInformation = new ApprovalInformation(pendingApprovals),
            };
            await ChangeAgentContextStateAsync(ContextStateEnum.Processing);
        }
    }

    /// <summary>
    /// Cancels all pending approvals and CLI executions, and adds tool result messages to chat history.
    /// This is called when a new user message comes in while there are pending approvals or CLI executions.
    /// </summary>
    private async Task CancelAllPendingApprovalsAndCliExecutionsAsync(AgentChatHistory agentChatHistory)
    {
        var toolResultMessages = new List<ChatMessage>();

        // Build a map of approval titles to function calls from chat history
        var functionCallsByTitle = new Dictionary<string, FunctionCallContent>();
        if (_chatHistory != null)
        {
            foreach (var message in _chatHistory)
            {
                foreach (var content in message.Contents)
                {
                    if (content is FunctionCallContent functionCall)
                    {
                        var title = GetApprovalTitle(functionCall);
                        functionCallsByTitle[title] = functionCall;
                    }
                }
            }
        }

        // Cancel pending approvals
        var pendingApprovalIds = _context.ApprovalInformation?.PendingApprovals?.ToList();
        if (pendingApprovalIds != null && pendingApprovalIds.Count > 0)
        {
            foreach (var approvalId in pendingApprovalIds)
            {
                var approval = await _threadRepository.GetApprovalAsync(_context.ThreadId, approvalId);
                if (approval == null)
                {
                    _logger.LogInternalWarning("[{threadId}]Approval {approvalId} not found when trying to cancel.", _context.ThreadId, approvalId);
                    continue;
                }

                // Update approval status to Cancelled
                var cancelledApproval = approval with
                {
                    Status = ApprovalDecision.Cancelled,
                    DecisionTimestamp = DateTime.UtcNow
                };
                await _threadRepository.UpdateApprovalAsync(cancelledApproval);
                await _outboundCommunicationService.NotifyApprovalUpdate(_context.ThreadId, cancelledApproval);
                _logger.LogInternalInformation("[{threadId}]Cancelled approval {approvalId} ({title}) due to new user message.",
                    _context.ThreadId, approvalId, approval.Title);

                // Find the corresponding function call and create a tool result message
                if (functionCallsByTitle.TryGetValue(approval.Title, out var functionCall))
                {
                    var result = new FunctionResultContent(functionCall.CallId,
                        "User cancelled the execution and sent a new message. Reflect on the new context.");
                    toolResultMessages.Add(new ChatMessage(ChatRole.Tool, [result]));
                }
            }

            // Clear all pending approvals from context
            _context = _context with
            {
                ApprovalInformation = new ApprovalInformation([]),
            };
        }

        // Cancel pending CLI executions
        var (azCliExecution, kubectlExecution, psqlExecution) = await ListPendingExecutions(_context.ThreadId);

        if (azCliExecution != null)
        {
            var cancelledExecution = azCliExecution with
            {
                Status = AzCliExecutionStatus.Cancelled,
                CompletedTimestamp = DateTime.UtcNow
            };
            await _threadRepository.UpdateAzCliExecutionAsync(_context.ThreadId, cancelledExecution);
            await _outboundCommunicationService.NotifyAzCliUpdate(_context.ThreadId, cancelledExecution);
            _logger.LogInternalInformation("[{threadId}]Cancelled AzCli execution {executionId} due to new user message.",
                _context.ThreadId, azCliExecution.Id);

            // Add tool result message if we have the original function call
            if (!string.IsNullOrEmpty(azCliExecution.OriginalFunctionCall))
            {
                var functionCall = JsonSerializer.Deserialize<FunctionCallContent>(azCliExecution.OriginalFunctionCall);
                if (functionCall != null)
                {
                    var result = new FunctionResultContent(functionCall.CallId,
                        "User cancelled the execution and sent a new message. Reflect on the new context.");
                    toolResultMessages.Add(new ChatMessage(ChatRole.Tool, [result]));
                }
            }
        }

        if (kubectlExecution != null)
        {
            var cancelledExecution = kubectlExecution with
            {
                Status = KubectlExecutionStatus.Cancelled,
                CompletedTimestamp = DateTime.UtcNow
            };
            await _threadRepository.UpdateKubectlExecutionAsync(_context.ThreadId, cancelledExecution);
            await _outboundCommunicationService.NotifyKubectlUpdate(_context.ThreadId, cancelledExecution);
            _logger.LogInternalInformation("[{threadId}]Cancelled Kubectl execution {executionId} due to new user message.",
                _context.ThreadId, kubectlExecution.Id);

            // Add tool result message if we have the original function call
            if (!string.IsNullOrEmpty(kubectlExecution.OriginalFunctionCall))
            {
                var functionCall = JsonSerializer.Deserialize<FunctionCallContent>(kubectlExecution.OriginalFunctionCall);
                if (functionCall != null)
                {
                    var result = new FunctionResultContent(functionCall.CallId,
                        "User cancelled the execution and sent a new message. Reflect on the new context.");
                    toolResultMessages.Add(new ChatMessage(ChatRole.Tool, [result]));
                }
            }
        }

        // I just have no idea why psqlExecution totally reuses the az cli execution document
        // which means we cannot distinct them here. So, in theory, pending psql execution should have been already handled above.
        // if (psqlExecution != null)
        // {
        //     var cancelledExecution = psqlExecution with
        //     {
        //         Status = AzCliExecutionStatus.Cancelled,
        //         CompletedTimestamp = DateTime.UtcNow
        //     };
        //     await _threadRepository.UpdatePsqlExecutionAsync(_context.ThreadId, cancelledExecution);
        //     await _outboundCommunicationService.NotifyPsqlUpdate(_context.ThreadId, cancelledExecution);
        //     _logger.LogInternalInformation("[{threadId}]Cancelled Psql execution {executionId} due to new user message.",
        //         _context.ThreadId, psqlExecution.Id);

        //     // Add tool result message if we have the original function call
        //     if (!string.IsNullOrEmpty(psqlExecution.OriginalFunctionCall))
        //     {
        //         var functionCall = JsonSerializer.Deserialize<FunctionCallContent>(psqlExecution.OriginalFunctionCall);
        //         if (functionCall != null)
        //         {
        //             var result = new FunctionResultContent(functionCall.CallId,
        //                 "User cancelled the execution and sent a new message. Reflect on the new context.");
        //             toolResultMessages.Add(new ChatMessage(ChatRole.Tool, [result]));
        //         }
        //     }
        // }

        // Persist all tool result messages
        if (toolResultMessages.Count > 0)
        {
            await PersistReasoningMessagesAsync(agentChatHistory, toolResultMessages);
        }

        await ChangeAgentContextStateAsync(ContextStateEnum.Processing);
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
            var currentAgentMode = _agentRuntimeModifier.GetThreadAgentMode(_context);
            _logger.LogInternalInformation($"Checking approval for tool {toolCall.Tool.Name}. Current agent mode: {currentAgentMode}");
            if (string.Compare(currentAgentMode, ActionMode.Autonomous.ToString(), StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.AutoApproved,
                };
            }

            var approvalTitle = GetApprovalTitle(toolCall.FunctionCall);
            var description = attribute.DisplayMessage ?? toolCall.Tool.Name;
            // Always create a new approval
            var newApproval = await CreateAndPersistApproval(
                approvalTitle: approvalTitle,
                description: description);

            return new CheckApprovalActivityOutput()
            {
                ApprovalId = newApproval.Id,
                ApprovalStatus = ToolApprovalStatus.Pending,
            };
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

    private async Task HandleToolExecutionUnauthorized(ToolExecutionUnauthorizedException ex, AIFunction aiTool, FunctionCallContent functionCall)
    {
        var attr = aiTool.UnderlyingMethod?.GetCustomAttribute<OboContextAttribute>() ?? new OboContextAttribute();
        if (attr.DisableObo)
        {
            _logger.LogInternalInformation($"Tool {aiTool.Name} does not support obo flow. Throw original exception.");
            throw ex.InnerException ?? ex;
        }

        _logger.LogInternalInformation($"Trigger obo flow for tool {aiTool.Name}.");
        var title = GetApprovalTitle(functionCall);
        await CreateAndPersistApproval(title, ex.CustomDescription ?? aiTool.Name, attr.Scope, ApprovalDecision.PendingAuthorization);
    }

    private async Task<Approval> CreateAndPersistApproval(
        string approvalTitle,
        string description,
        string? oboScope = null,
        ApprovalDecision status = ApprovalDecision.Pending
    )
    {
        var approval = new Approval(
            Id: Guid.NewGuid(),
            ThreadId: _context.ThreadId.ToString(),
            Title: approvalTitle,
            Description: description,
            Status: status,
            CreatedTimestamp: DateTime.UtcNow,
            DecisionTimestamp: null,
            OrchestrationId: null,
            AgentContextId: _context.Id,
            DecisionUser: null,
            OboToken: null,
            OboTokenScope: oboScope);

        await _threadRepository.CreateApprovalAsync(approval);

        var newPendingApprovals = _context.ApprovalInformation?.PendingApprovals ?? [];
        newPendingApprovals.Add(approval.Id);

        _context = _context with
        {
            ApprovalInformation = new ApprovalInformation(newPendingApprovals),
            ContextState = ContextStateEnum.PendingApproval
        };

        _context = await _threadRepository.UpdateAgentContextAsync(_context);

        await _outboundCommunicationService.AppendAgentApprovalMessage(
            _context.ThreadId,
            approval);

        _logger.LogInternalInformation("Created new approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status ToolApprovalStatus.Pending", approval.Id, _context.ThreadId, approval.Title);
        return approval;
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

        RecordUserActionResults(chatMessage);
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

        RecordUserActionResults(chatMessages);
    }

    private void RecordUserActionResults(params IEnumerable<ChatMessage> chatMessages)
    {
        if (LastIterationResult == null || LastIterationResult.AreUserActionsCompleted)
        {
            return;
        }

        foreach (var toolResult in chatMessages.SelectMany(m => m.Contents.OfType<FunctionResultContent>()))
        {
            LastIterationResult.SetResultForCallId(toolResult.CallId, toolResult);
        }
    }

    private async Task HandleRememberCommandAsync(AgentChatHistory agentChatHistory, string userMessage, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation($"[{_context.ThreadId}] Processing {RememberMarker} command.");

            await PersistReasoningMessageAsync(agentChatHistory, new ChatMessage(ChatRole.User, userMessage));

            // Extract the user message/content after '#remember'
            var rememberIndex = userMessage.IndexOf(RememberMarker, StringComparison.OrdinalIgnoreCase);
            var memoryContent = userMessage.Substring(rememberIndex + RememberMarker.Length).Trim();

            if (string.IsNullOrWhiteSpace(memoryContent))
            {
                var errorMessage = new ChatMessage(ChatRole.Assistant, $"Please provide some content after {RememberMarker}. For example: '{RememberMarker} my container app is not working looks like there is an issue with the NSG rules.'");

                await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, errorMessage);
                return;
            }

            var memoryId = $"memory_{_context.ThreadId}_{DateTime.UtcNow.Ticks}";

            var vector = await _chatClientProvider.EmbeddingModel.GenerateVectorForAgentMemoryAsync(memoryContent, _logger, cancellationToken);

            var memory = AgentMemory.FromUserMemory(
                id: memoryId,
                memoryContent: memoryContent,
                embedding: [.. vector.Span]
            );

            var success = await _searchIndexService.IndexContentAsync(memory);

            ChatMessage responseMessage;
            if (success)
            {
                responseMessage = new ChatMessage(ChatRole.Assistant, "✅ Agent Memory saved.");
                _logger.LogInternalInformation("[{threadId}]Successfully stored user memory: {MemoryContent}", _context.ThreadId, memoryContent);
            }
            else
            {
                responseMessage = new ChatMessage(ChatRole.Assistant, "Failed to save memory. Please try again.");
                _logger.LogInternalError("[{threadId}]Failed to store user memory: {MemoryContent}", _context.ThreadId, memoryContent);
            }

            await PersistReasoningMessageAsync(agentChatHistory, responseMessage);

            // Send response to user
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, responseMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[{_context.ThreadId}]Error processing {RememberMarker} command");

            var errorMessage = new ChatMessage(ChatRole.Assistant, "Error saving memory. Please try again.");

            await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, errorMessage);
        }
    }

    private async Task HandleForgetCommandAsync(AgentChatHistory agentChatHistory, string userMessage, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation($"[{_context.ThreadId}] Processing {ForgetMarker} command.");

            await PersistReasoningMessageAsync(agentChatHistory, new ChatMessage(ChatRole.User, userMessage));

            var forgetIndex = userMessage.IndexOf(ForgetMarker, StringComparison.OrdinalIgnoreCase);
            var query = userMessage.Substring(forgetIndex + ForgetMarker.Length).Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                var errorMessage = new ChatMessage(ChatRole.Assistant, $"Please provide what to forget after {ForgetMarker}. For example: '{ForgetMarker} my preferences about coffee'");

                await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, errorMessage);
                return;
            }

            var memories = await _agentMemoryClient.SearchUserMemoriesAsync(
                new SearchParams(
                    Query: query,
                    K: 1,
                    EnableHybridSearch: true,
                    EnableSemanticSearch: true,
                    VectorSimilarityThreshold: _agentMemorySettings.UserMemoryVectorSimilarityThreshold),
                cancellationToken: cancellationToken);

            if (memories.Count == 0)
            {
                var noResultsMessage = new ChatMessage(ChatRole.Assistant, "No memories found for your query.");

                await PersistReasoningMessageAsync(agentChatHistory, noResultsMessage);
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, noResultsMessage);
                return;
            }

            var deleted = await _searchIndexService.DeleteContentsAsync([.. memories.Select(m => new AgentMemory() { Id = m.Id })]);

            var responseText = deleted ? "✅ Agent Memory forgotten: " + memories.First().Chunk : "Failed to forget memory. Please try again.";

            var responseMessage = new ChatMessage(ChatRole.Assistant, responseText);

            await PersistReasoningMessageAsync(agentChatHistory, responseMessage);
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, responseMessage);

            _logger.LogInternalInformation($"[{_context.ThreadId}] Successfully processed {ForgetMarker} command with {memories.Count} memories");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[{_context.ThreadId}] Error processing {ForgetMarker} command");

            var errorMessage = new ChatMessage(ChatRole.Assistant, "Failed to forget memory. Please try again.");

            await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, errorMessage);
        }
    }

    private async Task HandleRetrieveCommandAsync(AgentChatHistory agentChatHistory, string userMessage, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation($"[{_context.ThreadId}] Processing {RetrieveMarker} command.");

            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.System, "You are an AI assistant. Use the provided memories to answer the user's query in the context of the recent conversation. If the memories don't contain relevant information, say so.")
            };

            // extract messages before the user message is persisted
            // recent chat history for context (last 5 user/assistant text messages)
            // todo: pass in a summary of the complete chat instead.
            var recentMessages = _chatHistory?
                .Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant)
                .Select(ExtractUsefulText)
                .Where(m => m is not null)
                .Select(m => m!)
                .TakeLast(5)
                .ToList() ?? [];
            chatMessages.AddRange(recentMessages);

            await PersistReasoningMessageAsync(agentChatHistory, new ChatMessage(ChatRole.User, userMessage));

            var retrieveIndex = userMessage.IndexOf(RetrieveMarker, StringComparison.OrdinalIgnoreCase);
            var query = userMessage.Substring(retrieveIndex + RetrieveMarker.Length).Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                var errorMessage = new ChatMessage(ChatRole.Assistant, $"Please provide a query after {RetrieveMarker}. For example: '{RetrieveMarker} my preferences about coffee'");

                await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, errorMessage);
                return;
            }

            var memories = await _agentMemoryClient.SearchUserMemoriesAsync(new SearchParams(
                Query: query,
                K: 5,
                EnableHybridSearch: true,
                EnableSemanticSearch: true,
                VectorSimilarityThreshold: _agentMemorySettings.UserMemoryVectorSimilarityThreshold));

            if (memories.Count == 0)
            {
                var noResultsMessage = new ChatMessage(ChatRole.Assistant, "No memories found for your query.");

                await PersistReasoningMessageAsync(agentChatHistory, noResultsMessage);
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, noResultsMessage);
                return;
            }

            var memoryContext = new StringBuilder();
            memoryContext.AppendLine("Retrieved memories:");
            foreach (var memory in memories.Take(5))
            {
                memoryContext.AppendLine($"- {memory.Chunk}");
            }

            var prompt = $"Based on the following retrieved memories, please answer the user's query: '{query}'\n\n{memoryContext}";

            chatMessages.Add(new(ChatRole.User, prompt));

            var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(chatMessages, cancellationToken: cancellationToken);
            var responseText = response.GetMessage().Text ?? "I couldn't generate a response from your memories.";

            var responseMessage = new ChatMessage(ChatRole.Assistant, responseText);

            await PersistReasoningMessageAsync(agentChatHistory, responseMessage);
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, responseMessage);

            _logger.LogInternalInformation($"[{_context.ThreadId}] Successfully processed {RetrieveMarker} command with {memories.Count} memories");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[{_context.ThreadId}] Error processing {RetrieveMarker} command");

            var errorMessage = new ChatMessage(ChatRole.Assistant, "Error retrieving memories. Please try again.");

            await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, errorMessage);
        }
    }

    private async Task HandleCompactCommandAsync(string userMessage, CancellationToken cancellationToken)
    {
        // Extract any additional instructions from the user message
        var compactIndex = userMessage.IndexOf(CompactMarker, StringComparison.OrdinalIgnoreCase);
        var compactAdditionalInstructions = userMessage.Substring(compactIndex + CompactMarker.Length).Trim();
        await CompactChatAsync(
            compactInstructions: compactAdditionalInstructions,
            compactReason: CompactReason.UserCommand,
            notifyUser: true,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Compacts the chat history by summarizing the conversation.
    /// This updates both the database and the in-memory cache.
    /// </summary>
    /// <param name="compactInstructions">Optional additional instructions for the compaction summarizer.</param>
    /// <param name="notifyUser">Whether to send a notification message to the user. Default is true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task CompactChatAsync(
        string compactInstructions,
        CompactReason compactReason,
        bool notifyUser = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInternalWarning($"[{_context.ThreadId}] Initiating chat compaction. Reason: {compactReason}.");
        var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
        if (agentChatHistory == null)
        {
            _logger.LogInternalError("[{threadId}] No chat history found for agent context {agentContextId}", _context.ThreadId, _context.Id);
            return;
        }

        try
        {
            _logger.LogInternalInformation("[{ThreadId}] Processing compaction. Reason: {CompactReason}", _context.ThreadId, compactReason);

            // Build full chat trajectory BEFORE compaction and save to file for later reference
            var chatTrajectory = new AgentTrajectory(_defaultStartingAgent.Name, _autoHandOffEnabled, _logger);
            foreach (var msg in _chatHistory!)
            {
                chatTrajectory.Append(msg);
            }
            var fullChatTranscript = chatTrajectory.GetFullTrajectory();

            // Save full chat history as a file so the model can reference it if needed
            string? chatHistoryFileKey = null;
            try
            {
                chatHistoryFileKey = await _agentFileStorageService.SaveToolOutputAsync(
                    threadId: _context.ThreadId,
                    toolName: "compaction_history",
                    callId: DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),
                    content: fullChatTranscript,
                    extension: "txt",
                    cancellationToken: cancellationToken);

                _logger.LogInternalInformation(
                    "[{ThreadId}] Saved pre-compaction chat history to file {FileKey} ({Length} characters)",
                    _context.ThreadId, chatHistoryFileKey, fullChatTranscript.Length);
            }
            catch (Exception ex)
            {
                // Log but don't fail compaction if file storage fails
                _logger.LogInternalWarning(ex, "[{ThreadId}] Failed to save pre-compaction chat history to file, continuing without file reference", _context.ThreadId);
            }

            // call LLM to get the compacted chat
            var compactedChat = await Summarizer.CompactChatHistoryAsync(
                additionalInstructions: compactInstructions,
                chatHistory: _chatHistory!,
                startingAgent: _defaultStartingAgent.Name,
                autoHandOffEnabled: _autoHandOffEnabled,
                chatClient: _chatClientProvider.GeneralPurposeModel,
                logger: _logger);

            // Append file reference to the summary if file was saved successfully
            if (!string.IsNullOrEmpty(chatHistoryFileKey))
            {
                var fileReferenceSection = $$"""

                    ---
                    **Previous Conversation Archive**
                    The full conversation before this compaction has been stored.
                    > Use the `ToolOutputRetriever` tool to access the previous conversation if you need to reference specific details not captured in this summary.

                    ```json
                    {
                      "fileKey": "{{chatHistoryFileKey}}",
                      "contentLength": {{fullChatTranscript.Length}},
                      "format": "text",
                      "structure": {
                        "userMessage": "Role: user\n<message_text>",
                        "agentResponse": "Role: <agent_name>\n<response_text>",
                        "functionCall": "Role: <agent_name>\nFunction Call: <name>\nParameters: <json>",
                        "toolResult": "CallId: <id>\nResult: <output>",
                        "internalReasoning": "Role: <agent_name>\nInternal Reasoning: <text>"
                      }
                    }
                    ```
                    """;
                compactedChat += fileReferenceSection;
            }

            // modify chat history
            var compactedChatMessage = new ChatMessage(ChatRole.User, compactedChat);
            await PersistReasoningMessageAsync(agentChatHistory, compactedChatMessage);

            _logger.LogInternalInformation($"[{_context.ThreadId}] Successfully compacted chat history");

            if (notifyUser)
            {
                // Send response to user
                var responseMessage = new ChatMessage(ChatRole.Assistant, "✅ This conversation is now in compact mode.");
                await PersistReasoningMessageAsync(agentChatHistory, responseMessage);
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, responseMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[{_context.ThreadId}] Error processing compaction");

            if (notifyUser)
            {
                var errorMessage = new ChatMessage(ChatRole.Assistant, "Error compacting chat history. Please try again.");
                await PersistReasoningMessageAsync(agentChatHistory, errorMessage);
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, errorMessage);
            }
        }
    }

    private async Task HandleIncidentRetroModeCommandAsync()
    {
        try
        {
            // Only enabled for Development environment or 1P tenants
            if (!_hostEnvironment.IsDevelopment() && !FirstPartyHelper.IsFirstPartyTenant())
            {
                _logger.LogInternalInformation($"[{_context.ThreadId}] {IncidentRetroModeMarker} command rejected - not a first-party tenant.");
                var rejectMessage = new ChatMessage(ChatRole.Assistant, "This command is not available.");
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, rejectMessage);
                return;
            }

            _logger.LogInternalInformation($"[{_context.ThreadId}] Processing {IncidentRetroModeMarker} command.");

            bool currentState;
            if (_context.IsIncidentTestModeEnabled.HasValue)
            {
                currentState = _context.IsIncidentTestModeEnabled.Value;
            }
            else
            {
                var thread = await _threadRepository.GetThreadAsync(_context.ThreadId);
                currentState = thread?.IsIncidentTestModeEnabled ?? false;
            }
            var newState = !currentState;

            await _threadRepository.UpdateThreadIncidentTestModeAsync(_context.ThreadId, newState);
            _context = _context with { IsIncidentTestModeEnabled = newState };
            ThreadContextAccessor.SetThreadContext(_context);

            var userMessage = new ChatMessage(ChatRole.Assistant,
                newState
                    ? "🔍 Incident retro mode enabled. ICM discussion entries will be filtered for the agent to show only the alerting entries."
                    : "Incident retro mode disabled. Agent will receive all ICM discussion entries including its own past posts.");
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, userMessage);

            _logger.LogInternalInformation($"[{_context.ThreadId}] Incident retro mode toggled to: {newState}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[{_context.ThreadId}] Error processing {IncidentRetroModeMarker} command");

            var errorMessage = new ChatMessage(ChatRole.Assistant, "Error toggling incident retro mode. Please try again.");
            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(_context, errorMessage);
        }
    }

    private static ChatMessage? ExtractUsefulText(ChatMessage m)
    {
        if (m.Role == ChatRole.Assistant)
        {
            var textContents = m.Contents.Where(c => c is TextContent).ToList();
            if (textContents.Count == 0)
            {
                return null;
            }
            return new ChatMessage(m.Role, textContents);
        }
        else if (m.Role == ChatRole.User)
        {
            return new ChatMessage(m.Role, Summarizer.ExtractUserQuestion(m.Text));
        }
        else
        {
            return m;
        }
    }

    private async Task<object?> InvokeToolWithErrorHandlingAsync(ManualToolCall toolCall, CancellationToken cancellationToken)
    {
        try
        {
            Core.ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;
            Core.ToolStatic.AsyncLocalCancellationToken.Value = cancellationToken;
            Core.ToolStatic.AsyncLocalToolTraceSpan.Value = _toolSpans.GetValueOrDefault(toolCall.FunctionCall.CallId);

            return await toolCall.Tool.InvokeAsync(new AIFunctionArguments(toolCall.FunctionCall.Arguments), cancellationToken);
        }
        catch (ToolExecutionUnauthorizedException)
        {
            // throw exception to trigger obo flow
            throw;
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

            var prompt = $"You are in read-only mode. You MUST NOT perform any write action. You should ONLY provide suggestions to user for what to do next. " +
            "Please format your suggestions in a user-friendly way:\n" +
            "- If suggesting CLI commands (like az cli, kubectl, shell scripts, etc.), format them using markdown ```shell code blocks``` for easy copy-paste\n" +
            "- If the command is accurate and ready to use, tell the user they can copy and paste it directly\n" +
            "- Provide clear explanations of what each suggested action will do\n" +
            "- Use bullet points or numbered lists to organize multiple suggestions\n" +
            "- Always wait for user confirmation before proceeding\n" +
            "- Only proceed with next steps if user explicitly tells you the actions have been taken.";

            prompt += $"\nThe suggestion is to call Function '{toolCall.FunctionCall.Name}' with arguments: {toolCall.FunctionCall.GetSerializedArguments()}. " +
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
        for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
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
                    Value = FormatContentValue(content)
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

    /// <summary>
    /// Formats a content value for logging, excluding sensitive properties like ProtectedData from TextReasoningContent.
    /// </summary>
    private static object FormatContentValue(AIContent content)
    {
        if (content is TextReasoningContent reasoningContent)
        {
            return new
            {
                reasoningContent.Text
            };
        }

        return content;
    }

    private static string FormatToolsByType(IEnumerable<AITool>? tools)
    {
        if (tools is null || !tools.Any())
        {
            return string.Empty;
        }

        try
        {
            var regularTools = new List<string>();

            foreach (var tool in tools)
            {
                var aiFunction = (AIFunction)tool;

                if (!aiFunction.IsHandoff() && !aiFunction.IsAgentAsTool())
                {
                    regularTools.Add(aiFunction.Name);
                }
            }

            return regularTools.Count > 0
                ? JsonSerializer.Serialize(regularTools, AIJsonUtilities.DefaultOptions)
                : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string FormatHandoffs(IEnumerable<AITool>? tools)
    {
        if (tools is null || !tools.Any())
        {
            return string.Empty;
        }

        try
        {
            var handoffAgents = new List<string>();

            foreach (var tool in tools)
            {
                var aiFunction = (AIFunction)tool;

                if (aiFunction.IsHandoff())
                {
                    var agentNameProp = aiFunction.GetType().GetProperty("AgentName");
                    if (agentNameProp != null)
                    {
                        var agentName = agentNameProp.GetValue(aiFunction)?.ToString();
                        if (!string.IsNullOrEmpty(agentName))
                        {
                            handoffAgents.Add(agentName);
                        }
                    }
                }
            }

            return handoffAgents.Count > 0
                ? JsonSerializer.Serialize(handoffAgents, AIJsonUtilities.DefaultOptions)
                : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string FormatAgentAsTools(IEnumerable<AITool>? tools)
    {
        if (tools is null || !tools.Any())
        {
            return string.Empty;
        }

        try
        {
            var agentTools = new List<string>();

            foreach (var tool in tools)
            {
                var aiFunction = (AIFunction)tool;

                if (aiFunction.IsAgentAsTool())
                {
                    agentTools.Add(aiFunction.Name);
                }
            }

            return agentTools.Count > 0
                ? JsonSerializer.Serialize(agentTools, AIJsonUtilities.DefaultOptions)
                : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string FormatPromptStarters(PromptText? instructions)
    {
        if (instructions is null)
        {
            return string.Empty;
        }

        try
        {
            var starters = instructions.GetPromptStarters();
            return starters.Count > 0
                ? JsonSerializer.Serialize(starters, AIJsonUtilities.DefaultOptions)
                : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string FormatAgentPrompt(PromptText? instructions)
    {
        if (instructions is null)
        {
            return string.Empty;
        }

        try
        {
            var agentPrompt = instructions.GetOriginalText();
            return !string.IsNullOrEmpty(agentPrompt)
                ? agentPrompt
                : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string FormatCommonPrompts(PromptText? instructions)
    {
        if (instructions is null)
        {
            return string.Empty;
        }

        try
        {
            var commonPrompts = instructions.GetCommonPrompts();
            if (commonPrompts.Count == 0)
            {
                return string.Empty;
            }

            // Transform to array of objects with name and content
            var formattedPrompts = commonPrompts.Select(cp => new
            {
                cp.name,
                cp.content
            }).ToList();

            return JsonSerializer.Serialize(formattedPrompts, AIJsonUtilities.DefaultOptions);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string FormatPromptEnders(PromptText? instructions)
    {
        if (instructions is null)
        {
            return string.Empty;
        }

        try
        {
            var enders = instructions.GetPromptEnders();
            return enders.Count > 0
                ? JsonSerializer.Serialize(enders, AIJsonUtilities.DefaultOptions)
                : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string FormatResultSummarizationPrompt(string? prompt)
    {
        return !string.IsNullOrEmpty(prompt) ? prompt : string.Empty;
    }

    private static string FormatExperimentVariants(IReadOnlyDictionary<string, Variant> variants)
    {
        try
        {
            var result = variants.Select(kv =>
            {
                return new
                {
                    ExperimentId = kv.Key,
                    Variant = kv.Value,
                };
            }).ToList();

            return JsonSerializer.Serialize(result, _chatMessageJsonOptions);
        }
        catch (Exception e)
        {
            return $"Error formatting experiment variants: {e.Message}";
        }
    }

    private void RefreshUserCancellationTokenSource()
    {
        if (_disposed)
        {
            return; // Don't refresh if disposed
        }

        lock (_userCancellationTokenSourceLock)
        {
            _userCancellationTokenSource.Dispose();
            _userCancellationTokenSource = new CancellationTokenSource();
            _logger.LogInternalInformation("User cancellation token source refreshed.");
        }
    }

    private async Task<ModificationResult> ProcessConversationModifierAsync(
        ConversationModifierEnum modifierEnum,
        string userMessage,
        CancellationToken cancellationToken)
    {
        if (Modifiers.TryGet(modifierEnum, out var modifier) && modifier != null)
        {
            _logger.LogInternalInformation("[{threadId}]Processing conversation modifier: {ModifierKey}", _context.ThreadId, modifierEnum);

            // Get the agent chat history for persistence
            var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
            if (agentChatHistory == null)
            {
                _logger.LogInternalError("[{threadId}] AgentChatHistory is null during modifier processing", _context.ThreadId);
                return new ModificationResult { PassToMainLoop = true };
            }

            // Get the modifier agent
            var modifierAgent = modifier.GetModifierAgent();

            // Set up RunConfig for direct agent invocation
            var runConfig = new RunConfig
            {
                ChatClient = _chatClientProvider.GeneralPurposeModel,
                LoggerFactory = _loggerFactory,
                EnableDebugOutput = _enableReasoningDebugOutput,
                ThreadId = _context.ThreadId,
                SkillRegistry = _skillRegistry,
                EnablePartialToolOutput = _featureConfig.PartialOutputEnabled,
                AmbientContextProvider = _ambientContextProvider,
                ChatClientProvider = _chatClientProvider,
                HookManager = _hookManager
            };

            try
            {
                var runHooks = CreateRunHooks();

                // Run the modifier agent with the full chat history plus new user message
                var runResult = await Runner.RunAsync(
                    startingAgent: modifierAgent,
                    input: _chatHistory!,
                    config: runConfig,
                    runtimeModifier: _agentRuntimeModifier,
                    context: _context,
                    hooks: runHooks,
                    displayModelOutput: new ChatMessageOutput(_outboundCommunicationService, _streamingMessageRepository, _context, Guid.NewGuid()),
                    cancellationToken: cancellationToken);

                _logger.LogInternalInformation("[{threadId}]Modifier agent completed execution", _context.ThreadId);

                // Persist all new messages from the modifier agent to chat history
                if (runResult.NewItems.Count != 0)
                {
                    await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);
                    _logger.LogInternalInformation("[{threadId}]Persisted {MessageCount} modifier agent messages to chat history",
                        _context.ThreadId, runResult.NewItems.Count);
                }

                // Handle manual tool calls from the modifier agent
                while (runResult.ManualToolCalls != null && runResult.ManualToolCalls.Count > 0)
                {
                    List<ManualToolCallResult> toolResults = [];

                    var toolCall = runResult.ManualToolCalls.Single(); // Should only be one tool call at a time
                    var toolCallMessageId = Guid.NewGuid();

                    await _outboundCommunicationService.AppendAgentManualToolCallMessage(
                        _context.ThreadId,
                        runResult.ManualToolCalls,
                        toolCallMessageId);

                    // TODO: Add support for read-only mode checking, approval flow, and CLI/kubectl execution handling
                    // For now, we'll execute tools directly without these checks
                    try
                    {
                        var functionResult = await InvokeToolWithErrorHandlingAsync(toolCall, cancellationToken);

                        if (_featureConfig.PartialOutputEnabled)
                        {
                            var processedOutput = await _toolOutputProcessService.ProcessToolOutputAsync(
                                _context.ThreadId,
                                toolCall.Tool,
                                toolCall.FunctionCall.CallId,
                                functionResult?.ToString(),
                                cancellationToken);

                            toolResults.Add(new ManualToolCallResult()
                            {
                                FunctionCall = toolCall.FunctionCall,
                                Output = processedOutput
                            });
                        }
                        else
                        {
                            toolResults.Add(new ManualToolCallResult()
                            {
                                FunctionCall = toolCall.FunctionCall,
                                Output = functionResult
                            });
                        }

                    }
                    catch (Exception ex)
                    {
                        toolResults.Add(new ManualToolCallResult()
                        {
                            FunctionCall = toolCall.FunctionCall,
                            Output = GetErrorMessage(toolCall.FunctionCall, ex),
                        });
                    }

                    runResult = await Runner.ResumeFromManualToolsAsync(
                        previousResult: runResult,
                        manualToolResults: toolResults,
                        config: runConfig,
                        context: _context,
                        hooks: runHooks,
                        displayModelOutput: new ChatMessageOutput(_outboundCommunicationService, _streamingMessageRepository, _context, Guid.NewGuid()),
                        toolOutputProcessService: _toolOutputProcessService,
                        allowParallelToolCalls: true,
                        cancellationToken: cancellationToken
                    );

                    await _outboundCommunicationService.AppendAgentManualToolCallResult(
                        _context.ThreadId,
                        toolResults,
                        toolCallMessageId);

                    await PersistReasoningMessagesAsync(agentChatHistory, runResult.NewItems);
                }

                // Process the agent output to determine modification result
                var modificationResult = await modifier.ProcessModificationAsync(runResult, cancellationToken);

                _logger.LogInternalInformation("[{threadId}]Modifier result: PassToMainLoop={PassToMainLoop}",
                    _context.ThreadId, modificationResult.PassToMainLoop);

                return modificationResult;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "[{threadId}]Error running conversation modifier: {ModifierKey}", _context.ThreadId, modifierEnum);
                // On error, pass to main loop as fallback
                return new ModificationResult { PassToMainLoop = true };
            }
        }
        else
        {
            _logger.LogInternalWarning("[{threadId}]Unknown conversation modifier: {ModifierKey}", _context.ThreadId, modifierEnum);
            // Unknown modifier, pass to main loop
            return new ModificationResult { PassToMainLoop = true };
        }
    }

    private async Task ProcessTodoPersistenceAndStreamingAsync(IEnumerable<KeyValuePair<string, object?>> todoArguments, Guid threadId)
    {
        if (_threadRepository == null || todoArguments == null)
        {
            _logger.LogInternalWarning("Skipping todo processing: HasThreadRepository={HasRepo}, HasArguments={HasArgs}",
                _threadRepository != null, todoArguments != null);
            return;
        }

        try
        {
            // Extract todos argument
            if (!todoArguments.Any(kvp => kvp.Key == "todos"))
            {
                return;
            }

            var todosObj = todoArguments.First(kvp => kvp.Key == "todos").Value;
            if (todosObj == null)
            {
                return;
            }

            string serializedTodos;
            if (todosObj is JsonElement element)
            {
                serializedTodos = element.GetRawText();
            }
            else
            {
                serializedTodos = JsonSerializer.Serialize(todosObj, AIJsonUtilities.DefaultOptions);
            }

            var frameworkTodos = JsonSerializer.Deserialize<List<FrameworkTodoItem>>(serializedTodos, AIJsonUtilities.DefaultOptions);
            if (frameworkTodos == null || frameworkTodos.Count == 0)
            {
                return;
            }

            var todoItems = frameworkTodos.Select((item, index) => new TodoItem
            {
                Content = item.Content,
                ActiveForm = item.ActiveForm,
                Status = ConvertToTodoItemStatus(item.Status),
                Order = index,
                StartedAt = item.Status == "in_progress" ? DateTime.UtcNow : null,
                CompletedAt = item.Status == "completed" ? DateTime.UtcNow : null
            }).ToList();

            // Find existing todo plan with same content
            var existingPlan = await FindTodoPlanWithSameContentAsync(todoItems, threadId, _threadRepository);

            TodoPlan todoPlan;
            TodoPlanUpdateType updateType;

            if (existingPlan == null)
            {
                // Create new todo plan
                todoPlan = CreateTodoPlanFromItems(todoItems, threadId);
                await _threadRepository.CreateTodoPlanAsync(todoPlan);
                updateType = TodoPlanUpdateType.Created;
            }
            else
            {
                // Update existing plan
                todoPlan = UpdateExistingPlan(existingPlan, todoItems);
                await _threadRepository.UpdateTodoPlanAsync(todoPlan);
                updateType = DetermineUpdateType(existingPlan, todoPlan);
            }

            // Serialize and stream the TodoPlan (for both create and update)
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            };
            var todoPlanJson = JsonSerializer.Serialize(todoPlan, options);

            // Stream the TodoPlan for live updates
            await _outboundCommunicationService.AppendTodoPlanUpdate(
                threadId,
                todoPlanJson,
                todoPlan.TriggerMessageId,
                todoPlan.LastUpdated
            );

            _logger.LogInternalInformation("Streamed TodoPlan update for thread {ThreadId}, plan {TodoPlanId}, updateType: {UpdateType}", threadId, todoPlan.Id, updateType);

            // Create TodoInfo message card ONLY for new plans (to show card in chat)
            if (updateType == TodoPlanUpdateType.Created)
            {
                _logger.LogInternalInformation("Creating TodoInfo message card for new TodoPlan {TodoPlanId} in thread {ThreadId}", todoPlan.Id, threadId);

                var todoInfo = new TodoInfo(
                    todoPlan.Id,
                    todoPlan.Title,
                    todoPlan.Status,
                    todoPlan.LastUpdated,
                    todoPlan.TriggerMessageId
                );

                var todoInfoJson = JsonSerializer.Serialize(todoInfo, options);
                var message = new ChatMessage(ChatRole.User, todoInfoJson);

                _logger.LogInternalInformation("Creating TodoInfo message card for thread {ThreadId}, messageId {MessageId}", threadId, todoPlan.TriggerMessageId);

                // Create the card in chat (stored in DB)
                await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                    threadId: threadId,
                    message: message,
                    agentTaskInfo: null,
                    todoInfo: todoInfo,
                    messageId: todoPlan.TriggerMessageId,
                    type: StreamMessageType.TodoPlan);

                _logger.LogInternalInformation("Successfully created TodoInfo message card for TodoPlan {TodoPlanId}", todoPlan.Id);
            }
            else
            {
                _logger.LogInternalInformation("Skipping TodoInfo card creation for TodoPlan {TodoPlanId} - updateType: {UpdateType}", todoPlan.Id, updateType);
            }

            _logger.LogInternalInformation("Successfully processed todo plan persistence and streaming for thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to process todo plan persistence and streaming for thread {ThreadId}", threadId);
        }
    }

    private async Task<(Core.Models.Api.v1.AzCliExecution?, Core.Models.Api.v1.KubectlExecution?, Core.Models.Api.v1.PsqlExecution?)> ListPendingExecutions(Guid threadId)
    {
        var azCliExecutionTask = _threadRepository.ListPendingAzCliExecutionAsync(threadId);
        var kubectlExecutionTask = _threadRepository.ListPendingKubectlExecutionAsync(threadId);
        var psqlExecutionTask = _threadRepository.ListPendingPsqlExecutionAsync(threadId);

        await Task.WhenAll(azCliExecutionTask, kubectlExecutionTask, psqlExecutionTask);

        return (await azCliExecutionTask, await kubectlExecutionTask, await psqlExecutionTask);
    }

    private record CliToolExecution
    {
        public CliToolType ToolType { get; set; }
        public Core.Models.Api.v1.AzCliExecution? AzCliExecution { get; set; }
        public Core.Models.Api.v1.KubectlExecution? KubectlExecution { get; set; }
        public Core.Models.Api.v1.PsqlExecution? PsqlExecution { get; set; }

        public bool IsPending => (ToolType == CliToolType.AzCli && AzCliExecution != null && AzCliExecution.Status.IsPending()) ||
                                 (ToolType == CliToolType.Kubectl && KubectlExecution != null && KubectlExecution.Status.IsPending()) ||
                                 (ToolType == CliToolType.Psql && PsqlExecution != null && PsqlExecution.Status.IsPending());
    }

    private async Task<CliToolExecution> GetCliToolExecution(Guid threadId, CliToolType toolType, Guid executionId)
    {
        if (toolType == CliToolType.AzCli)
        {
            var azCliExecution = await _threadRepository.GetAzCliExecutionAsync(threadId, executionId);
            return new CliToolExecution
            {
                ToolType = toolType,
                AzCliExecution = azCliExecution
            };
        }
        else if (toolType == CliToolType.Kubectl)
        {
            var kubectlExecution = await _threadRepository.GetKubectlExecutionAsync(threadId, executionId);
            return new CliToolExecution
            {
                ToolType = toolType,
                KubectlExecution = kubectlExecution
            };
        }
        else if (toolType == CliToolType.Psql)
        {
            var psqlExecution = await _threadRepository.GetPsqlExecutionAsync(threadId, executionId);
            return new CliToolExecution
            {
                ToolType = toolType,
                PsqlExecution = psqlExecution
            };
        }
        throw new ArgumentException($"Unsupported CliToolType: {toolType}");
    }

    private SkillList GetActiveSkills(Agent<AgentContext> agent)
    {
        var skillList = new SkillList();

        foreach (var skillName in _context.ActiveSkills ?? [])
        {
            var skill = _skillRegistry.GetSkillByName(skillName, agent.AddSystemSkills, agent.AllowedSkills);
            if (skill != null)
            {
                skillList.Enqueue(skill);
            }
        }

        return skillList;
    }

    private static async Task<TodoPlan?> FindTodoPlanWithSameContentAsync(List<TodoItem> newTodoItems, Guid threadId, IThreadRepository threadRepository)
    {
        var existingPlans = await threadRepository.GetTodoPlansAsync(threadId);

        foreach (var plan in existingPlans)
        {
            // Compare todo items by content with overlap threshold
            var existingContents = plan.Items.Select(item => item.Content).ToList();
            var newContents = newTodoItems.Select(item => item.Content).ToList();

            // Calculate overlap: how many existing items appear in new content
            var matchCount = existingContents.Count(e => newContents.Contains(e));
            var overlapPercentage = (double)matchCount / Math.Min(existingContents.Count, newContents.Count);

            // At least 1 match AND 40%+ overlap = same plan
            if (matchCount >= 1 && overlapPercentage >= 0.4)
            {
                return plan;
            }
        }

        return null;
    }

    private static TodoPlan CreateTodoPlanFromItems(List<TodoItem> todoItems, Guid threadId)
    {
        var now = DateTime.UtcNow;
        var title = GeneratePlanTitle(todoItems);

        return new TodoPlan
        {
            Id = Guid.NewGuid(),
            Title = title,
            ThreadId = threadId,
            TriggerMessageId = Framework.ToolStatic.AsyncLocalToolCallMessageId.Value ?? Guid.NewGuid(),
            Status = DeterminePlanStatus(todoItems),
            Items = todoItems,
            CreatedAt = now,
            LastUpdated = now
        };
    }

    private static TodoPlan UpdateExistingPlan(TodoPlan existingPlan, List<TodoItem> newTodoItems)
    {
        var now = DateTime.UtcNow;

        var updatedItems = newTodoItems.Select((item, index) => new TodoItem
        {
            Content = item.Content,
            ActiveForm = item.ActiveForm,
            Status = item.Status,
            Order = index,
            StartedAt = item.Status == TodoItemStatus.InProgress ? now : existingPlan.Items.ElementAtOrDefault(index)?.StartedAt,
            CompletedAt = item.Status == TodoItemStatus.Completed ? now : existingPlan.Items.ElementAtOrDefault(index)?.CompletedAt
        }).ToList();

        return existingPlan with
        {
            Items = updatedItems,
            Status = DeterminePlanStatus(updatedItems),
            LastUpdated = now
        };
    }

    private static TodoPlanUpdateType DetermineUpdateType(TodoPlan oldPlan, TodoPlan newPlan)
    {
        if (newPlan.Status == TodoPlanStatus.Completed && oldPlan.Status != TodoPlanStatus.Completed)
        {
            return TodoPlanUpdateType.Completed;
        }

        // Check if any item status changed
        var oldItems = oldPlan.Items.ToList();
        var newItems = newPlan.Items.ToList();

        for (var i = 0; i < Math.Min(oldItems.Count, newItems.Count); i++)
        {
            if (oldItems[i].Status != newItems[i].Status)
            {
                return TodoPlanUpdateType.ItemStatusChanged;
            }
        }

        return TodoPlanUpdateType.Updated;
    }

    private static string GeneratePlanTitle(List<TodoItem> todoItems)
    {
        if (todoItems.Count == 0)
        {
            return "Todo Plan";
        }

        var firstItem = todoItems.First().Content;

        return firstItem;
    }

    private static TodoItemStatus ConvertToTodoItemStatus(string status)
    {
        return status switch
        {
            "pending" => TodoItemStatus.Pending,
            "in_progress" => TodoItemStatus.InProgress,
            "completed" => TodoItemStatus.Completed,
            _ => TodoItemStatus.Pending
        };
    }

    private static TodoPlanStatus DeterminePlanStatus(List<TodoItem> todoItems)
    {
        if (todoItems.Count == 0)
        {
            return TodoPlanStatus.Planning;
        }

        if (todoItems.All(item => item.Status == TodoItemStatus.Completed))
        {
            return TodoPlanStatus.Completed;
        }

        if (todoItems.Any(item => item.Status == TodoItemStatus.InProgress))
        {
            return TodoPlanStatus.InProgress;
        }

        return TodoPlanStatus.Planning;
    }

    private static bool TryGetCliToolExecutionResult(object? toolResult, out CliToolExecutionResult? executionResult)
    {
        executionResult = null;
        try
        {
            if (toolResult is JsonElement jsonElement)
            {
                var result = jsonElement.Deserialize<CliToolExecutionResult>(AIJsonUtilities.DefaultOptions);
                if (result?.ExecutionId != null)
                {
                    executionResult = result;
                    return true;
                }
            }
        }
        catch
        {
            // Ignore deserialization errors
        }

        return false;
    }

    // Helper class to deserialize framework todo format
    private class FrameworkTodoItem
    {
        public string Content { get; set; } = string.Empty;
        public string ActiveForm { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            lock (_userCancellationTokenSourceLock)
            {
                _userCancellationTokenSource?.Dispose();
            }

            _semaphore?.Dispose();

            // Mark the channel as complete to signal no more writes
            _msgCh.Writer.TryComplete();

            _disposed = true;
        }
    }
}

