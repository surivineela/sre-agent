// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for validating CLI inputs.
/// </summary>
public static partial class ValidationHelper
{
    private const int MaxNameLength = 128;

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex ValidNameRegex();

    /// <summary>
    /// Validates a resource name (tool, agent, etc.).
    /// Names must be less than 128 characters and only contain letters, numbers, underscores, and hyphens.
    /// </summary>
    /// <param name="name">The name to validate</param>
    /// <param name="resourceType">The type of resource (e.g., "tool", "agent") for error messages</param>
    /// <returns>Tuple of (isValid, errorMessage)</returns>
    public static (bool IsValid, string? ErrorMessage) ValidateResourceName(string? name, string resourceType = "resource")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, $"{char.ToUpper(resourceType[0])}{resourceType[1..]} name must not be empty.");
        }

        if (name.Length > MaxNameLength)
        {
            return (false, $"{char.ToUpper(resourceType[0])}{resourceType[1..]} name must be less than {MaxNameLength} characters. Current length: {name.Length}");
        }

        if (!ValidNameRegex().IsMatch(name))
        {
            return (false, $"{char.ToUpper(resourceType[0])}{resourceType[1..]} name must only contain letters (a-z, A-Z), numbers (0-9), underscores (_), and hyphens (-).");
        }

        return (true, null);
    }
}
