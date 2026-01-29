// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Runtime.IncidentIndexing.Validators;

/// <summary>
/// Interface for validating incident indexing configuration for a specific provider.
/// </summary>
public interface IIncidentIndexingConfigurationValidator
{
    /// <summary>
    /// The provider type this validator handles.
    /// </summary>
    IncidentManagementType ProviderType { get; }

    /// <summary>
    /// Validates the configuration request and checks connectivity.
    /// </summary>
    /// <typeparam name="TPayload">The payload type for this provider.</typeparam>
    /// <param name="request">The configuration request to validate.</param>
    /// <returns>Validation result with success/failure status and additional info.</returns>
    Task<IncidentIndexingValidationResult> ValidateAsync<TPayload>(TPayload request);
}

/// <summary>
/// Result of configuration validation.
/// </summary>
/// <param name="IsValid">Whether the validation passed.</param>
/// <param name="Error">Error message if validation failed.</param>
/// <param name="Details">Additional details about the error or validation.</param>
/// <param name="ValidatedInfo">Additional validated information (e.g., team name, tenant name).</param>
public record IncidentIndexingValidationResult(
    bool IsValid,
    string? Error = null,
    string? Details = null,
    object? ValidatedInfo = null);

/// <summary>
/// Validated information specific to ICM.
/// </summary>
public record IcmValidatedInfo(string? TeamName, string? TenantName);
