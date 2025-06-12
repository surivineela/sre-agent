// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Mocks;

// [Export]
public class MockTimePlugin : ITimePlugin
{
    private readonly TimeProvider _timeProvider;

    public MockTimePlugin(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string GetAppTimeZone(string resourceId)
    {
        // Extract region from resource ID
        var match = Regex.Match(resourceId, @"/([^/]+)/providers/");
        if (!match.Success)
        {
            return "UTC";
        }

        var region = match.Groups[1].Value.ToLower();

        // Map regions to timezones
        return region switch
        {
            "eastus" => "America/New_York",
            "westus" => "America/Los_Angeles",
            "centralus" => "America/Chicago",
            "northcentralus" => "America/Chicago",
            "southcentralus" => "America/Chicago",
            "eastus2" => "America/New_York",
            "westus2" => "America/Los_Angeles",
            "westus3" => "America/Phoenix",
            "canadacentral" => "America/Toronto",
            "canadaeast" => "America/Halifax",
            "brazilsouth" => "America/Sao_Paulo",
            "northeurope" => "Europe/London",
            "westeurope" => "Europe/Paris",
            "uksouth" => "Europe/London",
            "ukwest" => "Europe/London",
            "eastasia" => "Asia/Shanghai",
            "southeastasia" => "Asia/Singapore",
            "australiaeast" => "Australia/Sydney",
            "australiasoutheast" => "Australia/Melbourne",
            _ => "UTC"
        };
    }

    public DateTime GetCurrentUtcTime()
    {
        return _timeProvider.GetUtcNow().DateTime;
    }
}

