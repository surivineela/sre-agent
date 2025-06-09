// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Services;
using Agent.Runtime.Models;
using Thread = Agent.Core.Models.Api.v1.Thread;
namespace Agent.Runtime.Interfaces;
/// <summary>
/// Interface for orchestrating the investigation flow
/// </summary>
public interface IInvestigationOrchestrator
{
    /// <summary>
    /// Runs the full investigation loop for an alert
    /// </summary>
    Task<InvestigationSummary> InvestigateAlertAsync(
        AlertItem alert,
        Thread alertThread,
        CancellationToken cancellationToken = default);
}

