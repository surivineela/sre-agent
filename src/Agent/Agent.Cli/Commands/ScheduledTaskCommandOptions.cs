// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for scheduledtask commands.
/// </summary>
public static class ScheduledTaskCommandOptions
{
    // ============================================================
    // ScheduledTask Create Command Options
    // ============================================================

    public static class Create
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the scheduled task",
            Required = true
        };

        public static readonly Option<string> DescriptionOption = new("--description")
        {
            Description = "Description of the scheduled task"
        };

        public static readonly Option<string> CronExpressionOption = new("--cron")
        {
            Description = "Cron expression for scheduling (e.g., '0 0 * * *' for daily at midnight)",
            Required = true
        };

        public static readonly Option<string> AgentPromptOption = new("--prompt")
        {
            Description = "The prompt for the agent to execute",
            Required = true
        };

        public static readonly Option<string> AgentOption = new("--agent")
        {
            Description = "The name of the agent to use for this task"
        };

        public static readonly Option<string> StartTimeOption = new("--start-time")
        {
            Description = "Start time for the task (ISO 8601 format)"
        };

        public static readonly Option<string> EndTimeOption = new("--end-time")
        {
            Description = "End time for the task (ISO 8601 format)"
        };

        public static readonly Option<string> ThreadIdOption = new("--thread-id")
        {
            Description = "Thread ID to associate with the task"
        };

        public static readonly Option<int> MaxExecutionsOption = new("--max-executions")
        {
            Description = "Maximum number of executions (default: unlimited)"
        };

        public static readonly Option<string> NotificationChannelOption = new("--notification-channel")
        {
            Description = "Notification channel for task updates"
        };
    }

    // ============================================================
    // ScheduledTask List Command Options
    // ============================================================

    public static class List
    {
        public static readonly Option<bool> VerboseOption = new("--verbose")
        {
            Description = "Show detailed information"
        };

        public static readonly Option<string> FilterThreadIdOption = new("--thread-id")
        {
            Description = "Filter tasks by thread ID"
        };

        public static readonly Option<string> FilterStatusOption = new("--status")
        {
            Description = "Filter tasks by status (Active, Paused, Completed, Failed)"
        };
    }

    // ============================================================
    // ScheduledTask Get Command Options
    // ============================================================

    public static class Get
    {
        public static readonly Option<string> TaskIdOption = new("--id")
        {
            Description = "The ID of the scheduled task",
            Required = true
        };
    }

    // ============================================================
    // ScheduledTask Pause Command Options
    // ============================================================

    public static class Pause
    {
        public static readonly Option<string> TaskIdOption = new("--id")
        {
            Description = "The ID of the scheduled task",
            Required = true
        };
    }

    // ============================================================
    // ScheduledTask Resume Command Options
    // ============================================================

    public static class Resume
    {
        public static readonly Option<string> TaskIdOption = new("--id")
        {
            Description = "The ID of the scheduled task",
            Required = true
        };
    }

    // ============================================================
    // ScheduledTask Delete Command Options
    // ============================================================

    public static class Delete
    {
        public static readonly Option<string> TaskIdOption = new("--id")
        {
            Description = "The ID of the scheduled task",
            Required = true
        };
    }

    // ============================================================
    // ScheduledTask Quickstart Command Options
    // ============================================================

    public static class Quickstart
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name for the hello-world task"
        };

        public static readonly Option<string> CronOption = new("--cron")
        {
            Description = "Cron for the hello-world task (default: */15 * * * *)"
        };

        public static readonly Option<int> DurationHoursOption = new("--duration-hours")
        {
            Description = "Duration hours for the task window (default: 1)"
        };

        public static readonly Option<string> AgentOption = new("--agent")
        {
            Description = "Starting agent for the thread (optional)"
        };

        public static readonly Option<bool> ApplyOption = new("--apply")
        {
            Description = "Apply immediately after generating the manifest"
        };
    }

    // ============================================================
    // ScheduledTask Apply Command Options
    // ============================================================

    public static class Apply
    {
        public static readonly Option<string> FileOption = new("--file")
        {
            Description = "Path to the YAML manifest file",
            Required = true
        };
    }
}
