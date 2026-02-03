// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Interfaces;

/// <summary>
/// Service for checking Azure API Connection (Microsoft.Web/connections) status.
/// Used by ServiceNowOAuthClient to verify connection health.
/// </summary>
public interface IApiConnectionService
{
    /// <summary>
    /// Gets the status of an API Connection (e.g., "Connected", "Error", "Unauthenticated").
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID</param>
    /// <param name="resourceGroupName">Resource group name</param>
    /// <param name="connectionName">Name of the API Connection resource</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection status or null if not found</returns>
    Task<string?> GetConnectionStatusAsync(
        string subscriptionId,
        string resourceGroupName,
        string connectionName,
        CancellationToken cancellationToken = default);
}
