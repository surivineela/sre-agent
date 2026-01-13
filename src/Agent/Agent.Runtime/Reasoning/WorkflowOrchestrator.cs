// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Logging;
using Agent.Runtime.Workflow;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Workflow orchestrator that provides a similar interface to ReasoningLoop
/// but executes workflow steps sequentially based on agent definitions.
/// This is designed to be pluggable and easily removable in the future.
/// </summary>
public class WorkflowOrchestrator : IDisposable
{
    private readonly ILogger<WorkflowOrchestrator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IThreadRepository _threadRepository;
    private readonly IAgentProvider<AgentContext> _agentProvider;
    private AgentContext _context; // made mutable to allow state transitions similar to ReasoningLoop
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly Tracer _tracer;
    private readonly IncidentManagementSettings _incidentManagementSettings;
    private readonly CoreSettings _coreSettings;
    private readonly ModeSwitchHandler _modeSwitchHandler;
    private readonly ISkillRegistry _skillRegistry;

    // Telemetry spans for workflow tracing
    private readonly TelemetrySpan? _rootSpan;
    private TelemetrySpan? _currentAgentSpan;
    private readonly ConcurrentDictionary<string, TelemetrySpan?> _toolSpans = new();
    private TelemetrySpan? _currentGenerationSpan;
    private TelemetrySpan? _currentSummarizerSpan;
    private TelemetrySpan? _currentCriticSpan;

    // In-memory storage for agent execution results
    private readonly Dictionary<string, WorkflowActivityAgentOutput> _executionResults = new();
    private readonly List<ChatMessage> _chatHistory = new();
    private bool _disposed = false;

    // Post-run persistence fields
    private string? _canonicalExtractedParametersJson; // canonical JSON snapshot of extracted parameters (after parameter extraction only)
    private bool _parametersPersisted; // guard to avoid duplicate post-run parameter message
    private bool _digestPersisted; // guard to avoid duplicate digest message
    private Guid? _lastUserReasoningMessageId; // track last user reasoning message for lazy AgentChatHistory creation

    public WorkflowOrchestrator(
        ILoggerFactory loggerFactory,
        IChatClientProvider chatClientProvider,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IThreadRepository threadRepository,
        AgentContext context,
        IAgentProvider<AgentContext> agentProvider,
        IToolFactory<AgentContext> toolFactory,
        Tracer tracer,
        IncidentManagementSettings incidentManagementSettings,
        CoreSettings coreSettings,
        ISkillRegistry skillRegistry)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<WorkflowOrchestrator>();
        _chatClientProvider = chatClientProvider;
        _outboundCommunicationService = outboundCommunicationService;
        _threadRepository = threadRepository;
        _context = context;
        _agentProvider = agentProvider;
        _toolFactory = toolFactory;
        _tracer = tracer;
        _incidentManagementSettings = incidentManagementSettings;
        _coreSettings = coreSettings;
        _skillRegistry = skillRegistry;

        // Initialize mode switch handler (kept minimal; only active if feature flag enabled)
        _modeSwitchHandler = new ModeSwitchHandler(
            threadRepository: _threadRepository,
            outboundCommunicationService: _outboundCommunicationService,
            enabled: ModeSwitchHelper.ModeSwitchEnabled(_coreSettings));

