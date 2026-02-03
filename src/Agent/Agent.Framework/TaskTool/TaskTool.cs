// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework.TaskTool;

/// <summary>
/// A tool that spawns subagents to perform specialized tasks like code exploration.
/// Multiple Task tool calls can run in parallel when the parent agent has AllowParallelToolCalls enabled.
/// </summary>
public class TaskTool<TContext> : AIFunction where TContext : class
{
    private readonly int _defaultMaxTurns;
    private readonly IAgentFactory<TContext>? _agentFactory;

    /// <summary>
    /// Gets or sets the run configuration for the subagent.
    /// Must be set before invoking the tool.
    /// </summary>
    public RunConfig? RunConfig { get; set; }

    /// <summary>
    /// Gets or sets the run hooks for the subagent.
    /// Must be set before invoking the tool.
    /// </summary>
    public RunHooks<TContext>? RunHooks { get; set; }

    /// <summary>
    /// Gets or sets the unique execution ID for this task.
    /// Set by the Runner before invocation to enable cancellation tracking.
    /// </summary>
    public string? ExecutionId { get; set; }

    public override string Name => "Task";

    public override string Description
    {
        get
        {
            var description = new System.Text.StringBuilder();
            description.AppendLine("Launch a subagent to handle complex, multi-step tasks autonomously.");
            description.AppendLine();
            description.AppendLine("Available subagent types:");

            // Built-in subagent types
            description.AppendLine("- Explore: Deeply analyzes codebase features by tracing execution paths, mapping architecture layers,");
            description.AppendLine("  understanding patterns and abstractions, and documenting dependencies. Use when you need to understand");
            description.AppendLine("  how a feature works, trace code flow, or map architecture before making changes.");
            description.AppendLine();
            description.AppendLine("- Plan: Designs feature architectures by analyzing existing codebase patterns and conventions, then");
            description.AppendLine("  providing comprehensive implementation blueprints with specific files to create/modify, component");
            description.AppendLine("  designs, data flows, and build sequences. Use for planning new features or significant changes.");
            description.AppendLine();
            description.AppendLine("- CodeReview: Reviews code for bugs, logic errors, security vulnerabilities, code quality issues,");
            description.AppendLine("  and adherence to project conventions. Uses confidence-based filtering (>=80) to report only");
            description.AppendLine("  high-priority issues that truly matter. Use for reviewing code changes before committing.");
            description.AppendLine();
            description.AppendLine("- KustoQuery: Executes KQL queries against Azure Data Explorer clusters and analyzes the results.");
            description.AppendLine("  Use when you need to investigate logs, metrics, or telemetry data. The agent can run queries,");
            description.AppendLine("  interpret results, identify patterns/anomalies, and provide actionable recommendations.");
            description.AppendLine();
            description.AppendLine("- Bash: Command execution specialist for running bash commands. Use this for git operations,");
            description.AppendLine("  command execution, and other terminal tasks.");

            // Add ExtendedAgents dynamically
            var extendedAgents = GetFilteredExtendedAgents();
            if (extendedAgents.Count > 0)
            {
                description.AppendLine();
                foreach (var agent in extendedAgents)
                {
                    var handoffDesc = agent.HandoffDescription!.GetOriginalText();
                    description.AppendLine($"- {agent.Name}: {handoffDesc}");
                }
            }

            description.AppendLine();
            description.AppendLine("You can call this tool multiple times in parallel to perform different tasks simultaneously.");
            description.AppendLine("Each subagent runs independently and returns its findings.");
            description.AppendLine();
            description.AppendLine("Example usage:");
            description.AppendLine("- Launch an Explore agent to \"trace the authentication flow from login to token generation\"");
            description.AppendLine("- Launch multiple Explore agents in parallel to investigate different parts of a feature");
            description.AppendLine("- Launch a Plan agent to \"design the architecture for adding a caching layer\"");
            description.AppendLine("- Launch a CodeReview agent to \"review the changes in src/Services/ for potential bugs\"");
            description.Append("- Launch a KustoQuery agent to \"analyze error rates in wawsprod over the last 24 hours\"");

            return description.ToString();
        }
    }

    public override JsonElement JsonSchema
    {
        get
        {
            var schema = new
            {
                type = "object",
                properties = new
                {
                    subagent_type = new
                    {
                        type = "string",
                        description = "The type of subagent to spawn. Can be a built-in type ('Explore', 'Plan', 'CodeReview', 'KustoQuery', 'Bash') or the name of any registered ExtendedAgent."
                    },
                    prompt = new
                    {
                        type = "string",
                        description = "The task for the subagent to perform. Be specific about what you want to understand or explore."
                    },
                    description = new
                    {
                        type = "string",
                        description = "A short (3-5 word) description of what this subagent will do, for tracking purposes."
                    },
                    max_turns = new
                    {
                        type = "integer",
                        description = "Optional maximum number of turns for the subagent. Defaults to 15.",
                        minimum = 1,
                        maximum = 50
                    }
                },
                required = new[] { "subagent_type", "prompt", "description" }
            };

            string jsonString = JsonSerializer.Serialize(schema);
            using var doc = JsonDocument.Parse(jsonString);
            return doc.RootElement.Clone();
        }
    }

