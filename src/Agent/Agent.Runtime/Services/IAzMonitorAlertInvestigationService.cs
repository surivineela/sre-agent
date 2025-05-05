// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Services;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

public interface IAzMonitorAlertInvestigationService
{
    Task<string> GetApplicationHealthAsync(AlertItem alert, Thread alertThread);

    Task<string> GetMetricsForResource(AlertItem alert, Thread alertThread);

    Task<string> AnalyzeConnectedComponents(AlertItem alert, Thread alertThread);

    Task<string> AnalyzeActivityLogsForResource(AlertItem alert, Thread alertThread);

    Task<string> AnalyzeLogQueries(AlertItem alert, Thread alertThread);
}
