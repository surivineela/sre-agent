// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for incident filter operations.
/// </summary>
public static class IncidentFilterHelper
{
    /// <summary>
    /// Platform name constants with canonical spellings.
    /// </summary>
    public static class Platform
    {
        public const string IcM = "IcM";
        public const string AzMonitor = "AzMonitor";
        public const string PagerDuty = "PagerDuty";
        public const string ServiceNow = "ServiceNow";

        /// <summary>
        /// All valid platform names.
        /// </summary>
        public static readonly string[] All = [IcM, AzMonitor, PagerDuty, ServiceNow];
    }

    /// <summary>
    /// Valid platform names for case-insensitive lookup.
    /// </summary>
    private static readonly HashSet<string> ValidPlatformSet = new(Platform.All, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Normalizes a platform name to its canonical spelling.
    /// Returns the original value if not a recognized platform.
    /// </summary>
    /// <param name="platform">The platform name to normalize</param>
    /// <returns>The normalized platform name</returns>
    public static string? NormalizePlatform(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            return platform;
        }

        // Find the canonical spelling using case-insensitive comparison
        foreach (var validPlatform in Platform.All)
        {
            if (string.Equals(platform, validPlatform, StringComparison.OrdinalIgnoreCase))
            {
                return validPlatform;
            }
        }

        // Return original if not a recognized platform
        return platform;
    }

    /// <summary>
    /// Checks if a platform name is valid (case-insensitive).
    /// </summary>
    /// <param name="platform">The platform name to check</param>
    /// <returns>True if the platform is valid</returns>
    public static bool IsValidPlatform(string? platform)
    {
        return !string.IsNullOrWhiteSpace(platform) && ValidPlatformSet.Contains(platform);
    }
}
