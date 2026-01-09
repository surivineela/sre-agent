// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class HelpCommand
    {
        public static Command Build()
        {
            var cmd = new Command("help", CommandExamples.General.HelpDescription)
            {
                HelpCommandOptions.OutputOption
            };

            // Make the command hidden so it doesn't show in `srectl --help`
            cmd.Hidden = true;

            cmd.SetAction(HelpCommandHandlers.HandleHelpCommand);

            return cmd;
        }
    }
}
