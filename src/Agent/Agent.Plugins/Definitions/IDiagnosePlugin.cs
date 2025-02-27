// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public interface IDiagnosePlugin
    {
        string Diagnose(Kernel kernel, IReadOnlyList<string> resourceIdList);
        AsyncOperationStatusSummary<string, string>? GetDiagnoseStatus(string resourceId);
    }
}
