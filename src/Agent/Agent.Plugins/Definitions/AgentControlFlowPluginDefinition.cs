using System.ComponentModel;
using Agent.Framework;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class AgentControlFlowPluginDefinition
{
    [Description(
        @"Handoff the current context to the upper level agent.
        Do not use this tool when there are other appropriate agents to handoff to.
        Use this tool when you do not have any other tools or handoffs to properly handle the current task.")]
    [AgentTool(ToolMode.Manual)] // requires special handling outside the framework
    public string HandoffBack()
    {
        throw new InvalidOperationException("HandoffBack is not exected to be called directly");
    }
}
