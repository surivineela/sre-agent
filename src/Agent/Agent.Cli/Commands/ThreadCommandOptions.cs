// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for thread commands.
/// </summary>
public static class ThreadCommandOptions
{
    // ============================================================
    // Thread New Command Options
    // ============================================================

    public static class New
    {
        public static readonly Option<string> MessageOption = new("--message")
        {
            Description = "Message to send automatically after starting the session"
        };

        public static readonly Option<string> AgentNameOption = new("--agent")
        {
            Description = "Agent to start chatting with"
        };

        public static readonly Option<string> UserIdOption = new("--user-id")
        {
            Description = "[DEPRECATED] User ID (obtained from token)"
        };

        public static readonly Option<string> DisplayNameOption = new("--display-name")
        {
            Description = "[DEPRECATED] Display name (obtained from token)"
        };

        public static readonly Option<bool> WaitOption = new("--wait")
        {
            Description = "[DEPRECATED] Always starts interactive session",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> NoWaitOption = new("--no-wait")
        {
            Description = "Send message without waiting for response (requires --message)"
        };
    }

    // ============================================================
    // Thread Continue Command Options
    // ============================================================

    public static class Continue
    {
        public static readonly Option<string> ThreadIdOption = new("--thread-id")
        {
            Description = "Thread ID (uses last thread if omitted)"
        };

        public static readonly Option<string> MessageOption = new("--message")
        {
            Description = "Message to send automatically after starting the session"
        };

        public static readonly Option<string> UserIdOption = new("--user-id")
        {
            Description = "[DEPRECATED] User ID (obtained from token)"
        };

        public static readonly Option<string> DisplayNameOption = new("--display-name")
        {
            Description = "[DEPRECATED] Display name (obtained from token)"
        };

        public static readonly Option<bool> WaitOption = new("--wait")
        {
            Description = "[DEPRECATED] Always starts interactive session",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> NoWaitOption = new("--no-wait")
        {
            Description = "Send message without waiting for response (requires --message)"
        };
    }

    // ============================================================
    // Thread Delete Command Options
    // ============================================================

    public static class Delete
    {
        public static readonly Option<string> ThreadIdOption = new("--thread-id")
        {
            Description = "Thread ID to delete",
            Required = true
        };
    }

    // ============================================================
    // Thread Track Command Options
    // ============================================================

    public static class Track
    {
        public static readonly Option<string> ThreadIdOption = new("--thread-id")
        {
            Description = "Thread ID to track",
            Required = true
        };
    }

    // ============================================================
    // Thread Apply Command Options
    // ============================================================

    public static class Apply
    {
        public static readonly Option<string> FileOption = new("--file")
        {
            Description = "Path to YAML file to apply"
        };
    }
}
