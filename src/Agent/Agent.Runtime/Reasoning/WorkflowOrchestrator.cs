// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Workflow;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Core;
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
    private readonly IChatClient _chatClient;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IThreadRepository _threadRepository;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private readonly AgentContext _context;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly Tracer _tracer;
    
    // Telemetry spans for workflow tracing
    private TelemetrySpan? _rootSpan;
    private TelemetrySpan? _currentAgentSpan;
    private TelemetrySpan? _currentToolSpan;
    private TelemetrySpan? _currentGenerationSpan;
    private TelemetrySpan? _currentCriticSpan;
    
    // In-memory storage for agent execution results
    private readonly Dictionary<string, WorkflowActivityAgentOutput> _executionResults = new();
    private readonly List<ChatMessage> _chatHistory = new();
    private bool _disposed = false;

    public WorkflowOrchestrator(
        ILoggerFactory loggerFactory,
        IChatClient chatClient,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IThreadRepository threadRepository,
        AgentContext context,
        IAgentFactory<AgentContext> agentFactory,
        IToolFactory<AgentContext> toolFactory,
        Tracer tracer)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<WorkflowOrchestrator>();
        _chatClient = chatClient;
        _outboundCommunicationService = outboundCommunicationService;
        _threadRepository = threadRepository;
        _context = context;
        _agentFactory = agentFactory;
        _toolFactory = toolFactory;
        _tracer = tracer;
        
        // Initialize root span for workflow execution
        _rootSpan = _tracer.StartSpan($"workflow.orchestrator.{_context.ThreadId}");
        _rootSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
        _rootSpan.SetAttribute("workflow.orchestrator", "WorkflowOrchestrator");
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
        _logger.LogInternalInformation("New user message received, starting workflow execution");
        
        // Add message to chat history
        _chatHistory.Add(msg);

        var messageId = Guid.NewGuid();
        // TODO Remove the Debug Part
        // Persist the message to the repository
        var reasoningMessage = new ReasoningMessage(
            Id: messageId,
            AgentContextId: _context.Id,
            Role: ReasoningMessageRoleEnum.User,
            SerializedChatMessage: JsonSerializer.Serialize(msg) + $"Debug Id: {_context.Id} MessageId: {messageId} ThreadId: {_context.ThreadId} ");
        
        await _threadRepository.CreateReasoningMessageAsync(reasoningMessage);
        
        var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
        if (agentChatHistory != null)
        {
            await _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage);
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
            
            // Set AsyncLocal for tool factory access
            Agent.Core.ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;
            
            var result = await Framework.Runner.RunWithHandoffDetectionAsync(
                routerAgent,
                _chatHistory,
                new RunConfig 
                { 
                    ChatClient = _chatClient, 
                    LoggerFactory = _loggerFactory 
                },
                context: _context,
                hooks: runHooks,
                cancellationToken: cancellationToken);
            
            if (result.HandoffDetected && !string.IsNullOrEmpty(result.HandoffTargetAgent))
            {
                _logger.LogInternalInformation($"Router agent handoff detected: {routerAgent.Name} -> {result.HandoffTargetAgent}");
                return result.HandoffTargetAgent;
            }
            
            _logger.LogInternalWarning($"Router agent {routerAgent.Name} completed without handoff");
            return null;
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
        
        try
        {
            _logger.LogInternalInformation("Starting workflow execution");
            
            // Get the current agent
            var currentAgentName = _context.AgentHandoffChain.LastOrDefault() ?? _context.CurrentAgent;
            if (string.IsNullOrEmpty(currentAgentName))
            {
                _logger.LogInternalError("No current agent found for workflow orchestration");
                return;
            }
            
            var currentAgent = _agentFactory.GetAgent(currentAgentName);
            if (currentAgent == null)
            {
                _logger.LogInternalError($"Agent {currentAgentName} not found");
                return;
            }
            
            // Check if this is a router agent (like rca_router_meta_agent)
            if (currentAgentName == "rca_router_meta_agent" || currentAgent.AgentType == Framework.Models.AgentType.Autonomous)
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
            
            if (!string.IsNullOrEmpty(parameterExtractionAgentName))
            {
                _logger.LogInternalInformation($"Executing parameter extraction agent: {parameterExtractionAgentName}");
                var parameterAgent = _agentFactory.GetAgent(parameterExtractionAgentName);
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
                }
            }
            
            // Step 2: Execute orchestration start agents recursively (each with its own context)
            var startAgents = orchestratorAgent.OrchestrationStartAgents;
            if (startAgents?.Count > 0)
            {
                _logger.LogInternalInformation($"Starting {startAgents.Count} independent orchestration branches from {currentAgentName}");
                
                // Execute each start agent as an independent branch with its own context
                var branchTasks = new List<Task>();
                
                for (int i = 0; i < startAgents.Count; i++)
                {
                    var startAgent = startAgents[i];
                    var branchContext = baseExecutionContext.Clone(); // Each branch gets its own context
                    branchContext.StepNumber = i + 1; // Unique step number for each branch
                    
                    _logger.LogInternalInformation($"Starting branch {i + 1}: {startAgent}");
                    
                    // Execute each branch independently
                    var branchTask = ExecuteAgentBranchAsync(startAgent, branchContext, new HashSet<string>(), cancellationToken);
                    branchTasks.Add(branchTask);
                }
                
                // Wait for all branches to complete
                await Task.WhenAll(branchTasks);
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
            
            // Set AsyncLocal for tool factory access
            Agent.Core.ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;
            
            // Use the existing chat history for parameter extraction
            var result = await Framework.Runner.RunAsync(
                agent,
                _chatHistory,
                new RunConfig 
                { 
                    ChatClient = _chatClient, 
                    LoggerFactory = _loggerFactory 
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
            
            // Set AsyncLocal for tool factory access
            Agent.Core.ToolStatic.AsyncLocalThreadId.Value = _context.ThreadId;
            
            // Create a minimal message with workflow parameters
            var parametersJson = JsonSerializer.Serialize(executionContext.AccumulatedParameters.Values);
            var parameterMessage = new ChatMessage(ChatRole.User, 
                $"Execute your analysis with the following parameters: {parametersJson}");
            
            var messages = new List<ChatMessage> { parameterMessage };
            
            var result = await Framework.Runner.RunAsync(
                agent,
                messages,
                new RunConfig 
                { 
                    ChatClient = _chatClient, 
                    LoggerFactory = _loggerFactory 
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
            return null;
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
            
            var agent = _agentFactory.GetAgent(agentName);
            var result = await ExecuteAgentWithParameters(agent, branchContext, cancellationToken);
            
            if (result != null)
            {
                // Store result with agent name and branch context
                result.AgentName = agentName;
                result.ExecutionContext = branchContext;
                _executionResults[agentName] = result;
                
                // Parse the result parameters
                result.ParseParameters();
                
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
            foreach (var kvp in _executionResults)
            {
                resultsBuilder.AppendLine($"## Agent: {kvp.Key}");
                resultsBuilder.AppendLine($"**Analysis:** {kvp.Value.Analysis}");
                resultsBuilder.AppendLine($"**State:** {kvp.Value.State}");
                resultsBuilder.AppendLine($"**Parameters:** {kvp.Value.Parameters}");
                resultsBuilder.AppendLine($"**Generated At:** {kvp.Value.GeneratedAt}");
                resultsBuilder.AppendLine();
            }
            
            var finalPrompt = summaryPrompt.Replace("{results}", resultsBuilder.ToString());
            
            // Generate summary using LLM
            var summaryMessages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, finalPrompt)
            };
            
            var response = await _chatClient.GetResponseAsync(summaryMessages, cancellationToken: cancellationToken);
            var summaryText = response.GetMessage().Text ?? "Unable to generate summary";
            
            // Add thread link for detailed view
            var threadLink = $"/static/#/views/activities/threads/{_context.ThreadId}";
            var finalMessage = $"{summaryText}\n\n**Thread Details:** [View detailed conversation]({threadLink})";
            
            // Post summary to thread
            var summaryMessage = new ChatMessage(ChatRole.Assistant, finalMessage + $"Id: {_context.Id} ThreadId: {_context.ThreadId}");
            _chatHistory.Add(summaryMessage);

            var messageId = Guid.NewGuid();
            // Persist to repository
            var reasoningMessage = new ReasoningMessage(
                Id: messageId,
                AgentContextId: _context.Id,
                Role: ReasoningMessageRoleEnum.Assistant,
                SerializedChatMessage: JsonSerializer.Serialize(summaryMessage));


            await _threadRepository.CreateReasoningMessageAsync(reasoningMessage);
            
            var agentChatHistory = await _threadRepository.GetAgentChatHistoryAsync(_context.Id);
            if (agentChatHistory != null)
            {
                await _threadRepository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage);
            }

            await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                _context.ThreadId, 
                string.Empty, 
                summaryMessage, 
                messageId);

            _logger.LogInternalInformation("Workflow summary posted to thread successfully");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error summarizing workflow results");
            throw;
        }
    }

    /// <summary>
    /// Create RunHooks for workflow agent execution (complete implementation matching ReasoningLoop)
    /// </summary>
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

                _logger.LogInternalInformation("Workflow trace invoke agent: {AgentName}", agent.Name);
                _currentAgentSpan = _tracer.StartActiveSpan($"workflow.agent.{agent.Name}", SpanKind.Internal, _rootSpan);
                _currentAgentSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
                _currentAgentSpan.SetAttribute("workflow.agent_name", agent.Name);
                _currentAgentSpan.SetAttribute("workflow.operation", "InvokeAgent");

                return Task.CompletedTask;
            },

            OnAgentEnd = (context, agent, output) =>
            {
                _logger.LogInternalInformation("Workflow trace ending agent: {AgentName}", agent.Name);
                _currentAgentSpan?.End();
                _currentAgentSpan = null;
                return Task.CompletedTask;
            },

            OnHandoff = async (context, agent, handoffAgent) =>
            {
                _logger.LogInternalInformation("Workflow trace handoff from agent: {AgentName} to agent: {HandoffAgentName}", agent.Name, handoffAgent.Name);
                _currentToolSpan = _tracer.StartSpan($"workflow.handoff", SpanKind.Internal, _currentAgentSpan);
                _currentToolSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
                _currentToolSpan.SetAttribute("workflow.operation", "Handoff");
                _currentToolSpan.SetAttribute("workflow.source_agent", agent.Name);
                _currentToolSpan.SetAttribute("workflow.target_agent", handoffAgent.Name);
                _currentToolSpan.End();
                _currentToolSpan = null;
                _currentAgentSpan?.End();
                
                // Update handoff chain (workflow orchestrator handles this differently)
                _context.AgentHandoffChain.Add(handoffAgent.Name);
                await _threadRepository.UpdateAgentContextAsync(_context);
            },

            OnToolStart = async (context, agent, tool, input) =>
            {
                _logger.LogInternalInformation("Workflow trace starting tool: {ToolName} for agent: {AgentName}", tool.Name, agent.Name);
                _currentToolSpan = _tracer.StartActiveSpan($"workflow.tool.{tool.Name}", SpanKind.Internal, _currentAgentSpan);
                _currentToolSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
                _currentToolSpan.SetAttribute("workflow.operation", "Tool");
                _currentToolSpan.SetAttribute("workflow.agent_name", agent.Name);
                _currentToolSpan.SetAttribute("workflow.tool_name", tool.Name);
                _currentToolSpan.SetAttribute("workflow.tool_input", FormatToolArguments(input));
                _currentToolSpan.SetAttribute("workflow.model_temperature", agent.Temperature.ToString());
                _currentToolSpan.SetAttribute("workflow.tool_description", tool.Description);


                // Stream auto tools to avoid missing them (manual tools are handled separately)
                if (((AIFunction)tool).GetToolMode() == ToolMode.Auto)
                {
                    var callId = Agent.Framework.ToolStatic.AsyncLocalFunctionCallId.Value;
                    if (!string.IsNullOrEmpty(callId))
                    {
                        _logger.LogInternalInformation("Workflow streaming auto tool call: {ToolName} with CallId: {CallId}", tool.Name, callId);
                        var toolCallMessageId = Guid.NewGuid();
                        await _outboundCommunicationService.AppendAgentToolCallMessage(_context.ThreadId, (AIFunction)tool, toolCallMessageId, callId);

                        // Store the message ID for OnToolEnd to use
                        Agent.Framework.ToolStatic.AsyncLocalToolCallMessageId.Value = toolCallMessageId;
                    }
                }
            },

            OnToolEnd = async (context, agent, tool, output) =>
            {
                _logger.LogInternalInformation("Workflow trace ending tool: {ToolName} for agent: {AgentName}", tool.Name, agent.Name);
                _currentToolSpan?.SetAttribute("workflow.tool_output", output?.ToString() ?? string.Empty);
                _currentToolSpan?.End();
                _currentToolSpan = null;

                // Stream auto tool results to complete the streaming flow
                if (((AIFunction)tool).GetToolMode() == ToolMode.Auto)
                {
                    var callId = Agent.Framework.ToolStatic.AsyncLocalFunctionCallId.Value;
                    var toolCallMessageId = Agent.Framework.ToolStatic.AsyncLocalToolCallMessageId.Value;

                    if (!string.IsNullOrEmpty(callId) && toolCallMessageId.HasValue)
                    {
                        _logger.LogInternalInformation("Workflow streaming auto tool result: {ToolName} with CallId: {CallId}", tool.Name, callId);
                        var result = new FunctionResultContent(callId, output);
                        await _outboundCommunicationService.AppendAgentToolCallResult(_context.ThreadId, result, toolCallMessageId.Value);

                        // Clear the stored IDs for next tool
                        Agent.Framework.ToolStatic.AsyncLocalFunctionCallId.Value = null;
                        Agent.Framework.ToolStatic.AsyncLocalToolCallMessageId.Value = null;
                    }
                }
            },

            OnModelGenerationStart = (context, agent, messages, chatOptions) =>
            {
                _logger.LogInternalInformation("Workflow trace starting model generation for agent: {AgentName}", agent.Name);
                _currentGenerationSpan = _tracer.StartActiveSpan($"workflow.model_generation", SpanKind.Internal, _currentAgentSpan);
                _currentGenerationSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
                _currentGenerationSpan.SetAttribute("workflow.agent_name", agent.Name);
                _currentGenerationSpan.SetAttribute("workflow.operation", "ModelGeneration");
                _currentGenerationSpan.SetAttribute("workflow.model_input", FormatChatMessages(messages));

                return Task.CompletedTask;
            },

            OnModelGenerationEnd = (context, agent, response) =>
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
            },

            OnCriticEnd = (context, agent, userQuery, criticResult, wasApproved) =>
            {
                _logger.LogInternalInformation("Workflow trace ending critic for agent: {AgentName}, Approved: {WasApproved}", agent.Name, wasApproved);
                _currentCriticSpan = _tracer.StartSpan($"workflow.critic", SpanKind.Internal, _currentAgentSpan);
                _currentCriticSpan.SetAttribute("workflow.thread_id", _context.ThreadId.ToString());
                _currentCriticSpan.SetAttribute("workflow.agent_name", agent.Name);
                _currentCriticSpan.SetAttribute("workflow.operation", "Critic");
                _currentCriticSpan.SetAttribute("workflow.critic.user_query", userQuery);
                _currentCriticSpan.SetAttribute("workflow.critic.result", criticResult);
                _currentCriticSpan.SetAttribute("workflow.critic.was_approved", wasApproved.ToString());
                _currentCriticSpan.End();
                _currentCriticSpan = null;

                return Task.CompletedTask;
            }
        };
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

    public void Dispose()
    {
        if (!_disposed)
        {
            // End all active spans
            _currentCriticSpan?.End();
            _currentGenerationSpan?.End();
            _currentToolSpan?.End();
            _currentAgentSpan?.End();
            _rootSpan?.End();
            
            _disposed = true;
        }
    }
}
