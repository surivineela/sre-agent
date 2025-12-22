// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class StatusCommand
    {
        public static Command Build()
        {
            var cmd = new Command("status", CommandExamples.General.StatusDescription);

            cmd.SetAction(GeneralCommandHandlers.HandleStatusCommand);

            return cmd;
        }
    }
}
