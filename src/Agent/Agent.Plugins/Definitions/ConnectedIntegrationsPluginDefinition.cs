// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions;
public class ConnectedIntegrationsPluginDefinition
{
    private readonly IConnectedIntegrationsPlugin _plugin;

    public ConnectedIntegrationsPluginDefinition(IConnectedIntegrationsPlugin plugin)
    {
        _plugin = plugin;
    }

    [KernelFunction("get_all_active_integrations")]
    [Description("Returns a list of all currently active integrations along with their configuration details. These are external integrations that the agent connects to example: DashboardSettings/Azure Managed Grafana, Pager Duty,etc. Also gives instructions on how to configure")]
    public List<IntegrationInfo> GetAllActiveConnectedIntegrations()
    {
        return _plugin.GetAllActiveIntegrations();
    }
}
