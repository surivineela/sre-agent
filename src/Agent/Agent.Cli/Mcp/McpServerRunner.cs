// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Agent.Cli.Mcp;

public static class McpServerRunner
{
    public static async Task RunAsync(bool verbose, CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection();
        services
            .AddMcpServer(options => options.ServerInfo = new() { Name = "sreagent", Version = "1.0.0" })
            .WithStdioServerTransport()
            .WithTools<SreAgentTools>();

        var serviceProvider = services.BuildServiceProvider();
        var server = serviceProvider.GetRequiredService<McpServer>();

        if (verbose)
            Console.Error.WriteLine("MCP Server initialized with tools from SreAgentTools");

        await server.RunAsync(cancellationToken);
    }
}
