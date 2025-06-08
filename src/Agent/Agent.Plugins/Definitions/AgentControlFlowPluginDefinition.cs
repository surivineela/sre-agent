using System.ComponentModel;
using Agent.Framework;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class AgentControlFlowPluginDefinition
{
    [Description(
        @"Handoff the current context to the upper level agent when the current request is out of your current scope.
        Do not use this tool if there are other appropriate handoff tools available.
        Use this tool when you do not have any other tools or handoffs to properly handle the current task.")]
    [AgentTool(ToolMode.Manual)] // requires special handling outside the framework
    public string HandoffBack()
    {
        throw new InvalidOperationException("HandoffBack is not exected to be called directly");
    }
}
