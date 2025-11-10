using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

public static class AgentExtensions
{
    public static ChatOptions GetChatOptions(
        this Agent<AgentContext> agent,
        TestHost host)
    {
        List<AITool> tools = [];
        tools.AddRange(agent.Tools);
        tools.AddRange(agent.FactoryTools
            .Select(ft => host.ToolFactory.GetTool(ft)));
        tools.AddRange(agent.Handoffs);

        return new ChatOptions
        {
            Tools = tools,
            ToolMode = agent.ChatToolMode,
            Temperature = agent.Temperature,
            AllowMultipleToolCalls = tools.Count > 0
                ? false // agent.AllowParallelToolCalls TODO: not supported yet
                : null // if there are no tools this value needs to be null, not false
        };
    }
}
