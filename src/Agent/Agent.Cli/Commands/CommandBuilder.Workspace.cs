// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class WorkspaceCommand
    {
        // Memory folder constants (same as in AgentFileStorageService)
        public const string RepoInstructionsFolderName = ".github";
        public const string SessionInsightsFolderName = "sessionInsights";
        public const string SynthesizedKnowledgeFolderName = "synthesizedKnowledge";

        public static Command Build()
        {
            var workspace = new Command("workspace", "Workspace management commands. Upload, download, and delete workspace files.")
            {
                CreateRepoInstructionsCommand(),
                CreateSessionInsightsCommand(),
                CreateSynthesizedKnowledgeCommand()
            };

            return workspace;
        }

        #region Repo Instructions Commands

        private static Command CreateRepoInstructionsCommand()
        {
            var cmd = new Command("repo-instructions", "Manage repository instructions (.github folder).")
            {
                CreateRepoInstructionsUploadCommand(),
                CreateRepoInstructionsDownloadCommand(),
                CreateRepoInstructionsDeleteCommand()
            };

            return cmd;
        }

        private static Command CreateRepoInstructionsUploadCommand()
        {
            var cmd = new Command("upload", CommandExamples.Workspace.RepoInstructionsUploadDescription)
            {
                WorkspaceCommandOptions.Memory.PathOption,
                WorkspaceCommandOptions.Memory.RepoOption
            };

            cmd.SetAction(WorkspaceCommandHandlers.HandleRepoInstructionsUploadCommand);
            return cmd;
        }

        private static Command CreateRepoInstructionsDownloadCommand()
        {
            var cmd = new Command("download", CommandExamples.Workspace.RepoInstructionsDownloadDescription)
            {
                WorkspaceCommandOptions.Memory.PathOption,
                WorkspaceCommandOptions.Memory.RepoOption
            };

            cmd.SetAction(WorkspaceCommandHandlers.HandleRepoInstructionsDownloadCommand);
            return cmd;
        }

        private static Command CreateRepoInstructionsDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Workspace.RepoInstructionsDeleteDescription)
            {
                WorkspaceCommandOptions.Memory.RepoOption
            };

            cmd.SetAction(WorkspaceCommandHandlers.HandleRepoInstructionsDeleteCommand);
            return cmd;
        }

        #endregion

        #region Session Insights Commands

        private static Command CreateSessionInsightsCommand()
        {
            var cmd = new Command("session-insights", "Manage session insights.")
            {
                CreateSessionInsightsUploadCommand(),
                CreateSessionInsightsDownloadCommand(),
                CreateSessionInsightsDeleteCommand()
            };

            return cmd;
        }

        private static Command CreateSessionInsightsUploadCommand()
        {
            var cmd = new Command("upload", CommandExamples.Workspace.SessionInsightsUploadDescription)
            {
                WorkspaceCommandOptions.Memory.PathOption,
                WorkspaceCommandOptions.Memory.ThreadIdOption
            };

            cmd.SetAction(WorkspaceCommandHandlers.HandleSessionInsightsUploadCommand);
            return cmd;
        }

        private static Command CreateSessionInsightsDownloadCommand()
        {
            var cmd = new Command("download", CommandExamples.Workspace.SessionInsightsDownloadDescription)
            {
                WorkspaceCommandOptions.Memory.PathOption,
                WorkspaceCommandOptions.Memory.ThreadIdOption
            };

            cmd.SetAction(WorkspaceCommandHandlers.HandleSessionInsightsDownloadCommand);
            return cmd;
        }

        private static Command CreateSessionInsightsDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Workspace.SessionInsightsDeleteDescription)
            {
                WorkspaceCommandOptions.Memory.ThreadIdOption
            };

            cmd.SetAction(WorkspaceCommandHandlers.HandleSessionInsightsDeleteCommand);
            return cmd;
        }

        #endregion

        #region Synthesized Knowledge Commands

        private static Command CreateSynthesizedKnowledgeCommand()
        {
            var cmd = new Command("synthesized-knowledge", "Manage synthesized knowledge.")
            {
                CreateSynthesizedKnowledgeUploadCommand(),
                CreateSynthesizedKnowledgeDownloadCommand(),
                CreateSynthesizedKnowledgeDeleteCommand()
            };

            return cmd;
        }

        private static Command CreateSynthesizedKnowledgeUploadCommand()
        {
            var cmd = new Command("upload", CommandExamples.Workspace.SynthesizedKnowledgeUploadDescription)
            {
                WorkspaceCommandOptions.Memory.PathOption
            };

            cmd.SetAction(WorkspaceCommandHandlers.HandleSynthesizedKnowledgeUploadCommand);
            return cmd;
        }

        private static Command CreateSynthesizedKnowledgeDownloadCommand()
        {
            var cmd = new Command("download", CommandExamples.Workspace.SynthesizedKnowledgeDownloadDescription)
            {
                WorkspaceCommandOptions.Memory.PathOption
            };

            cmd.SetAction(WorkspaceCommandHandlers.HandleSynthesizedKnowledgeDownloadCommand);
            return cmd;
        }

        private static Command CreateSynthesizedKnowledgeDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Workspace.SynthesizedKnowledgeDeleteDescription)
            {
            };

            cmd.SetAction(WorkspaceCommandHandlers.HandleSynthesizedKnowledgeDeleteCommand);
            return cmd;
        }

        #endregion
    }
}
