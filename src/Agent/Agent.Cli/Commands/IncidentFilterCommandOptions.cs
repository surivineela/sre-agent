// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for incident-filter commands.
/// </summary>
public static class IncidentFilterCommandOptions
{
    // ============================================================
    // Incident-Filter Create Command Options
    // ============================================================

    public static class Create
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the incident filter",
            Required = true
        };

        public static readonly Option<string> PlatformOption = new("--platform")
        {
            Description = $"Incident management platform ({string.Join(", ", IncidentFilterHelper.Platform.All)})",
            Required = true
        };

        public static readonly Option<string> HandlingAgentOption = new("--handling-agent")
        {
            Description = "Agent that will handle matching incidents"
        };

        public static readonly Option<string> ImpactedServiceOption = new("--impacted-service")
        {
            Description = "Impacted service name to filter on"
        };

        public static readonly Option<string> PriorityOption = new("--priority")
        {
            Description = "Priority level to filter on"
        };

        public static readonly Option<string> IncidentTypeOption = new("--incident-type")
        {
            Description = "Incident type to filter on"
        };

        public static readonly Option<string> AlertIdOption = new("--alert-id")
        {
            Description = "Alert ID to filter on"
        };

        public static readonly Option<string> TitleContainsOption = new("--title-contains")
        {
            Description = "Text that must be contained in the incident title"
        };

        public static readonly Option<string> AgentModeOption = new("--agent-mode")
        {
            Description = "Agent mode to use for matching incidents"
        };

        public static readonly Option<string> OwningTeamIdOption = new("--owning-team-id")
        {
            Description = "Owning team ID to filter on"
        };

        public static readonly Option<int?> MaxAutomatedInvestigationAttemptsOption = new("--max-investigation-attempts")
        {
            Description = "Maximum automated investigation attempts before requesting user input (default: 3)"
        };

        public static readonly Option<bool> DeepInvestigationEnabledOption = new("--deep-investigation")
        {
            Description = "Enable deep investigation for matching incidents"
        };

        public static readonly Option<bool> DisabledOption = new("--disabled")
        {
            Description = "Create the filter in disabled state"
        };

        // IcM-specific options
        public static readonly Option<string> MonitorIdOption = new("--monitor-id")
        {
            Description = "Monitor ID to filter on (IcM platform)"
        };

        public static readonly Option<string> CreatedByOption = new("--created-by")
        {
            Description = "Creator of the incident to filter on (IcM platform)"
        };

        // AzMonitor-specific options
        public static readonly Option<string> TargetResourceTypeOption = new("--target-resource-type")
        {
            Description = "Target resource type to filter on (AzMonitor platform)"
        };

        public static readonly Option<string> TargetResourceOption = new("--target-resource")
        {
            Description = "Target resource to filter on (AzMonitor platform)"
        };
    }

    // ============================================================
    // Incident-Filter Get Command Options
    // ============================================================

    public static class Get
    {
        public static readonly Option<string?> NameOption = new("--name")
        {
            Description = "Get a specific incident filter by name and output the full YAML"
        };

        public static readonly Option<bool> DetailOption = new("--detail")
        {
            Description = "Output the full YAML for all filters in the list"
        };
    }

    // ============================================================
    // Incident-Filter Apply Command Options
    // ============================================================

    public static class Apply
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the incident filter to apply",
            Required = true
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Show what would be applied without making changes"
        };
    }

    // ============================================================
    // Incident-Filter Delete Command Options
    // ============================================================

    public static class Delete
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the incident filter to delete",
            Required = true
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Show what would be deleted without making changes"
        };
    }
}
