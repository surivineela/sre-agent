// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework;
public class AgentAsTool<TContext> : AIFunction where TContext : class
{
    private readonly Agent<TContext> _targetAgent;
    private readonly int _maxTurns;
    private string _inputDescription { get; }

    public RunConfig? RunConfig { get; set; }
    public RunHooks<TContext>? RunHooks { get; set; }

    #region AITool overrides
    public override string Name { get; }
    public override string Description { get; }

    public override JsonElement JsonSchema
    {
        get
        {
            string description = _inputDescription ?? (_targetAgent.HandoffDescription != null
                ? _targetAgent.HandoffDescription.ToString()
                : $"Input for the {_targetAgent.Name} agent");

            // Create a simple schema as an anonymous object
            var schema = new
            {
                type = "object",
                properties = new
                {
                    input = new
                    {
                        type = "string",
                        description
                    }
                },
                required = new[] { "input" }
            };

            // Convert the anonymous object to a JsonElement
            string jsonString = JsonSerializer.Serialize(schema);
            using var doc = JsonDocument.Parse(jsonString);
            return doc.RootElement.Clone();
        }
    }

    #endregion

    internal AgentAsTool(
        Agent<TContext> targetAgent,
        string name,
        string description,
        int maxTurns,
        string inputDescription
    )
    {
        Name = name;
        Description = description;
        _targetAgent = targetAgent;
        _maxTurns = maxTurns;
        _inputDescription = inputDescription;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        string input = string.Empty; // override input schema? having a string input for now is fine. override jsonSchema property - string arg as input

        foreach (var arg in arguments)
        {
            if (arg.Key == "input" && arg.Value != null)
            {
                input = arg.Value.ToString() ?? string.Empty;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input argument is required and cannot be empty.");
        }

        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, input)
        };

        if (RunConfig == null)
        {
            throw new InvalidOperationException("RunConfig was not set on AgentAsTool instance");
        }

        if (RunHooks == null)
        {
            throw new InvalidOperationException("RunHooks was not set on AgentAsTool instance");
        }

        var runHooks = new RunHooks<TContext>
        {
            ResolveFactoryTools = async (context, agent) =>
            {
                var tools = await RunHooks.ResolveFactoryTools(context, _targetAgent);
                // Validate and filter tools - only allow auto tools in agent-as-tool context
                var autoTools = new List<AIFunction>();

                foreach (var tool in tools)
                {
                    if (tool.GetToolMode() == ToolMode.Auto)
                    {
                        autoTools.Add(tool);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Manual tool '{tool.Name}' is not supported in agent-as-tool context. Agent-as-tool can only use auto tools that don't require user interaction.");
                    }
                }

                return autoTools;
            }
        };

        var result = await Runner.RunAsync(
            startingAgent: _targetAgent,
            input: chatMessages,
            RunConfig,
            maxTurns: _maxTurns,
            hooks: RunHooks,
            cancellationToken: cancellationToken);

        var lastAssistantMessage = result.NewItems.LastOrDefault(m => m.Role == ChatRole.Assistant);

        if (lastAssistantMessage == null)
        {
            return "The agent did not provide a response";
        }

        return lastAssistantMessage.Text;
    }

    public static string DefaultToolName(Agent<TContext> agent)
    {
        return $"{agent.Name.ToLower().Replace(" ", "_").Replace("[^a-zA-Z0-9]", "_")}";
    }

    public static string DefaultToolDescription(Agent<TContext> agent)
    {
        return $"Use the {agent.Name} agent to process the input and get a response. Provide your query or instructions as input.";
    }

    public static AgentAsTool<TContext> Create(
        Agent<TContext> agent,
        string inputDescription,
        string? toolNameOverride = null,
        string? toolDescriptionOverride = null,
        int maxTurns = 10)
    {
        return new AgentAsTool<TContext>(
            targetAgent: agent,
            name: toolNameOverride ?? DefaultToolName(agent),
            description: toolDescriptionOverride ?? DefaultToolDescription(agent),
            maxTurns: maxTurns,
            inputDescription: inputDescription
        );
    }

}