        // Initialize root span for workflow execution
        _rootSpan = _tracer.StartSpan($"workflow.orchestrator.{_context.ThreadId}");
        _rootSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
        _rootSpan.SetAttribute("workflow.orchestrator", "WorkflowOrchestrator");
    }

    private async Task SetAmbientThreadContextAsync()
    {
        Core.ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;

        if (!_context.IsIncidentTestModeEnabled.HasValue)
        {
            var thread = await _threadRepository.GetThreadAsync(_context.ThreadId);
            _context = _context with { IsIncidentTestModeEnabled = thread?.IsIncidentTestModeEnabled ?? false };
        }
        ThreadContextAccessor.SetThreadContext(_context);
    }

    /// <summary>
    /// Interface compatibility method - loads chat history similar to ReasoningLoop
    /// </summary>
    public async Task LoadChatHistoryAsync()
    {
        _logger.LogInternalInformation("Loading chat history for workflow orchestrator");

        var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
        if (agentChatHistory != null)
        {
            var reasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(_threadRepository);
            var chatMessages = reasoningMessages.GetChatMessages();

            foreach (var chatMessage in chatMessages)
            {
                _chatHistory.Add(chatMessage);
            }
        }

        _logger.LogInternalInformation($"Loaded {_chatHistory.Count} messages from chat history");
    }

    /// <summary>
    /// Interface compatibility method - appends new user message to trigger workflow execution
    /// </summary>
    public async Task AppendNewUserMessageAsync(ChatMessage msg, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation($"New user message received, starting workflow execution Message: {msg.Text}");

        // Unified /mode handler (returns early if a mode switch or already-in-mode message was processed)
        var (handled, updatedCtx) = await _modeSwitchHandler.HandleAsync(_context, msg.Text, cancellationToken);
        if (handled)
        {
            _context = updatedCtx;
            await CompleteEarlyModeSwitchAsync(cancellationToken);
            return;
        }

        // Add message to chat history
        _chatHistory.Add(msg);

        var messageId = Guid.NewGuid();
        // TODO Remove the Debug Part
        // Persist the message to the repository
        var reasoningMessage = new ReasoningMessage(
            Id: messageId,
            AgentContextId: _context.Id,
            Role: ReasoningMessageRoleEnum.User,
            SerializedChatMessage: JsonSerializer.Serialize(msg));
        //    SerializedChatMessage: JsonSerializer.Serialize(msg) + $"Debug Id: {_context.Id} MessageId: {messageId} ThreadId: {_context.ThreadId} ");

        await _threadRepository.CreateReasoningMessageAsync(reasoningMessage);
        _lastUserReasoningMessageId = reasoningMessage.Id;

        var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
        if (agentChatHistory != null)
        {
            await _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage);
        }
        else
        {
            // Lazy create AgentChatHistory if it does not exist yet (ensures later persistence of parameters/digest/summary)
            var newHistory = new AgentChatHistory(_context.Id, new List<Guid> { reasoningMessage.Id })
            {
                LatestUserMessageId = reasoningMessage.Id
            };
            await _threadRepository.CreateAgentChatHistoryAsync(newHistory);
        }

        // Start workflow execution
        await ExecuteWorkflowAsync(cancellationToken);
    }

    /// <summary>
    /// Interface compatibility method - appends function call messages (not used in workflow orchestration)
    /// </summary>
    public Task AppendFunctionCallMessagesAsync(List<ChatMessage> msgs, CancellationToken cancellationToken = default)
    {
        // In workflow orchestration, we don't handle function calls the same way
        // This is mainly for ReasoningLoop compatibility
        _logger.LogInternalInformation("AppendFunctionCallMessagesAsync called but not implemented for workflow orchestration");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Interface compatibility method - appends approval messages (not used in workflow orchestration)
    /// </summary>
    public Task AppendNewApprovalMessageAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        // In workflow orchestration, we don't handle approvals the same way
        // This is mainly for ReasoningLoop compatibility
        _logger.LogInternalInformation("AppendNewApprovalMessageAsync called but not implemented for workflow orchestration");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Interface compatibility method - exports chat history
    /// </summary>
    public Task<IEnumerable<ChatMessage>> ExportChatHistoryAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_chatHistory.AsEnumerable());
    }

    /// <summary>
    /// Execute a router agent (like rca_router_meta_agent) with handoff detection
    /// </summary>
    private async Task<string?> ExecuteRouterAgentAsync(Agent<AgentContext> routerAgent, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation($"Executing router agent: {routerAgent.Name}");

            // Create RunHooks for tool resolution
            var runHooks = CreateRunHooks();

            await SetAmbientThreadContextAsync();

            var result = await Runner.RunWithHandoffDetectionAsync(
                routerAgent,
                _chatHistory,
                new RunConfig
                {
                    ChatClient = _chatClientProvider.GeneralPurposeModel,
                    LoggerFactory = _loggerFactory,
                    SkillRegistry = _skillRegistry
                },
                context: _context,
                hooks: runHooks,
                cancellationToken: cancellationToken);

            if (result.HandoffDetected && !string.IsNullOrEmpty(result.HandoffTargetAgent))
            {
                var message = $"Router agent {routerAgent.Name} decided to hand off to {result.HandoffTargetAgent}";
                _logger.LogInternalInformation(message);
                await PostAssistantMessageToThreadAsync(new ChatMessage(ChatRole.Assistant, message));
                return result.HandoffTargetAgent;
            }
            else
            {
                var message = $"Router agent {routerAgent.Name} completed without handoff";
                _logger.LogInternalWarning(message);
                await PostAssistantMessageToThreadAsync(new ChatMessage(ChatRole.Assistant, message));
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing router agent {routerAgent.Name}");
            return null;
        }
    }

    /// <summary>
    /// Main workflow execution logic
    /// </summary>
    private async Task ExecuteWorkflowAsync(CancellationToken cancellationToken)
    {
        // Transition to Processing (align with ReasoningLoop semantics) at the beginning of workflow execution.
        if (_context.ContextState != ContextStateEnum.Processing)
        {
            await ChangeAgentContextStateAsync(ContextStateEnum.Processing);
        }

        try
        {
            _logger.LogInternalInformation("Starting workflow execution");
            _logger.LogInternalInformation(GetWorkflowConfigurationStatus());

            // Get the current agent
            var currentAgentName = _context.AgentHandoffChain.LastOrDefault() ?? _context.CurrentAgent;
            if (string.IsNullOrEmpty(currentAgentName))
            {
                _logger.LogInternalError("No current agent found for workflow orchestration");
                return;
            }

            var currentAgent = _agentProvider.GetAgent(currentAgentName, _context.ThreadId.ToString());
            if (currentAgent == null)
            {
                _logger.LogInternalError($"Agent {currentAgentName} not found");
                return;
            }

            // Check if this is a router agent (like rca_router_meta_agent)
            if (currentAgentName == RcaRoutingConstants.WorkflowRootAgent || currentAgent.AgentType == Framework.Models.AgentType.Autonomous)
            {
                _logger.LogInternalInformation($"Detected router agent: {currentAgentName}");
                var handoffTarget = await ExecuteRouterAgentAsync(currentAgent, cancellationToken);

                if (!string.IsNullOrEmpty(handoffTarget))
                {
                    // Update the agent handoff chain
                    _context.AgentHandoffChain.Add(handoffTarget);

                    _logger.LogInternalInformation($"Router handoff completed: {currentAgentName} -> {handoffTarget}");

                    // Continue with the workflow using the target agent
                    await ExecuteWorkflowAsync(cancellationToken);
                    return;
                }
                else
                {
                    _logger.LogInternalWarning($"Router agent {currentAgentName} did not provide handoff target");
                    return;
                }
            }

            // Handle orchestrator agents (existing logic)
            var orchestratorAgent = currentAgent;
            if (orchestratorAgent.AgentType != Framework.Models.AgentType.Orchestrator)
            {
                _logger.LogInternalError($"Agent {currentAgentName} is not an Orchestrator type agent");
                return;
            }

            _logger.LogInternalInformation($"Using orchestrator agent: {currentAgentName}");

            // Step 1: Execute parameter extraction agent if defined
            var parameterExtractionAgentName = orchestratorAgent.ParameterExtractionAgent;

            WorkflowExecutionContext baseExecutionContext = new()
            {
                WorkflowId = Guid.NewGuid().ToString(),
                OrchestratorAgent = currentAgentName, // Use the dispatched agent name
                StartedAt = DateTime.UtcNow
            };

            // Bootstrap IncidentId from thread if available
            try
            {
                var threadBootstrap = await _threadRepository.GetThreadAsync(_context.ThreadId);
                var bootstrapIncidentId = threadBootstrap?.Status?.IncidentStatus?.IncidentId;
                if (!string.IsNullOrWhiteSpace(bootstrapIncidentId))
                {
                    baseExecutionContext.IncidentId = bootstrapIncidentId;
                    // Also seed parameters so downstream agents can see it
                    baseExecutionContext.AccumulatedParameters.SetString("IncidentId", bootstrapIncidentId);
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(parameterExtractionAgentName))
            {
                _logger.LogInternalInformation($"Executing parameter extraction agent: {parameterExtractionAgentName}");
                var parameterAgent = _agentProvider.GetAgent(parameterExtractionAgentName, _context.ThreadId.ToString());
                var parameterResult = await ExecuteAgentWithHistory(parameterAgent, cancellationToken);

                if (parameterResult != null)
                {
                    // Merge extracted parameters into base execution context
                    parameterResult.ParseParameters();
                    foreach (var param in parameterResult.ParsedParameters)
                    {
                        baseExecutionContext.AccumulatedParameters.SetString(param.Key, param.Value);
                    }
                    if (baseExecutionContext.AccumulatedParameters.Count == 0)
                    {
                        // Sometimes, it returns parameters that can't parse.
                        baseExecutionContext.AccumulatedParameters.SetString("parameters", parameterResult.Parameters);
                    }

                    // If IncidentId was extracted, also set it on the context property
                    if (parameterResult.ParsedParameters.TryGetValue("IncidentId", out var extractedIncidentId) && !string.IsNullOrWhiteSpace(extractedIncidentId))
                    {
                        baseExecutionContext.IncidentId = extractedIncidentId;
                    }

                    // Capture canonical parameter snapshot (sorted by key, stable JSON) once after extraction
                    if (_canonicalExtractedParametersJson == null)
                    {
                        try
                        {
                            // Preserve original insertion order (avoid alphabetical sort so related fields like startTime/endTime stay adjacent)
                            var dict = baseExecutionContext.AccumulatedParameters.ToDictionary();
                            _canonicalExtractedParametersJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalWarning(ex, "Failed to build canonical parameter snapshot");
                        }
                    }
                }
            }

            // Step 2: Execute orchestration start agents recursively (each with its own context)
            var startAgents = orchestratorAgent.OrchestrationStartAgents;
            if (startAgents?.Count > 0)
            {
                _logger.LogInternalInformation($"Starting {startAgents.Count} independent orchestration branches from {currentAgentName}");

                // Execute each start agent as an independent branch with its own context
                var branchTasks = new List<Task>();

                for (var i = 0; i < startAgents.Count; i++)
                {
                    var startAgent = startAgents[i];
                    var branchContext = baseExecutionContext.Clone(); // Each branch gets its own context
                    branchContext.StepNumber = i + 1; // Unique step number for each branch

                    _logger.LogInternalInformation($"Starting branch {i + 1}: {startAgent}");

                    // Execute each branch independently
                    await ExecuteAgentBranchAsync(startAgent, branchContext, new HashSet<string>(), cancellationToken);
                    //branchTasks.Add(branchTask);
                }

                // Wait for all branches to complete
                //await Task.WhenAll(branchTasks);
            }
            else
            {
                // No start agents: inject a synthetic result so that incidentId/parameters are discoverable by summarizer logic.
                // This preserves existing SummarizeAndPostResults behavior (it already looks at _executionResults for incidentId).
                const string syntheticKey = "orchestrator_parameters";
                if (!_executionResults.ContainsKey(syntheticKey))
                {
                    try
                    {
                        var paramJson = JsonSerializer.Serialize(baseExecutionContext.AccumulatedParameters.Values);
                        var synthetic = new WorkflowActivityAgentOutput
                        {
                            // Required workflow content
                            Analysis = "Parameter extraction completed (no activity agents executed).",
                            Parameters = string.IsNullOrWhiteSpace(paramJson) ? "{}" : paramJson,
                            NextSteps = new List<string>(),
                            // Required IAgentOutput properties
                            ReasoningScratchPad = "Synthetic output (no activity agents).",
                            NotifyUserMessage = "Collected parameters only.",
                            State = "Completed",
                            StateExplanation = "No activity agents configured; parameter extraction (or bootstrap) finished.",
                            // Programmatic fields
                            AgentName = parameterExtractionAgentName ?? syntheticKey,
                            ExecutionContext = baseExecutionContext,
                            GeneratedAt = DateTime.UtcNow
                        };
                        synthetic.ParseParameters();
                        _executionResults[syntheticKey] = synthetic;
                        _logger.LogInternalInformation("Injected synthetic parameters result for orchestrator {AgentName}", orchestratorAgent.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning(ex, "Failed to inject synthetic parameters result.");
                    }
                }
            }

            // Step 3: Summarize results and post to thread
            await SummarizeAndPostResults(orchestratorAgent, cancellationToken);

            _logger.LogInternalInformation("Workflow execution completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during workflow execution");
            throw;
        }
        finally
        {
            // Ensure we always attempt to signal the client that processing is complete.
            // Unlike ReasoningLoop we currently do not distinguish user cancellation token here.
            try
            {
                await _outboundCommunicationService.SignalProcessingComplete(_context.ThreadId, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected if the provided cancellation token was canceled before signaling stop.
                _logger.LogInternalInformation("SignalProcessingComplete canceled for workflow thread {ThreadId}", _context.ThreadId);
            }
            catch (Exception ex)
            {
                // Do not rethrow; we still want to attempt context state transition.
                _logger.LogInternalError(ex, "Failed to send SignalProcessingComplete for workflow thread {ThreadId}", _context.ThreadId);
            }

            // Return context state to Idle (single place handles all early returns)
            await ChangeAgentContextStateAsync(ContextStateEnum.Idle);
        }
    }

    /// <summary>
    /// Ensures UI spinner/processing state is cleared when /mode causes an early return.
    /// Mirrors ExecuteWorkflowAsync finally responsibilities (Idle + SignalProcessingComplete) for that path.
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
            _logger.LogInternalWarning(ex, "Failed to set Idle state after mode switch (workflow)");
        }
        try
        {
            await _outboundCommunicationService.SignalProcessingComplete(_context.ThreadId, cancellationToken: ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("SignalProcessingComplete canceled after mode switch (workflow) {ThreadId}", _context.ThreadId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to SignalProcessingComplete after mode switch (workflow) {ThreadId}", _context.ThreadId);
        }
    }

    private string GetWorkflowConfigurationStatus()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("WorkflowConfiguration: ");
        builder.Append($"AGENT_TYPE_NAME: {Environment.GetEnvironmentVariable("AGENT_TYPE_NAME")},");
        builder.Append($"SREAGENT_ALLOW_NON_SCANNER_RCA_POST: {Environment.GetEnvironmentVariable("SREAGENT_ALLOW_NON_SCANNER_RCA_POST")},");
        builder.Append($"IncidentManagement.ICMAPI.ReadOnly: {_incidentManagementSettings?.ICMAPI?.ReadOnly},");
        builder.Append($"IncidentManagement.AutomatedRCA.Enabled: {_incidentManagementSettings?.AutomatedRCA?.Enabled},");
        builder.Append($"IncidentManagement.AutomatedRCA.WebBaseUrl: {_incidentManagementSettings?.AutomatedRCA?.WebBaseUrl}");
        builder.Append($"Experimental.EnableModeSwitch: {_coreSettings.Experimental.EnableModeSwitch}");
        return builder.ToString();
    }

    /// <summary>
    /// Change AgentContext state safely (mirrors ReasoningLoop.ChangeAgentContextStateAsync).
    /// </summary>
    private async Task ChangeAgentContextStateAsync(ContextStateEnum newState)
    {
        var oldState = _context.ContextState;
        if (oldState == newState)
        {
            return; // no-op
        }
        try
        {
            _context = _context with { ContextState = newState };
            await _threadRepository.UpdateAgentContextAsync(_context);
            _logger.LogInternalInformation("Workflow context state changed {OldState} -> {NewState} (thread {ThreadId})", oldState, newState, _context.ThreadId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to change workflow context state {OldState} -> {NewState} (thread {ThreadId})", oldState, newState, _context.ThreadId);
        }
    }

    /// <summary>
    /// Execute an agent with conversation history (for parameter extraction)
    /// </summary>
    private async Task<WorkflowActivityAgentOutput?> ExecuteAgentWithHistory(
        Agent<AgentContext> agent,
        CancellationToken cancellationToken)
    {

        try
        {
            // Create RunHooks for tool resolution
            var runHooks = CreateRunHooks();

            await SetAmbientThreadContextAsync();

            // Use the existing chat history for parameter extraction
            var result = await Runner.RunAsync(
                agent,
                _chatHistory,
                new RunConfig
                {
                    ChatClient = _chatClientProvider.GeneralPurposeModel,
                    LoggerFactory = _loggerFactory,
                    SkillRegistry = _skillRegistry
                },
                context: _context,
                hooks: runHooks,
                cancellationToken: cancellationToken);

            if (result.Output is WorkflowActivityLLMOutput workflowOutput)
            {
                return WorkflowActivityAgentOutput.FromLLMOutput(workflowOutput, reasoningScratchPad: "", notifyUserMessage: "", state: "", stateExplanation: "");
            }

            _logger.LogInternalWarning($"Agent {agent.Name} did not return WorkflowActivityAgentOutput");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing agent {agent.Name} with history");
            return null;
        }
    }

    /// <summary>
    /// Execute an agent with workflow parameters (without conversation history)
    /// </summary>
    private async Task<WorkflowActivityAgentOutput?> ExecuteAgentWithParameters(
        Agent<AgentContext> agent,
        WorkflowExecutionContext executionContext,
        CancellationToken cancellationToken)
    {

        try
        {
            // Create RunHooks for tool resolution
            var runHooks = CreateRunHooks();

            await SetAmbientThreadContextAsync();

            // Backfill IncidentId from parameters if context property not yet set
            if (string.IsNullOrWhiteSpace(executionContext.IncidentId))
            {
                var pidFromParams = executionContext.AccumulatedParameters.GetString("IncidentId");
                if (!string.IsNullOrWhiteSpace(pidFromParams))
                {
                    executionContext.IncidentId = pidFromParams;
                }
            }

            // Ensure IncidentId exists in parameters if known on the context
            if (!string.IsNullOrWhiteSpace(executionContext.IncidentId) &&
                string.IsNullOrWhiteSpace(executionContext.AccumulatedParameters.GetString("IncidentId")))
            {
                executionContext.AccumulatedParameters.SetString("IncidentId", executionContext.IncidentId!);
            }

            // Create a minimal message with workflow parameters
            var parametersJson = JsonSerializer.Serialize(executionContext.AccumulatedParameters.Values);
            var parameterMessage = new ChatMessage(ChatRole.User,
                $"Execute your analysis with the following parameters: {parametersJson}");

            var messages = new List<ChatMessage> { parameterMessage };

            var result = await Runner.RunAsync(
                agent,
                messages,
                new RunConfig
                {
                    ChatClient = _chatClientProvider.GeneralPurposeModel,
                    LoggerFactory = _loggerFactory,
                    SkillRegistry = _skillRegistry
                },
                context: _context,
                hooks: runHooks,
                cancellationToken: cancellationToken);

            if (result.Output is WorkflowActivityLLMOutput workflowOutput)
            {
                return WorkflowActivityAgentOutput.FromLLMOutput(workflowOutput, reasoningScratchPad: "", notifyUserMessage: "", state: "", stateExplanation: "");
            }

            _logger.LogInternalWarning($"Agent {agent.Name} did not return WorkflowActivityAgentOutput");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing agent {agent.Name} with parameters");
            throw;
        }
    }

    /// <summary>
    /// Recursively execute a branch of agents, each maintaining its own parameter context
    /// </summary>
    private async Task ExecuteAgentBranchAsync(
        string agentName,
        WorkflowExecutionContext branchContext,
        HashSet<string> executedAgents,
        CancellationToken cancellationToken)
    {

        try
        {
            // Check if this agent was already executed in this branch
            if (executedAgents.Contains(agentName))
            {
                _logger.LogInternalWarning($"Agent {agentName} already executed in this branch, skipping");
                return;
            }

            // Check execution limits
            if (branchContext.ExecutedAgentCount >= branchContext.MaxAgentCount)
            {
                _logger.LogInternalWarning($"Maximum agent count reached in branch, stopping execution at {agentName}");
                return;
            }

            _logger.LogInternalInformation($"Executing agent: {agentName} in branch context {branchContext.WorkflowId}");

            // Mark as executed and update counters
            executedAgents.Add(agentName);
            branchContext.ExecutedAgentCount++;
            branchContext.StepNumber++;

            var agent = _agentProvider.GetAgent(agentName, _context.ThreadId.ToString());
            var result = await ExecuteAgentWithParameters(agent, branchContext, cancellationToken);

            if (result != null)
            {
                // Store result with agent name and branch context
                result.AgentName = agentName;
                result.ExecutionContext = branchContext;
                _executionResults[agentName] = result;

                // Parse the result parameters
                result.ParseParameters();

                // If this result includes an IncidentId, propagate into the branch context property
                var returnedIncidentId = result.ParsedParameters.TryGetValue("IncidentId", out var pid) ? pid : null;
                if (!string.IsNullOrWhiteSpace(returnedIncidentId))
                {
                    branchContext.IncidentId = returnedIncidentId;
                }

                // Determine next steps using result.NextSteps or agent's NextAgentMappings
                var nextSteps = result.NextSteps ?? new List<string>();

                // If agent has NextAgentMappings defined in YAML, use them to determine next steps
                if (agent.NextAgentMappings?.Count > 0)
                {
                    foreach (var mapping in agent.NextAgentMappings)
                    {
                        // Simple condition matching - could be enhanced for more complex logic
                        if (nextSteps.Any(step => step.Contains(mapping.Condition)))
                        {
                            nextSteps.AddRange(mapping.NextAgents);
                        }
                    }
                }

                // Recursively execute next steps with updated branch contexts
                if (nextSteps?.Count > 0)
                {
                    _logger.LogInternalInformation($"Agent {agentName} specified {nextSteps.Count} next steps");

                    foreach (var nextStep in nextSteps)
                    {
                        if (!string.IsNullOrEmpty(nextStep) && !executedAgents.Contains(nextStep))
                        {
                            // Create a new context for the next step (inherits current branch parameters)
                            var nextStepContext = branchContext.Clone();

                            // NOW merge parameters into the CLONED context
                            foreach (var param in result.ParsedParameters)
                            {
                                nextStepContext.AccumulatedParameters.SetString(param.Key, param.Value);
                            }

                            // Propagate IncidentId to cloned context as well
                            if (!string.IsNullOrWhiteSpace(returnedIncidentId))
                            {
                                nextStepContext.IncidentId = returnedIncidentId;
                            }

                            // Recursively execute the next step with the updated cloned context
                            await ExecuteAgentBranchAsync(nextStep, nextStepContext, new HashSet<string>(executedAgents), cancellationToken);
                        }
                    }
                }
                else
                {
                    _logger.LogInternalInformation($"Agent {agentName} completed with no next steps - branch terminated");
                }
            }
            else
            {
                _logger.LogInternalWarning($"Agent {agentName} returned null result");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing agent branch {agentName}");
            throw;
        }
    }

    /// <summary>
    /// Summarize all execution results and post to thread
    /// </summary>
    private async Task SummarizeAndPostResults(Agent<AgentContext> orchestratorAgent, CancellationToken cancellationToken)
    {
        // Ensure AgentChatHistory exists (lazy create) so we can append reasoning messages
        var existingHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
        if (existingHistory == null)
        {
            var seedIds = _lastUserReasoningMessageId.HasValue ? new List<Guid> { _lastUserReasoningMessageId.Value } : new List<Guid>();
            existingHistory = await _threadRepository.CreateAgentChatHistoryAsync(new AgentChatHistory(_context.Id, seedIds)
            {
                LatestUserMessageId = _lastUserReasoningMessageId ?? Guid.Empty
            });
        }

        // Persist extracted parameters reasoning message (once) BEFORE summary so user sees parameters first
        if (!_parametersPersisted && !string.IsNullOrWhiteSpace(_canonicalExtractedParametersJson))
        {
            try
            {
                var paramChat = new ChatMessage(ChatRole.Assistant, $"Extracted parameters (canonical): {_canonicalExtractedParametersJson}");
                var reasoning = new ReasoningMessage(
                    Id: Guid.NewGuid(),
                    AgentContextId: _context.Id,
                    Role: ReasoningMessageRoleEnum.Assistant,
                    SerializedChatMessage: JsonSerializer.Serialize(paramChat));
                await _threadRepository.CreateReasoningMessageAsync(reasoning);
                if (existingHistory != null)
                {
                    await _threadRepository.AddReasoningMessagesToChatHistoryAsync(existingHistory, reasoning);
                }
                _parametersPersisted = true;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to persist extracted parameters reasoning message");
            }
        }

        // Build and persist execution digest reasoning message once
        if (!_digestPersisted)
        {
            try
            {
                var digest = BuildExecutionDigest();
                if (!string.IsNullOrWhiteSpace(digest))
                {
                    var digestChat = new ChatMessage(ChatRole.Assistant, $"Agent Execution Digest:\n{digest}");
                    var digestReasoning = new ReasoningMessage(
                        Id: Guid.NewGuid(),
                        AgentContextId: _context.Id,
                        Role: ReasoningMessageRoleEnum.Assistant,
                        SerializedChatMessage: JsonSerializer.Serialize(digestChat));
                    await _threadRepository.CreateReasoningMessageAsync(digestReasoning);
                    if (existingHistory != null)
                    {
                        await _threadRepository.AddReasoningMessagesToChatHistoryAsync(existingHistory, digestReasoning);
                    }
                    _digestPersisted = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to persist execution digest reasoning message");
            }
        }

        try
        {
            var summaryPrompt = orchestratorAgent.ResultSummarizationPrompt;
            if (string.IsNullOrEmpty(summaryPrompt))
            {
                _logger.LogInternalWarning("No result summarization prompt found, using default");
                summaryPrompt = @"
Based on the following analysis results from multiple specialized agents, provide a comprehensive summary:

{results}

Please consolidate the findings, identify key insights, and provide actionable recommendations.
";
            }

            // Build results summary
            var resultsBuilder = new StringBuilder();
            if (_executionResults.Count == 0)
            {
                resultsBuilder.AppendLine("No analysis results were produced by any activity agents.");
            }
            else
            {
                foreach (var kvp in _executionResults)
                {
                    resultsBuilder.AppendLine($"## Agent: {kvp.Key}");
                    resultsBuilder.AppendLine($"Analysis: {kvp.Value.Analysis}");
                    if (!string.IsNullOrWhiteSpace(kvp.Value.State))
                    {
                        resultsBuilder.AppendLine($"State: {kvp.Value.State}");
                    }
                    if (!string.IsNullOrWhiteSpace(kvp.Value.Parameters))
                    {
                        resultsBuilder.AppendLine($"Parameters: {kvp.Value.Parameters}");
                    }
                    if (kvp.Value.GeneratedAt != default)
                    {
                        resultsBuilder.AppendLine($"GeneratedAt: {kvp.Value.GeneratedAt:O}");
                    }
                    resultsBuilder.AppendLine();
                }
            }

            // Compose messages: if prompt contains {results}, replace for compatibility; otherwise inject results as separate context
            var summaryMessages = new List<ChatMessage>();
            if (summaryPrompt.Contains("{results}", StringComparison.OrdinalIgnoreCase))
            {
                var finalPrompt = summaryPrompt.Replace("{results}", resultsBuilder.ToString(), StringComparison.OrdinalIgnoreCase);
                summaryMessages.Add(new ChatMessage(ChatRole.System, finalPrompt));
            }
            else
            {
                summaryMessages.Add(new ChatMessage(ChatRole.System, summaryPrompt));
                summaryMessages.Add(new ChatMessage(ChatRole.User, "Here are the agent analysis results to summarize:\n\n" + resultsBuilder.ToString()));
            }

            // Resolve incidentId from thread and choose default tag based on orchestrator agent
            string? incidentId = null;
            try
            {
                var thread = await _threadRepository.GetThreadAsync(_context.ThreadId);
                incidentId = thread?.Status?.IncidentStatus?.IncidentId;
            }
            catch { }

            // Fallback to any IncidentId present in execution contexts/parameters
            if (string.IsNullOrWhiteSpace(incidentId))
            {
                incidentId = _executionResults.Values
                    .Select(v => v.ExecutionContext?.IncidentId
                                 ?? v.ExecutionContext?.AccumulatedParameters.GetString("IncidentId"))
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            }

            var automatedRcaSettings = _incidentManagementSettings?.AutomatedRCA;
            var defaultTag = AutomatedRcaConfigurationHelper.ResolveResultTag(automatedRcaSettings, orchestratorAgent.Name);

            // Only when scanner-origin (set by IcmScanner) and incidentId resolvable, inject tool and instruction
            var tools = new List<AITool>();
            var allowNonScannerPost = false;
            try
            {
                var env = Environment.GetEnvironmentVariable("SREAGENT_ALLOW_NON_SCANNER_RCA_POST");
                allowNonScannerPost = !string.IsNullOrWhiteSpace(env) && (env.Equals("1", StringComparison.OrdinalIgnoreCase) || env.Equals("true", StringComparison.OrdinalIgnoreCase) || env.Equals("yes", StringComparison.OrdinalIgnoreCase));
            }
            catch { }

            if ((IncidentProcessingContext.IsScannerOrigin || allowNonScannerPost) && !string.IsNullOrWhiteSpace(incidentId))
            {
                try
                {
                    var postTool = _toolFactory.GetTool("PostIcmRcaSummary", _context.ThreadId, orchestratorAgent);
                    if (postTool != null)
                    {
                        tools.Add(postTool);
                    }
                }
                catch { /* Tool might not be registered; proceed without it */ }

                if (tools.Count > 0)
                {
                    // Merge tool instruction into the first System (or User) message, instead of adding another System message
                    var toolInstruction =
                        $"After you generate the summary, call the tool PostIcmRcaSummary with parameters: incidentId={incidentId}, tag={defaultTag}, summary=<the exact summary text you generated>. Then return the summary.";

                    var sysIdx = summaryMessages.FindIndex(m => m.Role == ChatRole.System);
                    if (sysIdx >= 0)
                    {
                        var merged = summaryMessages[sysIdx].Text ?? string.Empty;
                        merged = string.IsNullOrEmpty(merged) ? toolInstruction : $"{merged}\n\n{toolInstruction}";
                        summaryMessages[sysIdx] = new ChatMessage(ChatRole.System, merged);
                    }
                    else
                    {
                        var userIdx = summaryMessages.FindIndex(m => m.Role == ChatRole.User);
                        if (userIdx >= 0)
                        {
                            var merged = summaryMessages[userIdx].Text ?? string.Empty;
                            merged = string.IsNullOrEmpty(merged) ? toolInstruction : $"{merged}\n\n{toolInstruction}";
                            summaryMessages[userIdx] = new ChatMessage(ChatRole.User, merged);
                        }
                        else
                        {
                            // Fallback: if neither System nor User exists (unlikely), add one System message
                            summaryMessages.Add(new ChatMessage(ChatRole.System, toolInstruction));
                        }
                    }
                }
            }

            var chatOptions = new ChatOptions { Tools = tools.Count > 0 ? tools : null };
            if (tools.OfType<AIFunction>().Any(f => string.Equals(f.Name, "PostIcmRcaSummary", StringComparison.OrdinalIgnoreCase)))
            {
                // Force the model to call the specified tool when present
                chatOptions.ToolMode = ChatToolMode.RequireSpecific("PostIcmRcaSummary");
            }

            var threadLinkInfo = AutomatedRcaConfigurationHelper.BuildThreadLink(automatedRcaSettings, _context.ThreadId);
            var threadLink = threadLinkInfo.Link;
            var isLocal = threadLinkInfo.IsLocal;

            var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(summaryMessages, options: chatOptions, cancellationToken: cancellationToken);

            // The first response will likely be a tool-call; get the final summary after tool execution
            string? finalSummaryFromToolFlow = null;
            if (tools.Count > 0 && (IncidentProcessingContext.IsScannerOrigin || allowNonScannerPost) && !string.IsNullOrWhiteSpace(incidentId))
            {
                finalSummaryFromToolFlow = await HandleToolCallsAsync(
                    response,
                    tools,
                    incidentId!,
                    defaultTag,
                    fallbackSummary: "Generated summary",
                    threadLink: threadLink,
                    promptContext: summaryMessages,
                    ct: cancellationToken);
            }

            var summaryText = !string.IsNullOrWhiteSpace(finalSummaryFromToolFlow)
                ? finalSummaryFromToolFlow!
                : (TryGetAssistantText(response) ?? "Unable to generate summary");

            // Add thread link for detailed view + conditional access note
            var accessNote = threadLinkInfo.AccessNote;
            var finalMessage = $"{summaryText}\n\n**Thread Details:** [View detailed conversation]({threadLink}){accessNote}";

            // Post summary to thread
            var summaryMessage = new ChatMessage(ChatRole.Assistant, finalMessage + $"Id: {_context.Id} ThreadId: {_context.ThreadId}");

            await PostAssistantMessageToThreadAsync(summaryMessage);

            _logger.LogInternalInformation("Workflow summary posted to thread successfully");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error summarizing workflow results");
            throw;
        }
    }

    private async Task PostAssistantMessageToThreadAsync(ChatMessage chatMessage)
    {
        try
        {
            _chatHistory.Add(chatMessage);

            var messageId = Guid.NewGuid();
            // Persist to repository
            var reasoningMessage = new ReasoningMessage(
                Id: messageId,
                AgentContextId: _context.Id,
                Role: ReasoningMessageRoleEnum.Assistant,
                SerializedChatMessage: JsonSerializer.Serialize(chatMessage));

            await _threadRepository.CreateReasoningMessageAsync(reasoningMessage);

            var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
            if (agentChatHistory != null)
            {
                await _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage);
            }

            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                _context.ThreadId,
                chatMessage,
                messageId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to persist reasoning message for workflow chat message");
        }
    }

    /// <summary>
    /// Build a concise execution digest summarizing each agent's key outputs.
    /// </summary>
    private string BuildExecutionDigest()
    {
        if (_executionResults.Count == 0)
        {
            return string.Empty;
        }

        // Preserve execution (insertion) order so parameter-dependent sequence remains visible.
        var sb = new StringBuilder();
        foreach (var result in _executionResults.Values)
        {
            try
            {
                var agent = result.AgentName ?? "(unknown)";
                // Parameters: parse JSON or show raw, but compress
                // TODO Trimming the Parameters and result.
                var paramSnippet = CompressJson(result.Parameters, 160);
                var analysisSnippet = FirstSentenceOrTrim(result.Analysis, 200);
                var state = string.IsNullOrWhiteSpace(result.State) ? "" : $" state={result.State}";
                // Check if ExecutionContext always has IncidentId. Only when we use IcmScanner to set it.
                var incident = result.ExecutionContext?.IncidentId;
                var incidentFragment = string.IsNullOrWhiteSpace(incident) ? "" : $" incident={incident}";
                // TODO debug and double check it it satisfies the requirement.
                sb.AppendLine($"- {agent}:{state}{incidentFragment} params={paramSnippet} analysis={analysisSnippet}");
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to add agent to execution digest");
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string FirstSentenceOrTrim(string? text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var span = text.AsSpan();
        var periodIndex = text.IndexOf('.') >= 0 ? text.IndexOf('.') : -1;
        var candidate = periodIndex >= 0 ? text.Substring(0, periodIndex + 1) : text;
        candidate = candidate.Trim();
        if (candidate.Length > maxLen)
        {
            candidate = candidate.Substring(0, Math.Min(candidate.Length, maxLen));
        }
        return candidate.Replace('\n', ' ').Replace("  ", " ").Trim();
    }

    private static string CompressJson(string? json, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var compact = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
            if (compact.Length > maxLen)
            {
                return compact.Substring(0, maxLen) + "…";
            }
            return compact;
        }
        catch
        {
            var trimmed = json.Trim();
            return trimmed.Length > maxLen ? trimmed.Substring(0, maxLen) + "…" : trimmed;
        }
    }

    /// <summary>
    /// Create RunHooks for workflow agent execution (complete implementation matching ReasoningLoop)
    /// </summary>
    private RunHooks<AgentContext> CreateRunHooks()
    {
        var hooks = new RunHooks<AgentContext>();

        hooks.ResolveFactoryTools += (context, agent, additionalToolNames) =>
        {
            List<AIFunction> tools = [];
            List<string> allToolNames = [.. agent.FactoryTools, .. additionalToolNames];

            foreach (var toolName in allToolNames.Distinct())
            {
                // Skip disabled tools (those that don't meet EnabledIf condition)
                if (_toolFactory.IsToolDisabled(toolName))
                {
                    _logger.LogInternalDebug("Skipping disabled tool {toolName} for agent {agentName}", toolName, agent.Name);
                    continue;
                }

                var tool = _toolFactory.GetTool(toolName, _context.ThreadId, agent);
                tools.Add(tool);
            }

            return Task.FromResult(tools);
        };

        hooks.AgentStart += (context, agent) =>
        {
            _currentAgentSpan?.End();
            _currentAgentSpan = null;

            _logger.LogInternalInformation("Workflow trace invoke agent: {AgentName}", agent.Name);
            _currentAgentSpan = _tracer.StartActiveSpan($"workflow.agent.{agent.Name}", SpanKind.Internal, _rootSpan);
            _currentAgentSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
            _currentAgentSpan.SetAttribute("workflow.agent_name", agent.Name);
            _currentAgentSpan.SetAttribute("workflow.operation", "InvokeAgent");

            return Task.CompletedTask;
        };

        hooks.AgentEnd += (context, agent, output) =>
        {
            _logger.LogInternalInformation("Workflow trace ending agent: {AgentName}", agent.Name);
            _currentAgentSpan?.End();
            _currentAgentSpan = null;
            return Task.CompletedTask;
        };

        hooks.Handoff += async (context, agent, handoffAgent, handoffReasoning) =>
        {
            _logger.LogInternalInformation("Workflow trace handoff from agent: {AgentName} to agent: {HandoffAgentName}", agent.Name, handoffAgent.Name);
            var handoffSpan = _tracer.StartSpan($"workflow.handoff", SpanKind.Internal, _currentAgentSpan);
            handoffSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
            handoffSpan.SetAttribute("workflow.operation", "Handoff");
            handoffSpan.SetAttribute("workflow.source_agent", agent.Name);
            handoffSpan.SetAttribute("workflow.target_agent", handoffAgent.Name);
            handoffSpan.SetAttribute("workflow.handoff_reasoning", handoffReasoning);
            handoffSpan.End();
            _currentAgentSpan?.End();

            // Update handoff chain (workflow orchestrator handles this differently)
            _context.AgentHandoffChain.Add(handoffAgent.Name);
            await _threadRepository.UpdateAgentContextAsync(_context);
        };

        hooks.ToolStart += async (context, agent, functionCall, tool, input) =>
        {
            _logger.LogInternalInformation("Workflow trace starting tool: {ToolName} for agent: {AgentName}", tool.Name, agent.Name);
            var currentToolSpan = _tracer.StartActiveSpan($"workflow.tool.{tool.Name}", SpanKind.Internal, _currentAgentSpan);
            currentToolSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
            currentToolSpan.SetAttribute("workflow.operation", "Tool");
            currentToolSpan.SetAttribute("workflow.agent_name", agent.Name);
            currentToolSpan.SetAttribute("workflow.tool_name", tool.Name);
            currentToolSpan.SetAttribute("workflow.tool_input", FormatToolArguments(input));
            currentToolSpan.SetAttribute("workflow.model_temperature", agent.Temperature.ToString());
            currentToolSpan.SetAttribute("workflow.tool_description", tool.Description);
            currentToolSpan.SetAttribute("workflow.call_id", functionCall.CallId);

            _toolSpans[functionCall.CallId] = currentToolSpan;

            // Stream auto tools to avoid missing them (manual tools are handled separately)
            if (tool.GetToolMode() == ToolMode.Auto)
            {
                var callId = ToolStatic.AsyncLocalFunctionCallId.Value;
                if (!string.IsNullOrEmpty(callId))
                {
                    _logger.LogInternalInformation("Workflow streaming auto tool call: {ToolName} with CallId: {CallId}", tool.Name, callId);
                    var toolCallMessageId = Guid.NewGuid();
                    await _outboundCommunicationService.AppendAgentToolCallMessage(_context.ThreadId, tool, toolCallMessageId, callId);

                    // Store the message ID for OnToolEnd to use
                    ToolStatic.AsyncLocalToolCallMessageId.Value = toolCallMessageId;
                }
            }
        };

        hooks.ToolEnd += async (context, agent, functionCallContent, tool, output) =>
        {
            _logger.LogInternalInformation("Workflow trace ending tool: {ToolName} for agent: {AgentName}", tool.Name, agent.Name);
            var currentToolSpan = _toolSpans.GetValueOrDefault(functionCallContent.CallId);
            currentToolSpan?.SetAttribute("workflow.tool_output", output?.ToString() ?? string.Empty);
            currentToolSpan?.End();

            _toolSpans.Remove(functionCallContent.CallId, out var _);

            // Stream auto tool results to complete the streaming flow
            if (tool.GetToolMode() == ToolMode.Auto)
            {
                var callId = ToolStatic.AsyncLocalFunctionCallId.Value;
                var toolCallMessageId = ToolStatic.AsyncLocalToolCallMessageId.Value;

                if (!string.IsNullOrEmpty(callId) && toolCallMessageId.HasValue)
                {
                    _logger.LogInternalInformation("Workflow streaming auto tool result: {ToolName} with CallId: {CallId}", tool.Name, callId);
                    var result = new FunctionResultContent(callId, output);
                    await _outboundCommunicationService.AppendAgentToolCallResult(_context.ThreadId, result, toolCallMessageId.Value);

                    // Clear the stored IDs for next tool
                    ToolStatic.AsyncLocalFunctionCallId.Value = null;
                    ToolStatic.AsyncLocalToolCallMessageId.Value = null;
                }
            }
        };

        hooks.ModelGenerationStart += (context, agent, messages, chatOptions) =>
        {
            _logger.LogInternalInformation("Workflow trace starting model generation for agent: {AgentName}", agent.Name);
            _currentGenerationSpan = _tracer.StartActiveSpan($"workflow.model_generation", SpanKind.Internal, _currentAgentSpan);
            _currentGenerationSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
            _currentGenerationSpan.SetAttribute("workflow.agent_name", agent.Name);
            _currentGenerationSpan.SetAttribute("workflow.operation", "ModelGeneration");
            _currentGenerationSpan.SetAttribute("workflow.model_input", FormatChatMessages(messages));

            return Task.CompletedTask;
        };

        hooks.ModelGenerationEnd += (context, agent, response) =>
        {
            _logger.LogInternalInformation("Workflow trace ending model generation for agent: {AgentName}", agent?.Name ?? "Unknown");
            _currentGenerationSpan?.SetAttribute("workflow.model_output", FormatChatMessages(response?.Messages ?? []));
            _currentGenerationSpan?.SetAttribute("workflow.model_input_tokens", response?.Usage?.InputTokenCount?.ToString() ?? string.Empty);
            _currentGenerationSpan?.SetAttribute("workflow.model_output_tokens", response?.Usage?.OutputTokenCount?.ToString() ?? string.Empty);
            _currentGenerationSpan?.SetAttribute("workflow.model_total_tokens", response?.Usage?.TotalTokenCount?.ToString() ?? string.Empty);
            _currentGenerationSpan?.SetAttribute("workflow.model_temperature", agent?.Temperature.ToString() ?? string.Empty);
            _currentGenerationSpan?.End();
            _currentGenerationSpan = null;

            return Task.CompletedTask;
        };

        hooks.SummarizerStart += (context, agent) =>
        {
            _logger.LogInternalInformation("Workflow trace starting Summarizer for agent: {AgentName}.", agent.Name);
            _currentSummarizerSpan = _tracer.StartSpan($"summarizer", SpanKind.Internal, _currentAgentSpan);
            _currentSummarizerSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
            _currentSummarizerSpan.SetAttribute("workflow.agent_name", agent.Name);
            _currentSummarizerSpan.SetAttribute("workflow.operation", TraceOperationName.Summarizer);

            return Task.CompletedTask;
        };

        hooks.SummarizerEnd += (context, agent, extractedUserIntent) =>
        {
            _logger.LogInternalInformation("Workflow trace ending Summarizer for agent: {AgentName}.", agent.Name);
            _currentSummarizerSpan?.SetAttribute("workflow.summarizer.extracted_user_query", extractedUserIntent);
            _currentSummarizerSpan?.End();
            _currentSummarizerSpan = null;

            return Task.CompletedTask;
        };

        hooks.CriticStart += (context, agent, currentTurn) =>
        {
            var maxTurns = agent.MaxReflectionCount;
            _logger.LogInternalInformation("Workflow trace starting Critic for agent: {AgentName}. Turn# {CurrentTurn}/{MaxTurns}", agent.Name, currentTurn, maxTurns);
            _currentCriticSpan = _tracer.StartSpan($"workflow.critic", SpanKind.Internal, _currentAgentSpan);
            _currentCriticSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
            _currentCriticSpan.SetAttribute("workflow.agent_name", agent.Name);
            _currentCriticSpan.SetAttribute("workflow.operation", TraceOperationName.Critic);
            _currentCriticSpan.SetAttribute("workflow.critic.turn_index", currentTurn.ToString());
            _currentCriticSpan.SetAttribute("workflow.critic.max_turns", maxTurns.ToString());
            _currentCriticSpan.SetAttribute("workflow.critic.reflection_note", agent.CustomReflectionNote);

            return Task.CompletedTask;
        };

        hooks.CriticEnd += (context, agent, userQuery, criticResult, wasApproved) =>
        {
            _logger.LogInternalInformation("Workflow trace ending Critic for agent: {AgentName}, Approved: {WasApproved}", agent.Name, wasApproved);
            _currentCriticSpan?.SetAttribute("workflow.critic.user_query", userQuery);
            _currentCriticSpan?.SetAttribute("workflow.critic.result", criticResult);
            _currentCriticSpan?.SetAttribute("workflow.critic.was_approved", wasApproved.ToString());
            _currentCriticSpan?.End();
            _currentCriticSpan = null;

            return Task.CompletedTask;
        };

        return hooks;
    }

    /// <summary>
    /// Format tool arguments for logging (similar to ReasoningLoop)
    /// </summary>
    private static string FormatToolArguments(IEnumerable<KeyValuePair<string, object?>>? input)
    {
        if (input == null)
        {
            return string.Empty;
        }

        try
        {
            var argsDict = input.ToDictionary(kv => kv.Key, kv => kv.Value);
            return JsonSerializer.Serialize(argsDict, new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 3 // Prevent deep object serialization
            });
        }
        catch
        {
            return string.Join(", ", input.Select(kv => $"{kv.Key}: {kv.Value?.ToString() ?? "null"}"));
        }
    }

    /// <summary>
    /// Format chat messages for logging (similar to ReasoningLoop)
    /// </summary>
    private static string FormatChatMessages(IEnumerable<ChatMessage> messages)
    {
        try
        {
            var messageList = messages.Take(5).Select(m => new
            {
                Role = m.Role.ToString(),
                Content = m.Text?.Length > 200 ? m.Text[..200] + "..." : m.Text ?? ""
            });
            return JsonSerializer.Serialize(messageList, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return $"MessageCount: {messages.Count()}";
        }
    }

    private static string? TryGetAssistantText(ChatResponse response)
    {
        try
        {
            return response?.GetMessage()?.Text;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> HandleToolCallsAsync(
        ChatResponse response,
        List<AITool> tools,
        string incidentId,
        string defaultTag,
        string fallbackSummary,
        string threadLink,
        IReadOnlyList<ChatMessage> promptContext,
        CancellationToken ct)
    {
        try
        {
            var calls = ExtractFunctionCalls(response);
            if (calls.Count == 0)
            {
                return null;
            }

            string? lastFinalText = null;

            foreach (var call in calls)
            {
                var func = ResolveFunctionByName(tools, call.Name);
                if (func is null)
                {
                    _logger.LogInternalWarning("Tool call requested unknown tool: {ToolName}", call.Name);
                    continue;
                }

                var args = ParseArguments(call);

                // Fill required params if the model omitted them
                if (!args.ContainsKey("incidentId") || args["incidentId"] is null || string.IsNullOrWhiteSpace(args["incidentId"]?.ToString()))
                {
                    args["incidentId"] = incidentId;
                }
                if (!args.ContainsKey("tag") || args["tag"] is null || string.IsNullOrWhiteSpace(args["tag"]?.ToString()))
                {
                    args["tag"] = defaultTag;
                }
                if (!args.ContainsKey("summary") || args["summary"] is null || string.IsNullOrWhiteSpace(args["summary"]?.ToString()))
                {
                    args["summary"] = fallbackSummary;
                }

                // Ensure plugin-posted content also includes the thread link
                var summaryArg = args["summary"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(threadLink) && !summaryArg.Contains(threadLink, StringComparison.OrdinalIgnoreCase))
                {
                    summaryArg = $"{summaryArg}\n\n**Thread Details:** [View detailed conversation]({threadLink})";
                    args["summary"] = summaryArg;
                }

                _logger.LogInternalInformation("Invoking tool from model call: {ToolName} with args: {Args}", func.Name, JsonSerializer.Serialize(args));

                Core.ToolStatic.AsyncLocalCancellationToken.Value = ct;

                var result = await func.InvokeAsync(new AIFunctionArguments(args), ct);

                // Follow-up: provide tool result back to the model with the original prompt context to finalize its reply
                try
                {
                    var assistant = response.GetMessage();

                    var aiContents = new List<AIContent>
                    {
                        new FunctionResultContent(call.CallId, result)
                    };

                    var followup = new List<ChatMessage>();
                    followup.AddRange(promptContext);     // original instruction + results context
                    followup.Add(assistant);              // assistant message that called the tool
                    followup.Add(new ChatMessage(ChatRole.Tool, aiContents)); // tool result

                    var followupResponse = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(followup, cancellationToken: ct);
                    var text = TryGetAssistantText(followupResponse);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lastFinalText = text;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Follow-up after tool execution failed.");
                }
            }

            return lastFinalText;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "HandleToolCallsAsync failed.");
            return null;
        }
    }

    private static List<FunctionCallContent> ExtractFunctionCalls(ChatResponse response)
    {
        var list = new List<FunctionCallContent>();
        try
        {
            var assistantMsg = response?.GetMessage();
            if (assistantMsg?.Contents != null)
            {
                list.AddRange(assistantMsg.Contents.OfType<FunctionCallContent>());
            }

            if (response?.Messages != null)
            {
                foreach (var m in response.Messages)
                {
                    if (m?.Contents != null)
                    {
                        list.AddRange(m.Contents.OfType<FunctionCallContent>());
                    }
                }
            }
        }
        catch { /* ignore */ }

        return list;
    }

    private static AIFunction? ResolveFunctionByName(IEnumerable<AITool> tools, string name)
    {
        return tools?.OfType<AIFunction>()
                     .FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object?> ParseArguments(FunctionCallContent call)
    {
        try
        {
            if (call.Arguments is IReadOnlyDictionary<string, object?> dict)
            {
                return new Dictionary<string, object?>(dict);
            }

            var raw = call.Arguments?.ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(raw!);
                if (parsed != null)
                {
                    return parsed;
                }
            }
        }
        catch { /* ignored */ }

        return new Dictionary<string, object?>();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // End all active spans
            _currentCriticSpan?.End();
            _currentGenerationSpan?.End();

            // End all tool spans
            foreach (var toolSpan in _toolSpans.Values)
            {
                toolSpan?.End();
            }
            _toolSpans.Clear();

            _currentAgentSpan?.End();
            _rootSpan?.End();

            _disposed = true;
        }
    }
}
