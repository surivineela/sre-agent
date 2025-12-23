// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class ApplyYamlCommand
    {
        public static Command Build()
        {
            var cmd = new Command("apply-yaml", CommandExamples.General.ApplyYamlDescription)
            {
                ApplyYamlCommandOptions.FileOption
            };

            // Add 'apply' as an alias
            cmd.Aliases.Add("apply");

            cmd.AddValidator(result =>
            {
                var filePath = result.GetValue(ApplyYamlCommandOptions.FileOption);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    result.AddError(ErrorMessageHelper.InvalidParameter("--file cannot be empty"));
                }
            });

            cmd.SetAction(Commands.ApplyYamlCommandHandlers.HandleCommand);

            return cmd;
        }
    }
}
