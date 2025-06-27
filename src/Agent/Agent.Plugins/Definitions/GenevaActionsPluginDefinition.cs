using System;
using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins;

[AgentToolPlugin(IsFirstPartyOnly = true)]
public class GenevaActionsPluginDefinition
{
    private readonly IGenevaActionsPlugin _genevaActionsPlugin;
    public GenevaActionsPluginDefinition(IGenevaActionsPlugin plugin)
    {
        _genevaActionsPlugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Fetch the list of input parameters needed to execute a geneva action. Always use this tool before executing a geneva action.")]
    public Task<string> ListInputParametersForGenevaAction(string actionName)
    {
        return _genevaActionsPlugin.ListInputParametersForGenevaAction(actionName);
    }

    [AgentTool(ToolMode.Manual)]
    [Description("Execute a geneva action for a specific incident with action name, and input parameters.\nIf Geneva Action execution fails due to incorrect parameters, then correct the parameters and try again.")]
    public Task<string> ExecuteGenevaAction(string incidentId, string actionName, Dictionary<string, string> inputParameters)
    {
        return _genevaActionsPlugin.ExecuteGenevaAction(incidentId, actionName, inputParameters);
    }
}
