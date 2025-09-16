// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Agent.Common.Core.Manifests
{
    /// <summary>
    /// YAML manifest wrapper for a Scheduled Task resource.
    /// apiVersion: azuresre.ai/v1
    /// kind: ScheduledTask
    /// </summary>
    public class ScheduledTaskManifest
    {
        public string ApiVersion { get; set; } = "azuresre.ai/v1";
        public string Kind { get; set; } = "ScheduledTask";
        public ManifestMetadata Metadata { get; set; } = new ManifestMetadata();
        public ScheduledTaskSpec Spec { get; set; } = new ScheduledTaskSpec();
    }

    /// <summary>
    /// Common metadata block for YAML resources.
    /// </summary>
    public class ManifestMetadata
    {
        public string? Name { get; set; }
        public string? Owner { get; set; }
        public string? Version { get; set; }
        public IList<string>? Tags { get; set; }
    }

    /// <summary>
    /// Spec for creating/updating a scheduled task. Property names intentionally
    /// follow PascalCase to map from underscored YAML via YamlDotNet naming.
    /// </summary>
    public class ScheduledTaskSpec
    {
        // Required
        public string Name { get; set; } = string.Empty;
        public string CronExpression { get; set; } = string.Empty; // supports YAML key 'cron_expression' or 'cron'
        public string AgentPrompt { get; set; } = string.Empty;

        // Optional
        public string? Description { get; set; }
        public string? Agent { get; set; } // starting agent for the task
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? DurationHours { get; set; } // helper for quickstart; if set, EndTime = StartTime + DurationHours
        public string? ThreadId { get; set; }
        public int? MaxExecutions { get; set; }
        public string? NotificationChannel { get; set; }

        // Back-compat alias for YAML that uses 'cron' instead of 'cron_expression'.
        public string? Cron { get; set; }

        public string ResolveCronExpression()
        {
            if (!string.IsNullOrWhiteSpace(CronExpression)) return CronExpression;
            if (!string.IsNullOrWhiteSpace(Cron)) return Cron!;
            return string.Empty;
        }
    }
}
