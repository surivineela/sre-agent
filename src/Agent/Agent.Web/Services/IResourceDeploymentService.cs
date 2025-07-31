// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Web.Models.ExtendedAgents;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Services;


public interface IResourceDeploymentService
{
    

    Task<IActionResult> ApplyAsync(AgentDeploymentModel spec);
    Task<IActionResult> ApplyAsync(ConnectorsDeploymentModel spec);
    Task<IActionResult> ApplyAsync(ToolsDeploymentModel spec);
    Task<IActionResult> ApplyAsync(PluginConfigDeploymentModel pluginConfig);
}
