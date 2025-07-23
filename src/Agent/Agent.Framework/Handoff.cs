// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class Handoff<TContext> : AIFunction where TContext : class
{
    public string AgentName { get; }

    public string TransferMessage { get; } = HandoffMessage;

    public const string HandoffMessage = "Handoff is complete. Analyze the current state of the conversation, think about the required next steps, and continue handling the task";

    #region AITool overrides

    public override string Name { get; }

    public override string Description { get; }

    #endregion

    #region AIFunction overrides

    public override JsonElement JsonSchema { get; }

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        // not to be called directly, handoffs will be intercepted by the run loop and handled separately
        throw new NotImplementedException();
    }

    #endregion

    public Func<RunContextWrapper<TContext>, Task<Agent<TContext>>> OnInvokeHandoff { get; }

    internal Handoff(
        string name,
        string description,
        string agentName,
        Func<RunContextWrapper<TContext>, Task<Agent<TContext>>> onInvokeHandoff
    )
    {
        Name = name;
        Description = description;
        AgentName = agentName;
        OnInvokeHandoff = onInvokeHandoff;

        JsonSchema = ConstructReasoningSchema(name, description);
    }

    public static string DefaultToolName(Agent<TContext> agent)
    {
        return $"transfer_to_{agent.Name.ToLower().Replace(" ", "_")}";
    }

    public static string DefaultToolDescription(Agent<TContext> agent)
    {
        return $"Handoff to the {agent.Name} agent to handle the request. {agent.HandoffDescription}";
    }

    public static Handoff<TContext> Create(
        Agent<TContext> agent,
        string? toolNameOverride = null,
        string? toolDescriptionOverride = null
    )
    {
        return new Handoff<TContext>(
            name: toolNameOverride ?? DefaultToolName(agent),
            description: toolDescriptionOverride ?? DefaultToolDescription(agent),
            agentName: agent.Name,
            onInvokeHandoff: (_) => Task.FromResult(agent)
        );
    }

    public static JsonElement ConstructReasoningSchema(string handoffMethodName, string handoffMethodDescription)
    {
        return JsonSerializer.SerializeToElement(new
        {
            title = handoffMethodName,
            description = handoffMethodDescription,
            type = "object",
            properties = new
            {
                subtask = new
                {
                    description =
                    """
                    1 line description of what specific task this agent must do.
                    Example: Check the open port of container app myapp.
                    """,
                    type = "string"
                },
                selectionReasoning = new
                {
                    description =
                    """
                    1 line description of why this specific agent was picked for that task.
                    Must clearly mention the agent capability that drove the selection.
                    Example: container_apps_agent is able to check container app configuration.
                    """,
                    type = "string"
                }
            },
            required = new[] { "subtask", "selectionReasoning" }
        });
    }
}
