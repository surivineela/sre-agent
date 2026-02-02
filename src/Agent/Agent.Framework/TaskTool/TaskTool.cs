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

    public override string Description => """
        Launch a subagent to handle complex, multi-step tasks autonomously.

        Available subagent types:
        - Explore: Deeply analyzes codebase features by tracing execution paths, mapping architecture layers,
          understanding patterns and abstractions, and documenting dependencies. Use when you need to understand
          how a feature works, trace code flow, or map architecture before making changes.

        - Plan: Designs feature architectures by analyzing existing codebase patterns and conventions, then
          providing comprehensive implementation blueprints with specific files to create/modify, component
          designs, data flows, and build sequences. Use for planning new features or significant changes.

        - CodeReview: Reviews code for bugs, logic errors, security vulnerabilities, code quality issues,
          and adherence to project conventions. Uses confidence-based filtering (>=80) to report only
          high-priority issues that truly matter. Use for reviewing code changes before committing.

        - KustoQuery: Executes KQL queries against Azure Data Explorer clusters and analyzes the results.
          Use when you need to investigate logs, metrics, or telemetry data. The agent can run queries,
          interpret results, identify patterns/anomalies, and provide actionable recommendations.

        You can call this tool multiple times in parallel to perform different tasks simultaneously.
        Each subagent runs independently and returns its findings.

        Example usage:
        - Launch an Explore agent to "trace the authentication flow from login to token generation"
        - Launch multiple Explore agents in parallel to investigate different parts of a feature
        - Launch a Plan agent to "design the architecture for adding a caching layer"
        - Launch a CodeReview agent to "review the changes in src/Services/ for potential bugs"
        - Launch a KustoQuery agent to "analyze error rates in wawsprod over the last 24 hours"
        """;

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
                        @enum = Enum.GetNames(typeof(SubAgentType)),
                        description = "The type of subagent to spawn: 'Explore' for code exploration, 'Plan' for architecture design, 'CodeReview' for reviewing code quality and bugs."
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

    public TaskTool(int defaultMaxTurns = 15)
    {
        _defaultMaxTurns = defaultMaxTurns;
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
            return "Error: subagent_type is required. Available types: " + string.Join(", ", Enum.GetNames(typeof(SubAgentType)));
        }

        if (!Enum.TryParse<SubAgentType>(subagentTypeStr, ignoreCase: true, out var subagentType))
        {
            return $"Error: Unknown subagent_type '{subagentTypeStr}'. Available types: " + string.Join(", ", Enum.GetNames(typeof(SubAgentType)));
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

        try
        {
            // Register with cancellation registry to allow external cancellation
            effectiveToken = TaskToolCancellationRegistry.RegisterExecution(executionId, cancellationToken);

            // Create ephemeral agent for this task
            var agentName = $"task_{subagentType.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
            var systemPrompt = SubAgentPrompts.GetPrompt(subagentType);
            var toolNames = SubAgentPrompts.GetTools(subagentType);

            var agent = new Agent<TContext>(agentName)
            {
                Instructions = systemPrompt,
                FactoryTools = toolNames.ToList(),
                AllowParallelToolCalls = true,
                Temperature = 0.3f
            };

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
                return $"[{description ?? subagentType.ToString()}] The subagent did not provide a response.";
            }

            return $"[{description ?? subagentType.ToString()}]\n\n{lastAssistantMessage.Text}";
        }
        catch (OperationCanceledException) when (effectiveToken.IsCancellationRequested)
        {
            return $"[{description ?? subagentType.ToString()}] Task was cancelled.";
        }
        finally
        {
            // Clear subagent context
            ToolStatic.AsyncLocalSubagentExecutionId.Value = null;
            // Always unregister to prevent memory leaks
            TaskToolCancellationRegistry.UnregisterExecution(executionId);
        }
    }

    /// <summary>
    /// Creates a new TaskTool instance.
    /// </summary>
    public static TaskTool<TContext> Create(int defaultMaxTurns = 15)
    {
        return new TaskTool<TContext>(defaultMaxTurns);
    }
}
