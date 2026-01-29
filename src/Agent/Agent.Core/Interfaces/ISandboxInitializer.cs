// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Adc;

namespace Agent.Core.Interfaces;

/// <summary>
/// Interface for initializing a new sandbox after it has been provisioned and started.
/// </summary>
public interface ISandboxInitializer
{
    /// <summary>
    /// Performs initialization steps on the running sandbox.
    /// </summary>
    /// <param name="sandbox">The sandbox info including ID, endpoint, and resources.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(AdcSandboxInfo sandbox, CancellationToken cancellationToken);
}
