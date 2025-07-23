using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin(Category = ToolCategories.System)]
public class AgentControlFlowPluginDefinition
{
    [Description(
        @"Handoff the current context to the upper level agent when the current request is out of your current scope.
        Do not use this tool if there are other appropriate handoff tools available.
        Use this tool when you do not have any other tools or handoffs to properly handle the current task.")]
    [AgentTool(ToolMode.Manual)] // requires special handling outside the framework
    public string HandoffBack(
        [Description(
        """
        Explain in 2-3 lines what do you want the follow up agent to do?
        It must mention what you did (success or failure), and what must the next agent do.
        Example: I checked the container app metrics and found connection to SQL. Next agent must help to check the SQL logs further.
        Example: I was unable to check the kubernetes service. Next agent must try to do it or transfer to another agent which can.
        """)]
        string reasoning)
    {
        throw new InvalidOperationException("HandoffBack is not exected to be called directly");
    }
}
