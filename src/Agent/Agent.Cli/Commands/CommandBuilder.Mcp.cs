// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

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

            // Add default action for mcp command to show formatted help
            mcp.SetAction(pr => ShowFormattedMcpHelp(mcp));
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

    private static void ShowFormattedMcpHelp(Command cmd)
    {
        ConsoleUI.WriteSection("MCP - Model Context Protocol Server", ConsoleColor.Cyan);
        Console.WriteLine();

        ConsoleUI.WriteBullet("The MCP server provides tools for AI assistants to help build SRE agents.", ConsoleColor.White);
        ConsoleUI.WriteBullet("Run 'srectl mcp start' to start the MCP server for integration with AI tools.", ConsoleColor.White);
        Console.WriteLine();

        ConsoleUI.WriteCommandGroup("Available Commands", new[]
        {
            ("start", "Start the MCP server using stdio transport"),
            ("info", "Display information about SRE agent building concepts")
        });

        Console.WriteLine();
        ConsoleUI.WriteExamples(new[]
        {
            ("# Start MCP server (stdio transport)", "srectl mcp start"),
            ("# Start MCP server with verbose logging", "srectl mcp start --verbose"),
            ("# Get info about all agent building concepts", "srectl mcp info --all"),
            ("# Get specific info about triggers", "srectl mcp info --topic triggers")
        }, 0);
    }
}
