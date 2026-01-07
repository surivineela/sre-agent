// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration;

/// <summary>
/// Provides valid priority values for each incident management platform.
/// These values are used for validation and for populating filter field options.
/// </summary>
public static class IncidentPriorities
{
    /// <summary>
    /// Valid priority values for ICM incidents.
    /// </summary>
    public static readonly string[] Icm = { "1", "2", "25", "3", "4" };

    /// <summary>
    /// Valid priority values for Azure Monitor incidents (severity levels).
    /// </summary>
    public static readonly string[] AzMonitor = { "Sev0", "Sev1", "Sev2", "Sev3", "Sev4" };

    /// <summary>
    /// Valid priority values for PagerDuty incidents.
    /// Note: PagerDuty priorities can be customized per account, but these are common defaults.
    /// </summary>
    public static readonly string[] PagerDuty = { "P1", "P2", "P3", "P4", "P5" };

    /// <summary>
    /// Valid priority values for ServiceNow incidents.
    /// </summary>
    public static readonly string[] ServiceNow = { "1", "2", "3", "4", "5" };

    /// <summary>
    /// Gets the valid priority values for the specified incident management type.
    /// </summary>
    /// <param name="type">The incident management type.</param>
    /// <returns>Array of valid priority values, or empty array if type is None or null.</returns>
    public static string[] GetValidPriorities(IncidentManagementType? type)
    {
        return type switch
        {
            IncidentManagementType.Icm => Icm,
            IncidentManagementType.AzMonitor => AzMonitor,
            IncidentManagementType.PagerDuty => PagerDuty,
            IncidentManagementType.ServiceNow => ServiceNow,
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Checks if a priority value is valid for the specified incident management type.
    /// </summary>
    /// <param name="type">The incident management type.</param>
    /// <param name="priority">The priority value to validate.</param>
    /// <returns>True if the priority is valid, false otherwise.</returns>
    public static bool IsValidPriority(IncidentManagementType? type, string priority)
    {
        if (string.IsNullOrEmpty(priority))
        {
            return true; // Empty is allowed - no priority filter set
        }

        var validPriorities = GetValidPriorities(type);
        return validPriorities.Length == 0 || validPriorities.Any(p => string.Equals(p, priority, StringComparison.OrdinalIgnoreCase));
    }
}
