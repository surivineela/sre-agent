// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class ExtensionCommand
    {
        public static Command Build()
        {
            var extension = new Command("extension", "Extension commands for generating deployment files and configurations")
            {
                CreateGenerateEv2Command()
            };

            // Add default action for extension command to show formatted help
            extension.SetAction(pr => ShowFormattedExtensionHelp(extension));
            return extension;
        }

        private static Command CreateGenerateEv2Command()
        {
            var cmd = new Command("generate-ev2", CommandExamples.Extension.GenerateEv2Description)
            {
                ExtensionCommandOptions.GenerateEv2.ToolsFolderOption,
                ExtensionCommandOptions.GenerateEv2.AgentFolderOption,
                ExtensionCommandOptions.GenerateEv2.OutputOption
            };

            cmd.SetAction(ExtensionCommandHandlers.HandleGenerateEv2Command);
            return cmd;
        }
    }
}