    public TaskTool(int defaultMaxTurns = 15, IAgentFactory<TContext>? agentFactory = null)
    {
        _defaultMaxTurns = defaultMaxTurns;
        _agentFactory = agentFactory;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        // Parse arguments
        string? subagentTypeStr = null;
        string? prompt = null;
        string? description = null;
        int maxTurns = _defaultMaxTurns;

        foreach (var arg in arguments)
        {
            switch (arg.Key)
            {
                case "subagent_type":
                    subagentTypeStr = arg.Value?.ToString();
                    break;
                case "prompt":
                    prompt = arg.Value?.ToString();
                    break;
                case "description":
                    description = arg.Value?.ToString();
                    break;
                case "max_turns":
                    if (arg.Value is int turns)
                        maxTurns = turns;
                    else if (int.TryParse(arg.Value?.ToString(), out var parsedTurns))
                        maxTurns = parsedTurns;
                    break;
            }
        }

        // Validate required arguments
        if (string.IsNullOrWhiteSpace(subagentTypeStr))
        {
            return "Error: subagent_type is required. Available types: " + GetAvailableSubagentTypes();
        }

        // Try built-in subagent type first
        bool isBuiltIn = Enum.TryParse<SubAgentType>(subagentTypeStr, ignoreCase: true, out var builtInType);

        // If not built-in, check if it's an ExtendedAgent
        bool isExtendedAgent = false;
        Agent<TContext>? extendedAgent = null;

        if (!isBuiltIn && _agentFactory != null)
        {
            isExtendedAgent = _agentFactory.AgentExists(subagentTypeStr);
            if (isExtendedAgent)
            {
                try
                {
                    extendedAgent = _agentFactory.GetAgent(subagentTypeStr);
                }
                catch (KeyNotFoundException)
                {
                    isExtendedAgent = false;
                }
            }
        }

        if (!isBuiltIn && !isExtendedAgent)
        {
            return $"Error: Unknown subagent_type '{subagentTypeStr}'. Available types: {GetAvailableSubagentTypes()}";
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "Error: prompt is required. Provide a clear task for the subagent.";
        }

        if (RunConfig == null)
        {
            throw new InvalidOperationException("RunConfig was not set on TaskTool instance");
        }

        if (RunHooks == null)
        {
            throw new InvalidOperationException("RunHooks was not set on TaskTool instance");
        }

        // Register for cancellation tracking if ExecutionId is set
        var effectiveToken = cancellationToken;
        var executionId = ExecutionId ?? Guid.NewGuid().ToString("N");
        string agentTypeName = subagentTypeStr ?? "Unknown";

        try
        {
            // Register with cancellation registry to allow external cancellation
            effectiveToken = TaskToolCancellationRegistry.RegisterExecution(executionId, cancellationToken);

            // Create ephemeral agent for this task
            Agent<TContext> agent;
            IReadOnlyList<string> toolNames;

            if (isBuiltIn)
            {
                // Built-in subagent - use existing logic
                agentTypeName = builtInType.ToString();
                var agentName = $"task_{builtInType.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
                var systemPrompt = SubAgentPrompts.GetPrompt(builtInType);
                toolNames = SubAgentPrompts.GetTools(builtInType);

                agent = new Agent<TContext>(agentName)
                {
                    Instructions = systemPrompt,
                    FactoryTools = toolNames.ToList(),
                    AllowParallelToolCalls = true,
                    Temperature = 0.3f
                };
            }
            else
            {
                // ExtendedAgent - clone configuration from registered agent
                agentTypeName = extendedAgent!.Name;
                var agentName = $"task_{extendedAgent.Name.ToLowerInvariant()}_{Guid.NewGuid():N}";

                agent = new Agent<TContext>(agentName)
                {
                    Instructions = extendedAgent.Instructions,
                    FactoryTools = extendedAgent.FactoryTools.ToList(),
                    AllowParallelToolCalls = extendedAgent.AllowParallelToolCalls,
                    Temperature = extendedAgent.Temperature,
                    HandoffDescription = extendedAgent.HandoffDescription,
                    MaxReflectionCount = extendedAgent.MaxReflectionCount,
                    CustomReflectionNote = extendedAgent.CustomReflectionNote,
                    OutputType = extendedAgent.OutputType
                };

                toolNames = extendedAgent.FactoryTools;
            }

            // Set up hooks that resolve tools from the parent's tool factory
            var subagentHooks = new RunHooks<TContext>();
            subagentHooks.ResolveFactoryTools += async (context, subagent, additionalToolNames) =>
            {
                // Get tools from the parent hooks, but only for this subagent's allowed tools
                var allToolNames = toolNames.Concat(additionalToolNames ?? Enumerable.Empty<string>());
                var tools = await RunHooks.OnResolveFactoryTools(context, agent, allToolNames);

                // Filter to only auto tools (no manual/approval tools in subagent context)
                var autoTools = new List<AIFunction>();
                foreach (var tool in tools)
                {
                    // Skip the Task tool to prevent infinite recursion (subagents spawning subagents)
                    if (tool.Name == "Task")
                    {
                        continue;
                    }

                    if (tool.GetToolMode() == ToolMode.Auto)
                    {
                        autoTools.Add(tool);
                    }
                }

                return autoTools;
            };

            // Forward tool invocations to parent for streaming
            subagentHooks.ToolStart += async (context, subagent, functionCall, tool, arguments) =>
            {
                // Extract a brief description from the arguments if possible
                string? toolDescription = null;
                if (arguments != null)
                {
                    var firstArg = arguments.FirstOrDefault();
                    if (firstArg.Value != null)
                    {
                        var argStr = firstArg.Value.ToString();
                        toolDescription = argStr?.Length > 50 ? argStr.Substring(0, 50) + "..." : argStr;
                    }
                }

                await RunHooks.OnTaskToolInvocationStart(context, agent, executionId, tool.Name, toolDescription);
            };

            subagentHooks.ToolEnd += async (context, subagent, functionCall, tool, result) =>
            {
                var success = result != null && !result.ToString()?.StartsWith("Error", StringComparison.OrdinalIgnoreCase) == true;
                // Truncate output to 500 chars for streaming efficiency
                var output = result?.ToString();
                if (output != null && output.Length > 500)
                {
                    output = output.Substring(0, 500) + "...";
                }
                await RunHooks.OnTaskToolInvocationEnd(context, agent, executionId, tool.Name, success, output);
            };

            // Run the subagent with the linked cancellation token
            var chatMessages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.User, prompt)
            };

