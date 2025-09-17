// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels.IncidentModel;
using Agent.Runtime.Models;

namespace Agent.Runtime.Interfaces;
/// <summary>
/// Interface for a single reasoning step in the investigation flow
/// </summary>
public interface IReasoningStep
{
    /// <summary>
    /// Executes this reasoning step
    /// </summary>
    Task<StepResult> ExecuteAsync(AlertItem alert, InvestigationContext context, CancellationToken cancellationToken = default);
    /// <summary>
    /// Unique name for this reasoning step
    /// </summary>
    string StepName { get; }
    /// <summary>
    /// Default priority for execution order (lower = higher priority)
    /// </summary>
    int DefaultPriority { get; }
}
