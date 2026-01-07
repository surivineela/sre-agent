// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Validation;
using Agent.Data.DataModels;

namespace Agent.Web.Validation;

/// <summary>
/// Interface for validating incident filter documents before persisting to storage.
/// Used primarily during dry-run operations to validate resource structure and constraints.
/// Validates:
/// - Incident management platform matches the configured platform
/// - Enum-like string fields (e.g., AgentMode) contain valid values
/// - Required fields are set
/// </summary>
public interface IIncidentFilterValidator
{
    /// <summary>
    /// Validates an incident filter document.
    /// Checks platform consistency, enum field values, and required fields.
    /// </summary>
    /// <param name="document">The incident filter document to validate.</param>
    /// <returns>ApiValidationResult indicating success or validation errors.</returns>
    ApiValidationResult ValidateIncidentFilter(IIncidentFilterDocument document);
}
