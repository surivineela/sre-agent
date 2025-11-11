using Agent.Core.Models.Api.v1;
using Agent.Framework;
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
        var runConfig = new RunConfig
        {
            ChatClient = host.Services.GetRequiredService<IChatClientProvider>().GeneralPurposeModel,
            LoggerFactory = host.Services.GetRequiredService<ILoggerFactory>(),
            SkillRegistry = host.Services.GetRequiredService<ISkillRegistry>(),
        };

        return new(
            host,
            host.Services.GetRequiredService<IAgentFactory<AgentContext>>(),
            host.Services.GetRequiredService<IToolFactory<AgentContext>>(),
            runConfig);
    }
}
