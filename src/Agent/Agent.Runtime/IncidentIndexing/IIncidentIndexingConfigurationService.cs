// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Runtime.IncidentIndexing.Validators;

namespace Agent.Runtime.IncidentIndexing;

/// <summary>
/// Generic service interface for managing incident indexing configuration.
/// One configuration per provider type per agent.
/// </summary>
/// <typeparam name="TDocument">The provider-specific configuration document type.</typeparam>
/// <typeparam name="TPayload">The provider-specific configuration request payload type.</typeparam>
public interface IIncidentIndexingConfigurationService<TDocument, TPayload>
    where TDocument : IIncidentIndexingConfigurationDocument
    where TPayload : IncidentIndexingConfigurationPayload
{
    /// <summary>
    /// Gets the current configuration. Returns null if not configured.
    /// </summary>
    Task<TDocument?> GetConfigurationAsync();

    /// <summary>
    /// Creates or updates the configuration after validation.
    /// </summary>
    /// <param name="request">The configuration request payload.</param>
    /// <param name="validationResult">The validation result with any validated info.</param>
    /// <returns>The saved configuration document.</returns>
    Task<TDocument> CreateOrUpdateConfigurationAsync(TPayload request, IncidentIndexingValidationResult validationResult);

    /// <summary>
    /// Deletes the configuration.
    /// </summary>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteConfigurationAsync();
}
