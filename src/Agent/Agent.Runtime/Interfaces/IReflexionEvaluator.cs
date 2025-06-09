// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Models;
namespace Agent.Runtime.Interfaces;
/// <summary>
/// Interface for evaluating the quality of an investigation
/// </summary>
public interface IReflexionEvaluator
{
    // <summary>
    /// Evaluates the current state of an investigation
    /// </summary>
    Task<ReflexionResult> EvaluateInvestigationAsync(
        InvestigationContext context,
        CancellationToken cancellationToken = default);
}
