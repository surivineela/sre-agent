// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels.IncidentModel;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

public interface IAzMonitorAlertInvestigationService
{
    Task<string> AnalyzeApplicationHealth(AlertItem alert, Thread alertThread);

    Task<string> AnalyzeResourceMetrics(AlertItem alert, Thread alertThread);

    Task<string> AnalyzeConnectedComponents(AlertItem alert, Thread alertThread);

    Task<string> AnalyzeActivityLogsForResource(AlertItem alert, Thread alertThread);

    Task<string> AnalyzeLogQueries(AlertItem alert, Thread alertThread);

    Task<string> AnalyzeGenericLogQueries(AlertItem alert, Thread alertThread);
}
