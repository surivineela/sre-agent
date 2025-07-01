// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin(IsFirstPartyOnly = true)]
public class WebAppPluginDefinition
{
    private readonly IWebAppPlugin _webAppPlugin;

    public WebAppPluginDefinition(IWebAppPlugin webAppPlugin)
    {
        _webAppPlugin = webAppPlugin ?? throw new ArgumentNullException(nameof(webAppPlugin));
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Takes a web app name and a stamp name and fetches the details to reboot the worker like location, role, roleinstance, etc.")]
    public Task<string> GetWebAppRebootWorkerDetails(
        [Description("Name of the web app")] string webappName,
        [Description("Name of the stamp")] string stampName)
    {
        return _webAppPlugin.GetWebAppRebootWorkerDetails(webappName, stampName);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Takes a web app name and fetches the details like subscription id, webspace name, hostnames etc.")]
    public Task<string> GetWebAppDetailsByName(
        [Description("Name of the web app")] string webappName)
    {
        return _webAppPlugin.GetWebAppDetailsByName(webappName);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Takes a web app name and a stamp name and fetches the hostnames for the web app.")]
    public Task<string> GetWebAppHostnames(
        [Description("Name of the web app")] string webappName,
        [Description("Name of the stamp")] string stampName)
    {
        return _webAppPlugin.GetWebAppHostnames(webappName, stampName);
    }
}
