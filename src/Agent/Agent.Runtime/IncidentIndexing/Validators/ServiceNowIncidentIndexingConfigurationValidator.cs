// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Runtime.IncidentIndexing.Validators;

/// <summary>
/// Validator for ServiceNow incident indexing configuration.
/// Currently returns "not yet supported" - stub implementation.
/// </summary>
public class ServiceNowIncidentIndexingConfigurationValidator : IIncidentIndexingConfigurationValidator
{
    public IncidentManagementType ProviderType => IncidentManagementType.ServiceNow;

    public Task<IncidentIndexingValidationResult> ValidateAsync<TPayload>(TPayload request)
    {
        return Task.FromResult(new IncidentIndexingValidationResult(
            IsValid: false,
            Error: "Provider not yet supported",
            Details: "ServiceNow incident indexing configuration is not yet implemented. Please use ICM for now."));
    }
}
