// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Common.ApiModels;

namespace Agent.Core.Interfaces;

/// <summary>
/// Service for loading managed identity configuration from the agent's MSI config map and certificates.
/// </summary>
public interface IManagedIdentityConfigService
{
    /// <summary>
    /// Loads managed identity info for the specified identity resource ID.
    /// </summary>
    /// <param name="identityResourceId">
    /// The ARM resource ID of the user-assigned managed identity to use.
    /// Pass null or empty string to use the system-assigned managed identity.
    /// </param>
    /// <returns>
    /// The managed identity info if found and loaded successfully; null otherwise.
    /// </returns>
    Task<ManagedIdentityInfo?> GetManagedIdentityInfoAsync(string? identityResourceId);
}
