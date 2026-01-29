// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin(Category = ToolCategories.AzureOperation)]
public class LinuxWebAppRuntimeStatusPluginDefinition
{
    private readonly ILinuxWebAppRuntimeStatusPlugin _linuxWebAppRuntimeStatusPlugin;

    public LinuxWebAppRuntimeStatusPluginDefinition(
        ILinuxWebAppRuntimeStatusPlugin linuxWebAppRuntimeStatusPlugin)
    {
        _linuxWebAppRuntimeStatusPlugin = linuxWebAppRuntimeStatusPlugin;
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Gets the current runtime status of the Linux Web App. If the site is failing to start, it provides more details on why the site is failing to startup.")]
    public async Task<string> GetLinuxWebAppRuntimeStatusAsync(
        [Description("The full Azure resource ID of the Linux Web App.")] string resourceId)
    {
        return await _linuxWebAppRuntimeStatusPlugin.GetLinuxWebAppRuntimeStatus(resourceId);
    }
}
