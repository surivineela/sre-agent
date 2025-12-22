// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class VersionCommand
    {
        public static Command Build()
        {
            var cmd = new Command("version", CommandExamples.General.VersionDescription);

            cmd.SetAction(GeneralCommandHandlers.HandleVersionCommand);

            return cmd;
        }
    }
}
