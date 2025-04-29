// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions;
public interface IAppCodeAnalysisPlugin
{
    public Guid? ThreadId { get; set; }

    Task<string> GetCallStackForApp(string resourceId);

    Task<string> GetSummaryOfExceptions(string resourceId);

    Task<string> GetStackTraceOfLastException(string resourceId);

    Task<string> GetStackTraceOfMostCommonException(string resourceId);

    Task<string> PerformDeploymentSwapForApp(string resourceId);

    Task<string> GetDeploymentActivity(string resourceId);


    Task<string> GetAppConsoleLogs(string resourceId);
    Task<bool> WaitInMilliSeconds([Description("time to wait in milliseconds")] int numMilliSeconds);
}



