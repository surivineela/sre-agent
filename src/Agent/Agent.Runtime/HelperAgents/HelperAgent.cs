// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Attributes;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.HelperAgents;

public abstract class HelperAgent
{
    protected Guid ThreadId = Guid.Empty;

    private HelperAgentInput? _input;

    protected List<AITool> Tools { get; set; } = [];

    protected IChatClient ChatClient { get; init; }

    protected IToolsRepository ToolsRepository { get; init; }

    protected HelperAgent(
        IChatClient chatClient,
        IToolsRepository toolsRepository)
    {
        ChatClient = chatClient;
        ToolsRepository = toolsRepository;
    }

    public virtual void Initialize(HelperAgentInput input, Guid threadId)
    {
        if (input.AgentType != GetType())
        {
            throw new InvalidOperationException($"Input type {input.GetType()} is not configured for use with helper agent: {GetType()}");
        }

        _input = input;
        ThreadId = threadId;

        var resolvedTools = ToolsRepository.ResolveTools(_input.ToolSignatures);

        // validate none of the tools require approval, those cannot be called from helper agents
        Tools.AddRange(resolvedTools.Where(x =>
        {
            if (x is not AIFunction func)
            {
                return false;
            }

            return func.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>() == null;
        }));
    }

    protected TInput GetInput<TInput>() where TInput : HelperAgentInput
    {
        if (_input == null || _input.AgentType != GetType())
        {
            throw new InvalidOperationException("Helper agent must be initialized");
        }

        return _input as TInput
            ?? throw new InvalidOperationException($"Helper agent was initialized with input of type {_input.GetType()}, expected type: {typeof(TInput)}");
    }
}
