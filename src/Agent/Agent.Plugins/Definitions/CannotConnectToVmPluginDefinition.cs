// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Core;
using Agent.Core.Attributes;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class CannotConnectToVmPluginDefinition
    {
        private readonly ICannotConnectToVmPlugin _impl;

        public CannotConnectToVmPluginDefinition(ICannotConnectToVmPlugin impl) => _impl = impl;

        [Description("Analyze VM boot screenshot. Always run first for any 'not booting' case.")]
        [AgentTool(ToolMode.Auto)]
        [WriteAction]
        [OboContext(scope: Constants.DefaultOboTokenScope)]
        [RequiresApproval]
        public Task<string> AnalyzeVmScreenshot(
            [Description("Full Azure VM resource ID.")] string resourceId) =>
            _impl.AnalyzeVmScreenshotAsync(resourceId);

        [Description("Analyze VM serial boot log. Use ONLY for Linux VMs")]
        [AgentTool(ToolMode.Auto)]
        [WriteAction]
        [OboContext(scope: Constants.DefaultOboTokenScope)]
        [RequiresApproval]
        public Task<string> AnalyzeVmSerialLog(
            [Description("Full Azure VM resource ID.")] string resourceId) =>
            _impl.AnalyzeVmSerialLogAsync(resourceId);

        [Description("High-level orchestrator for \"cannot connect to VM\" scenarios. errorMessage is optional. " +
            "If an error is matched in internal mapping, guidance is returned immediately." +
            "Otherwise the method uses osType to evaluate boot diagnostics, invokes screenshot/serial analysis," +
            "and if still no cause is found returns a handoff indicator to meta agent.")]
        [AgentTool(ToolMode.Auto)]
        [WriteAction]
        [OboContext(scope: Constants.DefaultOboTokenScope)]
        [RequiresApproval]
        public Task<string> DiagnoseVmConnectivityIssues(
            [Description("Full Azure VM resource ID.")] string resourceId,
            [Description("OS type: Windows or Linux.")] string osType,
            [Description("Optional Tsg referencing solution to the error text provided by the user.")] string? tsgFileName = null) =>
            _impl.DiagnoseVmConnectivityIssuesAsync(resourceId, osType, tsgFileName);

    }
}
