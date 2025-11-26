// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public sealed class AppHealthInfo
{
    // last captured timestamp
    public DateTime LastDataCaptureTimeStampInUTC { get; set; } = DateTime.UtcNow;
    public ScorecardHealthState Health { get; set; } = ScorecardHealthState.Unknown;

    // availability
    public double? Availability { get; set; }

    // activity (requests/transactions)
    public double? Transactions { get; set; }

    // costs ($ USD)
    public double? Costs { get; set; }

    // average latency (ms)
    public double? AvgLatencyInMs { get; set; }

    public double? AvgMemoryUsage { get; set; }

    public double? AvgCpuUsage { get; set; }

    // maybe not needed?
    public IDictionary<string, object> AdditionalMetrics { get; set; } = new Dictionary<string, object>();

    // time since lastActivity
    public DateTime? TimeSinceLastActivity { get; set; }

    // if resource IsActive
    [JsonIgnore]
    public bool IsActive
    {
        get
        {
            if ((Transactions != null && Transactions > 0) || (AvgCpuUsage != null && AvgCpuUsage > 0) || (AvgMemoryUsage != null && AvgMemoryUsage > 0))
            {
                TimeSinceLastActivity = DateTime.UtcNow;
            }

            // If we have scanned in the last 30 mins and never set a timeSinceLastActivity then it has never been active
            // If there's been no activity for 24 hours, it's inactive
            if (((DateTime.UtcNow - LastDataCaptureTimeStampInUTC) > TimeSpan.FromMinutes(30) && TimeSinceLastActivity == null) ||
                    (TimeSinceLastActivity.HasValue && DateTime.UtcNow - TimeSinceLastActivity.Value >= TimeSpan.FromHours(24)))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