            // Set subagent context to suppress individual tool message streaming
            ToolStatic.AsyncLocalSubagentExecutionId.Value = executionId;

            var result = await Runner.RunAsync(
                startingAgent: agent,
                input: chatMessages,
                RunConfig,
                maxTurns: maxTurns,
                hooks: subagentHooks,
                cancellationToken: effectiveToken);

            // Extract the final response
            var lastAssistantMessage = result.NewItems.LastOrDefault(m => m.Role == ChatRole.Assistant);

            if (lastAssistantMessage == null)
            {
                return $"[{description ?? agentTypeName}] The subagent did not provide a response.";
            }

            return $"[{description ?? agentTypeName}]\n\n{lastAssistantMessage.Text}";
        }
        catch (OperationCanceledException) when (effectiveToken.IsCancellationRequested)
        {
            return $"[{description ?? agentTypeName}] Task was cancelled.";
        }
        finally
        {
            // Clear subagent context
            ToolStatic.AsyncLocalSubagentExecutionId.Value = null;
            // Always unregister to prevent memory leaks
            TaskToolCancellationRegistry.UnregisterExecution(executionId);
        }
    }

    private List<Agent<TContext>> GetFilteredExtendedAgents()
    {
        if (_agentFactory == null)
        {
            return [];
        }

        return _agentFactory.GetAllAgents()
            .Where(a => a.IsExtended)
            .Where(a => a.Name != "meta_agent")
            .Where(a => !string.IsNullOrWhiteSpace(a.HandoffDescription?.GetOriginalText()))
            .OrderBy(a => a.Name)
            .ToList();
    }

    private string GetAvailableSubagentTypes()
    {
        var builtInTypes = string.Join(", ", Enum.GetNames(typeof(SubAgentType)));

        var extendedAgents = GetFilteredExtendedAgents();

        if (extendedAgents.Count > 0)
        {
            return $"Built-in: {builtInTypes}; ExtendedAgents: {string.Join(", ", extendedAgents.Select(a => a.Name))}";
        }

        return builtInTypes;
    }

    /// <summary>
    /// Creates a new TaskTool instance.
    /// </summary>
    public static TaskTool<TContext> Create(int defaultMaxTurns = 15, IAgentFactory<TContext>? agentFactory = null)
    {
        return new TaskTool<TContext>(defaultMaxTurns, agentFactory);
    }
}
