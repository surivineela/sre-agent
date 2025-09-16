// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static class ScheduledTaskCommandOptions
{
    // Common options
    public static readonly Option<string> TaskIdOption = new("--id")
    {
        Description = "The ID of the scheduled task"
    };

    public static readonly Option<bool> VerboseOption = new("--verbose")
    {
        Description = "Show detailed information"
    };

    // Create command options
    public static readonly Option<string> CreateNameOption = new("--name")
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

    // Note: older System.CommandLine version does not support AddAlias on Option; omit alias.

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

    // Update command options (all optional for updates)
    public static readonly Option<string> UpdateNameOption = new("--name")
    {
        Description = "New name for the scheduled task"
    };

    public static readonly Option<string> UpdateDescriptionOption = new("--description")
    {
        Description = "New description for the scheduled task"
    };

    public static readonly Option<string> UpdateCronExpressionOption = new("--cron")
    {
        Description = "New cron expression for scheduling"
    };

    public static readonly Option<string> UpdateAgentPromptOption = new("--prompt")
    {
        Description = "New prompt for the agent to execute"
    };

    public static readonly Option<string> UpdateAgentOption = new("--agent")
    {
        Description = "New agent to use for this task"
    };

    public static readonly Option<string> UpdateStatusOption = new("--status")
    {
        Description = "New status for the task (Active, Paused, Completed, Failed)"
    };

    // Filter options for list command
    public static readonly Option<string> FilterThreadIdOption = new("--thread-id")
    {
        Description = "Filter tasks by thread ID"
    };

    public static readonly Option<string> FilterStatusOption = new("--status")
    {
        Description = "Filter tasks by status (Active, Paused, Completed, Failed)"
    };

    // Required ID option for specific task operations
    public static readonly Option<string> RequiredTaskIdOption = new("--id")
    {
        Description = "The ID of the scheduled task",
        Required = true
    };

    // Quickstart/apply helpers
    public static readonly Option<string> QuickstartNameOption = new("--name")
    {
        Description = "Name for the hello-world task"
    };
    public static readonly Option<string> QuickstartCronOption = new("--cron")
    {
        Description = "Cron for the hello-world task (default: */15 * * * *)"
    };
    public static readonly Option<int> QuickstartDurationHoursOption = new("--duration-hours")
    {
        Description = "Duration hours for the task window (default: 1)"
    };
    public static readonly Option<string> QuickstartAgentOption = new("--agent")
    {
        Description = "Starting agent for the thread (optional)"
    };
    public static readonly Option<bool> QuickstartApplyOption = new("--apply")
    {
        Description = "Apply immediately after generating the manifest"
    };
}
