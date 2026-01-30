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
        /// If not specified, operate on all threads.
        /// </summary>
        public static readonly Option<Guid?> ThreadIdOption = new("--thread-id")
        {
            Description = "Thread ID for session insights (empty = all threads)"
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
