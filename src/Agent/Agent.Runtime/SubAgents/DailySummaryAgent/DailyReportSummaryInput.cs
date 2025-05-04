// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Newtonsoft.Json;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.DailyReportSummary
{
    public class DailyReportSummaryInput
    {
        public string ReportType { get; set; } = "Daily"; // Daily, Weekly, Monthly
        public string MetricsDescription { get; set; } = string.Empty;
        //public List<string> ResourceTypesToInclude { get; set; } = new List<string>();
        public string Timespan { get; set; } = "1d";
        public ReportOverview Overview { get; set; } = new ReportOverview();
        public CVESummary CVESummary { get; set; } = null;
        public IncidentSummary IncidentsSummary { get; set; } = null;
        public List<AppGroupResourceSummary> AppGroupResourceSummary { get; set; } = null;
        public RecommendedActionsAndObservations RecommendedActionsAndObservations { get; set; } = null;
    }

    public sealed record DailyReportSummaryAgentInput(
          DailyReportSummaryInput Input,
          IReadOnlyList<string> ToolSignatures,
          Guid ThreadId);

    public class RecommendedActionsAndObservations
    {
        [JsonProperty("actions")]
        public List<ActionItem>? Actions { get; set; }

        [JsonProperty("observations")]
        public List<string>? Observations { get; set; }
    }

    public class ActionItem
    {
        [JsonProperty("priority")]
        public string? Priority { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("eta")]
        public string? ETA { get; set; }
    }

    public class AppGroupResourceSummary
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string SubscriptionName { get; set; } = string.Empty;
        public List<AppGroupResourceInfo> AppGroups { get; set; } = new List<AppGroupResourceInfo>();
    }


    public class AppGroupResourceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public AppHealthInfo? AppHealthInfo { get; set; } = null;
    }

    public class IncidentSummary
    {
        public List<IncidentInfo> PagerDuty { get; set; } = new List<IncidentInfo>();
        public List<IncidentInfo> AzureMonitor { get; set; } = new List<IncidentInfo>();
    }

    public class CVESummary
    {
        public List<CVEInfo> Vulnerabilities { get; set; } = new List<CVEInfo>();
        public Dictionary<string, List<string>> VulnerabilitiesByRepo { get; set; } = new Dictionary<string, List<string>>();
        public int TotalVulnerabilities { get; set; }
        public int CriticalVulnerabilities { get; set; }
        public int HighVulnerabilities { get; set; }
        public int ModerateVulnerabilities { get; set; }
        public int LowVulnerabilities { get; set; }
    }

    public class CVEInfo
    {
        public string RepoUrl { get; set; } = string.Empty;
        public int Number { get; set; }
        public string State { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? FixedAt { get; set; }
    }

    public class IncidentInfo
    {
        public string IncidentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime? CreateTime { get; set; } = null;
        public TimeSpan? Duration { get; set; } = null;
        public string Status { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string InvestigationDetails { get; set; } = string.Empty;
        public string ThreadLink { get; set; } = "fakelink";
    }

    public class SecurityOverview
    {
        public int Critical { get; set; }
        public int High { get; set; }
        public int Moderate { get; set; }
        public int Low { get; set; }
        public int TotalCount { get; set; }
    }

    public class IncidentsOverview
    {
        public int Active { get; set; }
        public int Mitigated { get; set; }
        public int Resolved { get; set; }
        public int TotalCount { get; set; }
    }

    public class HealthPerformanceOverview
    {
        public int Healthy { get; set; }
        public int Degraded { get; set; }
        public int Unhealthy { get; set; }
        public int TotalCount { get; set; }
    }

    public class ReportOverview
    {
        public SecurityOverview SecurityFindings { get; set; } = new SecurityOverview();
        public IncidentsOverview Incidents { get; set; } = new IncidentsOverview();
        public HealthPerformanceOverview HealthAndPerformance { get; set; } = new HealthPerformanceOverview();
    }

    public class AppHealthInfo
    {
        public DateTime LastDataCaptureTimeStampInUTC { get; set; }
        public ScorecardHealthState Health { get; set; }
        public double Availability { get; set; }
        public double AvgCpuUsage { get; set; }
        public double AvgMemoryUsage { get; set; }
        public long Transactions { get; set; }
        public List<HistoricalDataPoint> HistoricalData { get; set; } = new List<HistoricalDataPoint>();
    }

    public class HistoricalDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Availability { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
    }
}

