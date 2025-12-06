// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class InitCommand
    {
        public static Command Build()
        {
            var cmd = new Command("init", CommandExamples.General.InitDescription)
            {
                InitCommandOptions.ResourceUrlOption
            };

            // Guard so flow analysis knows it's enforced at runtime
            cmd.AddValidator(pr =>
            {
                var url = pr.GetValue(InitCommandOptions.ResourceUrlOption);

                if (string.IsNullOrWhiteSpace(url))
                {
                    pr.AddError(ErrorMessageHelper.InvalidParameter("--resource-url must be provided and non-empty."));
                }
            });

            cmd.SetAction(InitCommandHandler.HandleCommand);

            return cmd;
        }
    }
}
