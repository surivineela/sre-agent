// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class ApplyCommand
    {
        public static Command Build()
        {
            var cmd = new Command("apply", CommandExamples.General.ApplyDescription)
            {
                ApplyYamlCommandOptions.FileOption
            };

            cmd.AddValidator(result =>
            {
                var filePath = result.GetValue(ApplyYamlCommandOptions.FileOption);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("-f/--file cannot be empty"));
                }
            });

            cmd.SetAction(Commands.ApplyYamlCommand.HandleCommand);

            return cmd;
        }
    }
}
