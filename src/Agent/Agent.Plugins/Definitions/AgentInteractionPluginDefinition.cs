// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Core.Models;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.System)]
    public class AgentInteractionPluginDefinition
    {
        private readonly IAgentInteractionPlugin _agentInteractionPlugin;

        public AgentInteractionPluginDefinition(IAgentInteractionPlugin agentInteractionPlugin)
        {
            _agentInteractionPlugin = agentInteractionPlugin;
        }

        [Description("Share the analysis results of an agent-as-tool call with other participants in the conversation thread. " +
                     "This allows transparency about what other agents have analyzed and enables better collaboration.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> ShareAgentResult(
            [Description("The name of the agent that was called as a tool")] string calledAgentName,
            [Description("The complete analysis summary and findings from the called agent (NOT raw query results)")] string analysisSummary,
            [Description("Optional context about why this agent was called or what analysis was performed")] string? context = null)
        {
            return await _agentInteractionPlugin.ShareAgentResultAsync(calledAgentName, analysisSummary, context);
        }
    }
}
