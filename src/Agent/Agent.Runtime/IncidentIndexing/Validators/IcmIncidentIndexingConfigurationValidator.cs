// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Framework;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.IncidentIndexing.Validators;

/// <summary>
/// Validator for ICM incident indexing configuration.
/// Validates team access and connectivity with ICM API.
/// </summary>
public class IcmIncidentIndexingConfigurationValidator : IIncidentIndexingConfigurationValidator
{
    private readonly IICMAPIClient _icmApiClient;
    private readonly ILogger<IcmIncidentIndexingConfigurationValidator> _logger;

    public IncidentManagementType ProviderType => IncidentManagementType.Icm;

    public IcmIncidentIndexingConfigurationValidator(
        IICMAPIClient icmApiClient,
        ILogger<IcmIncidentIndexingConfigurationValidator> logger)
    {
        _icmApiClient = icmApiClient ?? throw new ArgumentNullException(nameof(icmApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IncidentIndexingValidationResult> ValidateAsync<TPayload>(TPayload request)
    {
        if (request is not IcmIncidentIndexingConfigurationPayload icmRequest)
        {
            return new IncidentIndexingValidationResult(
                IsValid: false,
                Error: "Invalid request type",
                Details: $"Expected IcmIncidentIndexingConfigurationPayload but received {typeof(TPayload).Name}");
        }

        // Validate required fields
        if (icmRequest.TeamIds == null || icmRequest.TeamIds.Count == 0)
        {
            _logger.LogInternalWarning("ICM validation failed: No team IDs provided");
            return new IncidentIndexingValidationResult(
                IsValid: false,
                Error: "At least one team ID is required",
                Details: "Please provide one or more ICM team IDs to configure incident indexing.");
        }

        // Validate severity range (in ICM, lower number = higher severity)
        if (icmRequest.MinSeverity < icmRequest.MaxSeverity)
        {
            _logger.LogInternalWarning(
                "ICM validation failed: Invalid severity range. Min={Min}, Max={Max}",
                icmRequest.MinSeverity,
                icmRequest.MaxSeverity);
            return new IncidentIndexingValidationResult(
                IsValid: false,
                Error: "Invalid severity range",
                Details: $"MinSeverity ({icmRequest.MinSeverity}) must be greater than or equal to MaxSeverity ({icmRequest.MaxSeverity}). Note: In ICM, 1 is most critical.");
        }

        // Validate ICM connectivity by fetching team info
        var primaryTeamId = icmRequest.TeamIds.First();
        try
        {
            _logger.LogInternalInformation("Validating ICM team {TeamId}", primaryTeamId);
            var teamInfo = await _icmApiClient.GetTeamAsync(primaryTeamId);

            if (teamInfo == null)
            {
                _logger.LogInternalWarning("ICM team not found: {TeamId}", primaryTeamId);
                return new IncidentIndexingValidationResult(
                    IsValid: false,
                    Error: "Team not found",
                    Details: $"Team ID '{primaryTeamId}' does not exist or is not accessible. Please verify the team ID.");
            }

            var teamName = teamInfo.Name;
            var tenantName = teamInfo.Tenant?.Name;

            _logger.LogInternalInformation(
                "ICM team validated: {TeamName} ({TenantName})",
                teamName,
                tenantName);

            return new IncidentIndexingValidationResult(
                IsValid: true,
                ValidatedInfo: new IcmValidatedInfo(teamName, tenantName));
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to validate ICM team {TeamId}", primaryTeamId);
            return new IncidentIndexingValidationResult(
                IsValid: false,
                Error: "Unable to connect to ICM",
                Details: $"Failed to validate team '{primaryTeamId}'. Please ensure the managed identity has access to this team. Error: {ex.Message}");
        }
    }
}
