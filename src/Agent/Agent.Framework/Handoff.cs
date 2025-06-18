// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class Handoff<TContext> : AIFunction where TContext : class
{
    public string AgentName { get; }

    public Type? InputType { get; }

    public string TransferMessage => HandoffMessage;

    public const string HandoffMessage = "Handoff is complete. Analyze the current state of the conversation, think about the required next steps, and continue handling the task";

    #region AITool overrides

    public override string Name { get; }

    public override string Description { get; }

    #endregion

    #region AIFunction overrides

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
        Type? inputType,
        Func<RunContextWrapper<TContext>, Task<Agent<TContext>>> onInvokeHandoff
    )
    {
        Name = name;
        Description = description;
        AgentName = agentName;
        InputType = inputType;
        OnInvokeHandoff = onInvokeHandoff;
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
            inputType: null,
            onInvokeHandoff: (_) => Task.FromResult(agent)
        );
    }

    // todo: support handoff with input

    // public static Handoff<TContext> Create<TInput>(
    //     Agent<TContext> agent,
    //     Func<RunContextWrapper<TContext>, TInput, Task> onHandoff,
    //     string? toolNameOverride = null,
    //     string? toolDescriptionOverride = null
    // )
    // {
    //     return new Handoff<TContext>(
    //         name: toolNameOverride ?? DefaultToolName(agent),
    //         description: toolDescriptionOverride ?? DefaultToolDescription(agent),
    //         agentName: agent.Name,
    //         inputType: typeof(TInput),
    //         onInvokeHandoff: async (context, input) =>
    //         {
    //             var typedContext = new RunContextWrapper<TContext>((TContext)context.Context!);
    //             var deserializedInput = JsonSerializer.Deserialize<TInput>(input) ?? throw new InvalidOperationException("Failed to deserialize input");
    //             await onHandoff(typedContext, deserializedInput);
    //             return agent;
    //         }
    //     );
    // }
}
