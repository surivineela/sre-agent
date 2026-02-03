// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Common.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for workspace commands.
/// </summary>
public static class WorkspaceCommandOptions
{
    // ============================================================
    // Workspace Memory Command Options
    // ============================================================

    public static class Memory
    {
        /// <summary>
        /// Gets the default memory path based on the local sandbox configuration.
        /// </summary>
        private static string GetDefaultMemoryPath()
        {
            try
            {
                return new LocalSandboxPaths().SandboxPaths.MemoriesPath;
            }
            catch
            {
                // Fallback if LocalSandboxPaths fails to initialize
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "sreagent",
                    "terminalRoot",
                    "memories");
            }
        }

        /// <summary>
        /// Local path for memory files. Defaults to the local sandbox memories path.
        /// </summary>
        public static readonly Option<string> PathOption = new("--path")
        {
            Description = "Local path for memory files (default: local sandbox memories path)"
        };

        /// <summary>
        /// Repository name for repo-instructions operations.
        /// </summary>
        public static readonly Option<string?> RepoOption = new("--repo")
        {
            Description = "Repository name for repo-instructions operations"
        };

        /// <summary>
        /// Thread ID for session insights operations.
        /// Use "*" or omit to operate on all threads.
        /// </summary>
        public static readonly Option<string> ThreadIdOption = new("--thread-id")
        {
            Description = "Thread ID for session insights (use '*' or omit for all threads)"
        };

        /// <summary>
        /// Parses thread ID string to Guid. Returns null for "*" or empty (meaning all threads).
        /// </summary>
        public static Guid? ParseThreadId(string? threadIdStr)
        {
            if (string.IsNullOrWhiteSpace(threadIdStr) || threadIdStr == "*")
            {
                return null;
            }
            if (!Guid.TryParse(threadIdStr, out var guid))
            {
                throw new ArgumentException($"Invalid thread ID: '{threadIdStr}'. Must be a valid GUID or '*' for all threads.");
            }
            return guid;
        }

        /// <summary>
        /// Flag to include session insights in sync operations.
        /// </summary>
        public static readonly Option<bool> IncludeSessionInsightsOption = new("--include-session-insights")
        {
            Description = "Include session insights in sync operation (all threads unless --thread-id specified)"
        };

        /// <summary>
        /// Gets the effective path, using the default if not specified.
        /// </summary>
        public static string GetEffectivePath(string? specifiedPath)
        {
            return string.IsNullOrWhiteSpace(specifiedPath) ? GetDefaultMemoryPath() : specifiedPath;
        }
    }
}
