// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for incidenthandler commands.
/// </summary>
public static class IncidentHandlerCommandOptions
{
    // ============================================================
    // IncidentHandler MapAgent Command Options
    // ============================================================

    public static class MapAgent
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "The name of the incident filter to map",
            Required = true
        };

        public static readonly Option<string> HandlingAgentOption = new("--handling-agent")
        {
            Description = "The name of the YAML agent to handle incidents for this filter",
            Required = true
        };
    }

    // ============================================================
    // IncidentHandler List Command Options
    // ============================================================

    public static class List
    {
        public static readonly Option<bool> VerboseOption = new("--verbose", "-v")
        {
            Description = "Show detailed information including filter details"
        };
    }

    // ============================================================
    // IncidentHandler Create Command Options
    // ============================================================

    public static class Create
    {
        public static readonly Option<string> IdOption = new("--id")
        {
            Description = "The unique identifier for the incident filter",
            Required = true
        };

        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "The name of the incident filter"
        };

        public static readonly Option<string> ImpactedServiceOption = new("--impacted-service")
        {
            Description = "The impacted service for the filter"
        };

        public static readonly Option<string> PriorityOption = new("--priority")
        {
            Description = "The priority level for incidents (e.g., 1, 2, 3, 4)"
        };

        public static readonly Option<string> IncidentTypeOption = new("--incident-type")
        {
            Description = "The type of incident (e.g., LiveSite, Monitoring)"
        };

        public static readonly Option<string> AlertIdOption = new("--alert-id")
        {
            Description = "The alert ID pattern to match"
        };

        public static readonly Option<string> TitleContainsOption = new("--title-contains")
        {
            Description = "Text that must be contained in the incident title"
        };

        public static readonly Option<string> AgentModeOption = new("--agent-mode")
        {
            Description = "The agent mode (e.g., autonomous, manual)",
            Required = false
        };

        public static readonly Option<string> HandlingAgentOption = new("--handling-agent")
        {
            Description = "The YAML agent to handle incidents for this filter"
        };

        public static readonly Option<string> OwningTeamIdOption = new("--owning-team-id")
        {
            Description = "The ID of the team that owns this filter"
        };

        public static readonly Option<int> MaxAttemptsOption = new("--max-attempts")
        {
            Description = "Maximum number of automated investigation attempts (default: 3)"
        };
    }
}
