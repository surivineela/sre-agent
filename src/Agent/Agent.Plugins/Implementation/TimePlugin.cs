// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins;

// [Export]
public class TimePlugin : ITimePlugin
{
    private static readonly Dictionary<string, (string timezone, string location, string info)> RegionMapping = new()
    {
        { "westus", ("America/Los_Angeles", "Fresno, California", "Pacific Time") },
        { "eastus", ("America/New_York", "Virginia", "Eastern Time") },
        { "centralus", ("America/Chicago", "Iowa", "Central Time") },
        { "northcentralus", ("America/Chicago", "Illinois", "Central Time") },
        { "southcentralus", ("America/Chicago", "Texas", "Central Time") },
        { "eastus2", ("America/New_York", "Virginia", "Eastern Time") },
        { "westus2", ("America/Los_Angeles", "Washington", "Pacific Time") },
        { "westus3", ("America/Phoenix", "Arizona", "Mountain Time") },
        { "canadacentral", ("America/Chicago", "Toronto", "Central Time") },
        { "canadaeast", ("America/New_York", "Quebec City", "Eastern Time") },
        { "brazilsouth", ("America/Sao_Paulo", "São Paulo", "Brasilia Time") },
        { "northeurope", ("Europe/Paris", "Ireland", "Central European Time") },
        { "westeurope", ("Europe/Paris", "Netherlands", "Central European Time") },
        { "uksouth", ("Europe/London", "London", "British Time") },
        { "ukwest", ("Europe/London", "Cardiff", "British Time") },
        { "eastasia", ("Asia/Shanghai", "Hong Kong", "China Standard Time") },
        { "southeastasia", ("Asia/Singapore", "Singapore", "Singapore Time") },
        { "japaneast", ("Asia/Tokyo", "Tokyo", "Japan Standard Time") },
        { "australiaeast", ("Australia/Sydney", "New South Wales", "Australian Eastern Time") },
        { "australiasoutheast", ("Australia/Melbourne", "Victoria", "Australian Eastern Time") }
    };

    private static readonly Dictionary<string, (string standard, string daylight)> TimeZoneAbbreviations = new()
    {
        { "America/Los_Angeles", ("PST", "PDT") },
        { "America/New_York", ("EST", "EDT") },
        { "America/Chicago", ("CST", "CDT") },
        { "America/Phoenix", ("MST", "MDT") },
        { "Europe/Paris", ("CET", "CEST") },
        { "Europe/London", ("GMT", "BST") },
        { "Asia/Shanghai", ("CST", "CST") },
        { "Asia/Singapore", ("SGT", "SGT") },
        { "Asia/Tokyo", ("JST", "JST") },
        { "Australia/Sydney", ("AEST", "AEDT") },
        { "Australia/Melbourne", ("AEST", "AEDT") },
        { "America/Sao_Paulo", ("BRT", "BRST") }
    };

    public DateTime GetCurrentUtcTime()
    {
        return DateTime.UtcNow;
    }

    public string GetAppTimeZone(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return "Invalid resource ID provided.";
        }

        // Convert resource ID to lowercase for case-insensitive matching
        resourceId = resourceId.ToLower();

        // Find the first matching region
        var matchingRegion = RegionMapping.FirstOrDefault(r => resourceId.Contains(r.Key));
        if (matchingRegion.Key == null)
        {
            return "Region not recognized. Please check if the resourceId contains a supported Azure region.";
        }

        var (timezone, location, tzInfo) = matchingRegion.Value;
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var isDst = timeZoneInfo.IsDaylightSavingTime(DateTime.UtcNow);

        // Get timezone abbreviation
        string timeZoneAbbr = GetTimeZoneAbbreviation(timezone, isDst);

        // Format the response
        var regionName = char.ToUpper(matchingRegion.Key[0]) + matchingRegion.Key.Substring(1);
        return $"App is created in {regionName} region of Azure. " +
               $"Azure {regionName} Datacenter is in {location} " +
               $"following the time zone {tzInfo} ({timezone}). " +
               $"Currently observing {timeZoneAbbr} ({(isDst ? "Daylight Saving Time" : "Standard Time")} is in effect)";
    }

    private static string GetTimeZoneAbbreviation(string timezone, bool isDst)
    {
        if (TimeZoneAbbreviations.TryGetValue(timezone, out var abbreviations))
        {
            return isDst ? abbreviations.daylight : abbreviations.standard;
        }
        return timezone.Split('/').Last();
    }
}
