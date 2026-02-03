// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Framework.Hooks;
using Agent.Framework.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Evals;

public sealed record TestHost(
    IHost Host,
    IAgentFactory<AgentContext> AgentFactory,
    IToolFactory<AgentContext> ToolFactory,
    RunConfig RunConfig)
{
    public static TestHost Create(IHost host)
    {
        var chatClientProvider = host.Services.GetRequiredService<IChatClientProvider>();
        var runConfig = new RunConfig
        {
            ChatClient = chatClientProvider.EvalModel,
            LoggerFactory = host.Services.GetRequiredService<ILoggerFactory>(),
            SkillRegistry = host.Services.GetRequiredService<ISkillRegistry>(),
            AmbientContextProvider = DisabledAmbientContextProvider.Instance,
            ChatClientProvider = chatClientProvider,
            HookManager = host.Services.GetService<HookManager>()
        };

        return new(
            host,
            host.Services.GetRequiredService<IAgentFactory<AgentContext>>(),
            host.Services.GetRequiredService<IToolFactory<AgentContext>>(),
            runConfig);
    }
}
