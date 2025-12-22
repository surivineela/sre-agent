// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static partial class CommandBuilder
{
    private static class McpCommand
    {
        public static Command Build()
        {
            var mcp = new Command("mcp", "Model Context Protocol server for building SRE agents")
            {
                CreateStartCommand(),
                CreateInfoCommand()
            };

            return mcp;
        }

        private static Command CreateStartCommand()
        {
            var cmd = new Command("start", CommandExamples.Mcp.StartDescription)
            {
                McpCommandOptions.Start.VerboseOption
            };

            cmd.SetAction(async pr => await McpCommandHandlers.HandleStartCommand(pr));
            return cmd;
        }

        private static Command CreateInfoCommand()
        {
            var cmd = new Command("info", CommandExamples.Mcp.InfoDescription)
            {
                McpCommandOptions.Info.TopicOption,
                McpCommandOptions.Info.AllOption
            };

            cmd.SetAction(pr => McpCommandHandlers.HandleInfoCommand(pr));
            return cmd;
        }
    }
}
