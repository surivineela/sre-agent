// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Models;

namespace Agent.Runtime.Interfaces;
/// <summary>
/// Interface for generating and updating hypotheses
/// </summary>
public interface IHypothesisGenerator
{
    /// <summary>
    /// Generates hypotheses based on all collected evidence
    /// </summary>
    Task<List<Hypothesis>> GenerateHypothesesAsync(
        InvestigationContext context,
        CancellationToken cancellationToken = default);
}
