// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using System.ComponentModel;

namespace Agent.Runtime.SubAgents.DailyReportSummary
{
    public class DailyReportSummaryInput
    {
        public string ReportType { get; set; } = "Daily"; // Daily, Weekly, Monthly
        public string MetricsDescription { get; set; } = string.Empty;
        //public List<string> ResourceTypesToInclude { get; set; } = new List<string>();
        public string Timespan { get; set; } = "1d";
        [Description("A detailed description of dashboard summaries captured by the SRE Dashboard Agent")]
        public string DashboardSummary { get; set; } = null;
    }

    public sealed record DailyReportSummaryAgentInput(
        DailyReportSummaryInput Input,
        IReadOnlyList<string> ToolSignatures,
        ThreadContext Context);
}

