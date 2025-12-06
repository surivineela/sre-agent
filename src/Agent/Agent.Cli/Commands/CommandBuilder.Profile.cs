// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class ProfileCommand
    {
        public static Command Build()
        {
            var cmd = new Command("profile", "Profile management commands. Profiles store connection settings for different SRE Agent instances (local or remote)")
            {
                CreateProfileListCommand(),
                CreateProfileGetCommand(),
                CreateProfileCreateCommand(),
                CreateProfileSetCommand(),
                CreateProfileDeleteCommand()
            };

            // Add default action for profile command to show formatted help
            cmd.SetAction(pr => ShowFormattedProfileHelp(cmd));

            return cmd;
        }

        private static Command CreateProfileListCommand()
        {
            var cmd = new Command("list", CommandExamples.Profile.ListDescription);
            cmd.SetAction(ProfileCommandHandlers.HandleListCommand);
            return cmd;
        }

        private static Command CreateProfileGetCommand()
        {
            var cmd = new Command("get", CommandExamples.Profile.GetDescription)
            {
                ProfileCommandOptions.ProfileNameOption
            };
            cmd.SetAction(ProfileCommandHandlers.HandleGetCommand);
            return cmd;
        }

        private static Command CreateProfileCreateCommand()
        {
            var cmd = new Command("create", CommandExamples.Profile.CreateDescription)
            {
                ProfileCommandOptions.ProfileNameRequiredOption,
                ProfileCommandOptions.ResourceUrlOption,
                ProfileCommandOptions.SetCurrentOption
            };
            cmd.SetAction(ProfileCommandHandlers.HandleCreateCommand);
            return cmd;
        }

        private static Command CreateProfileSetCommand()
        {
            var cmd = new Command("set", CommandExamples.Profile.SetDescription)
            {
                ProfileCommandOptions.ProfileNameRequiredOption
            };
            cmd.SetAction(ProfileCommandHandlers.HandleSetCommand);
            return cmd;
        }

        private static Command CreateProfileDeleteCommand()
        {
            var cmd = new Command("delete", CommandExamples.Profile.DeleteDescription)
            {
                ProfileCommandOptions.ProfileNameRequiredOption
            };
            cmd.SetAction(ProfileCommandHandlers.HandleDeleteCommand);
            return cmd;
        }
    }
}
