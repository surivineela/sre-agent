// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class WelcomeCommand
    {
        public static Command Build()
        {
            var cmd = new Command("welcome", CommandExamples.General.WelcomeDescription);

            cmd.SetAction(GeneralCommandHandlers.HandleWelcomeCommand);

            return cmd;
        }
    }
}
