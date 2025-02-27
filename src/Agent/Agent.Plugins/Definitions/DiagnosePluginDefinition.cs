// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class DiagnosePluginDefinition
    {
        private readonly IDiagnosePlugin _diagnosePlugin;

        public DiagnosePluginDefinition(IDiagnosePlugin diagnosePlugin)
        {
            _diagnosePlugin = diagnosePlugin;
        }

        [KernelFunction("diagnose_appservices")]
        [Description(
            "To start diagnose process for single or multiple app services asynchronously."
        )]
        public string Diagnose(
            Kernel kernel,
            [Description("The list of resource ID of the app service resource to diagnose.")]
                IReadOnlyList<string> resourceIdList
        )
        {
            return _diagnosePlugin.Diagnose(kernel, resourceIdList);
        }

        [KernelFunction("get_appservice_diagnose_status")]
        [Description(
            "To query an app service's diagnose status to know if it's finished or in progress. Returns null if the operation hasn't started yet."
        )]
        public AsyncOperationStatusSummary<string, string>? GetDiagnoseStatus(
            [Description("The resource ID of the app service resource.")] string resourceId
        )
        {
            return _diagnosePlugin.GetDiagnoseStatus(resourceId);
        }
    }
}
