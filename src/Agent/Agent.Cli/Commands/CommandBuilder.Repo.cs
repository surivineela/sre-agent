// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class RepoCommand
    {
        public static Command Build()
        {
            var cmd = new Command("repo", "Manage Azure DevOps repository connectors for TSG documents")
            {
                CreateRepoAddCommand(),
                CreateRepoUpdateCommand(),
                CreateRepoRemoveCommand(),
                CreateRepoListCommand()
            };

            return cmd;
        }

        private static Command CreateRepoAddCommand()
        {
            var cmd = new Command("add", CommandExamples.Repo.AddDescription)
            {
                RepoCommandOptions.NameOption,
                RepoCommandOptions.UrlOption,
                RepoCommandOptions.PatOption
            };
            cmd.SetAction(RepoCommandHandlers.HandleAddCommand);
            return cmd;
        }

        private static Command CreateRepoUpdateCommand()
        {
            var cmd = new Command("update", CommandExamples.Repo.UpdateDescription)
            {
                RepoCommandOptions.NameOption,
                RepoCommandOptions.PatOption,
                RepoCommandOptions.RegenerateOption
            };

            // Add validator to ensure --pat and --regenerate are mutually exclusive
            cmd.AddValidator(result =>
            {
                var patValue = result.GetValue(RepoCommandOptions.PatOption);
                var regenerateValue = result.GetValue(RepoCommandOptions.RegenerateOption);

                if (!string.IsNullOrEmpty(patValue) && regenerateValue)
                {
                    result.AddError("Options --pat and --regenerate cannot be used together.");
                }

                if (string.IsNullOrEmpty(patValue) && !regenerateValue)
                {
                    result.AddError("Either --pat or --regenerate must be specified.");
                }
            });

            cmd.SetAction(RepoCommandHandlers.HandleUpdateCommand);
            return cmd;
        }

        private static Command CreateRepoRemoveCommand()
        {
            var cmd = new Command("remove", CommandExamples.Repo.RemoveDescription)
            {
                RepoCommandOptions.NameOption,
                RepoCommandOptions.ForceOption
            };
            cmd.SetAction(RepoCommandHandlers.HandleRemoveCommand);
            return cmd;
        }

        private static Command CreateRepoListCommand()
        {
            var cmd = new Command("list", CommandExamples.Repo.ListDescription);
            cmd.SetAction(RepoCommandHandlers.HandleListCommand);
            return cmd;
        }
    }
}
