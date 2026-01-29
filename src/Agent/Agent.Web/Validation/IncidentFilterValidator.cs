// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models;
using Agent.Core.Validation;
using Agent.Data.DataModels;

namespace Agent.Web.Validation;

/// <summary>
/// Default implementation of IIncidentFilterValidator.
/// Validates incident filter documents for required fields, valid enum values, and platform consistency.
/// </summary>
public class IncidentFilterValidator : IIncidentFilterValidator
{
    private readonly ILogger<IncidentFilterValidator> _logger;
    private readonly IncidentManagementSettings _incidentManagementSettings;

    public IncidentFilterValidator(
        ILogger<IncidentFilterValidator> logger,
        IncidentManagementSettings incidentManagementSettings)
    {
        _logger = logger;
        _incidentManagementSettings = incidentManagementSettings;
    }

    /// <inheritdoc/>
    public ApiValidationResult ValidateIncidentFilter(IIncidentFilterDocument document)
    {
        _logger.LogDebug("Validating IncidentFilter document: {Id}", document.Id);

        var result = new ApiValidationResult();

        // Validate Id is not empty
        if (string.IsNullOrEmpty(document.Id))
        {
            result.AddError("Id cannot be empty");
        }

        // Validate HandlingAgent is set
        if (string.IsNullOrEmpty(document.HandlingAgent))
        {
            result.AddError("HandlingAgent must be set");
        }

        // Validate incident management platform matches configuration
        ValidateIncidentManagementPlatform(document, result);

        // Validate enum-like string fields
        ValidateEnumFields(document, result);

        return result;
    }

    /// <summary>
    /// Validates that the incident management platform in the document matches the configured platform.
    /// </summary>
    private void ValidateIncidentManagementPlatform(IIncidentFilterDocument document, ApiValidationResult result)
    {
        var expectedDocumentType = IncidentFilterDocumentUtilities.GetDocumentTypeName(_incidentManagementSettings.Type);
        if (!string.Equals(document.DocumentType, expectedDocumentType, StringComparison.OrdinalIgnoreCase))
        {
            var actualType = IncidentFilterDocumentUtilities.GetIncidentManagementTypeFromDocumentType(document.DocumentType);
            result.AddError($"Incident platform '{actualType}' does not match configured incident management type '{_incidentManagementSettings.Type}'");
        }
    }

    /// <summary>
    /// Validates enum-like string fields in the document.
    /// Currently validates:
    /// - AgentMode: must be one of ReadOnly, Review, Autonomous (case insensitive)
    /// - Priority: must be valid for the incident management platform (case insensitive)
    /// </summary>
    private void ValidateEnumFields(IIncidentFilterDocument document, ApiValidationResult result)
    {
        // Validate AgentMode and Priority if the document is an IncidentFilterDocumentPayload
        if (document is IncidentFilterDocumentPayload payload)
        {
            ValidateAgentMode(payload.AgentMode, result);
            ValidatePriority(payload.Priority, result);
        }
    }

    /// <summary>
    /// Validates that AgentMode is a valid value (ReadOnly, Review, Autonomous - case insensitive).
    /// Empty string is allowed as it indicates no specific mode is set.
    /// </summary>
    private static void ValidateAgentMode(string agentMode, ApiValidationResult result)
    {
        // Empty string is allowed - it means no specific mode is set
        if (string.IsNullOrEmpty(agentMode))
        {
            return;
        }

        if (!AgentModes.IsModeValid(agentMode))
        {
            result.AddError($"AgentMode '{agentMode}' is not valid. Allowed values are: {string.Join(", ", AgentModes.All)} (case insensitive)");
        }
    }

    /// <summary>
    /// Validates that Priority is a valid value for the configured incident management platform.
    /// Empty string is allowed as it indicates no specific priority filter is set.
    /// </summary>
    private void ValidatePriority(string priority, ApiValidationResult result)
    {
        // Empty string is allowed - it means no priority filter is set
        if (string.IsNullOrEmpty(priority))
        {
            return;
        }

        var incidentType = _incidentManagementSettings.Type;
        if (!IncidentPriorities.IsValidPriority(incidentType, priority))
        {
            var validPriorities = IncidentPriorities.GetValidPriorities(incidentType);
            result.AddError($"Priority '{priority}' is not valid for {incidentType}. Allowed values are: {string.Join(", ", validPriorities)} (case insensitive)");
        }
    }
}
