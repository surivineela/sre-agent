using System;
using System.ComponentModel;
using Agent.Framework;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class AgentControlFlowPluginDefinition
{
        [Description(@"Handoff the current context to the upper level agent. Do not use this tool when there are appropriate agents to handoff.")]
        public void HandoffBack()
        {
            throw new InvalidOperationException("HandoffBack is not exected to be called directly");
        }
}
