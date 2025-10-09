// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;

namespace Agent.Cli.Services;

/// <summary>
/// Service for providing intelligent command suggestions and auto-completion
/// </summary>
public static class CommandSuggestionService
{
    /// <summary>
    /// Suggest commands based on current context and user intent
    /// </summary>
    public static async Task<string[]> GetContextualSuggestions(string? currentInput = null)
    {
        var suggestions = new List<string>();

        // Check workspace state for context
        var configService = new CliConfigurationService();
        var hasConfig = await configService.HasValidConfigurationAsync();

        if (!hasConfig)
        {
            suggestions.AddRange(new[]
            {
                "srectl init --resource-url https://localhost:7023",
                "srectl init --resource-url https://your-agent-endpoint.ai",
                "srectl help quickstart"
            });
            return suggestions.ToArray();
        }

        // Context-aware suggestions based on workspace state
        var hasAgents = Directory.Exists("agents") && Directory.GetDirectories("agents").Length > 0;
        var hasTools = Directory.Exists("tools") && Directory.GetDirectories("tools").Length > 0;

        if (!hasAgents && !hasTools)
        {
            // First-time user suggestions
            suggestions.AddRange(new[]
            {
                "srectl agent create --name MyFirstAgent --smart",
                "srectl tool show-types",
                "srectl chat",
                "srectl help examples"
            });
        }
        else if (hasAgents && !hasTools)
        {
            // User has agents but no tools
            suggestions.AddRange(new[]
            {
                "srectl tool create --name QueryMetrics --type KustoTool",
                "srectl agent test --name [agent-name] --message 'Hello'",
                "srectl list agents",
                "srectl chat"
            });
        }
        else
        {
            // Experienced user suggestions
            suggestions.AddRange(new[]
            {
                "srectl agent apply --name [agent-name]",
                "srectl agent validate --all --check-tools",
                "srectl thread new --message 'Your question here'",
                "srectl chat",
                "srectl list agents"
            });
        }

        // Filter suggestions based on current input
        if (!string.IsNullOrEmpty(currentInput))
        {
            suggestions = suggestions
                .Where(s => s.Contains(currentInput, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return suggestions.ToArray();
    }

    /// <summary>
    /// Show smart suggestions when a command fails or user seems lost
    /// </summary>
    public static async Task ShowSmartSuggestions(string? failedCommand = null, string? errorContext = null)
    {
        Console.WriteLine("💡 Here are some suggestions to help you:");
        Console.WriteLine();

        var suggestions = await GetContextualSuggestions();

        // Add specific suggestions based on error context
        if (!string.IsNullOrEmpty(errorContext))
        {
            var contextSuggestions = GetErrorContextSuggestions(errorContext, failedCommand);
            suggestions = contextSuggestions.Concat(suggestions).ToArray();
        }

        for (int i = 0; i < Math.Min(5, suggestions.Length); i++)
        {
            Console.WriteLine($"   {i + 1}. {suggestions[i]}");
        }

        Console.WriteLine();
        Console.WriteLine("🔍 For more help:");
        Console.WriteLine("   • srectl help              # General help");
        Console.WriteLine("   • srectl help quickstart   # Step-by-step guide");
        Console.WriteLine("   • srectl help examples     # Real-world examples");
        Console.WriteLine();
    }

    /// <summary>
    /// Get suggestions based on specific error context
    /// </summary>
    private static string[] GetErrorContextSuggestions(string errorContext, string? failedCommand)
    {
        var error = errorContext.ToLowerInvariant();

        if (error.Contains("connection") || error.Contains("network") || error.Contains("unreachable"))
        {
            return new[]
            {
                "srectl profile get  # Check current server URL",
                "srectl list agents --debug  # Test connection with debug info",
                "srectl init --resource-url <correct-url>  # Update server URL"
            };
        }

        if (error.Contains("not found") || error.Contains("404"))
        {
            return new[]
            {
                "srectl list agents  # See what's actually deployed",
                "srectl agent apply --name [agent-name]  # Deploy the agent first",
                "srectl agent validate --all  # Check agent configurations"
            };
        }

        if (error.Contains("validation") || error.Contains("invalid"))
        {
            return new[]
            {
                "srectl agent validate --all --check-tools  # Detailed validation",
                "srectl tool show-types  # See available tool types",
                $"{failedCommand} --debug  # Run with debug output"
            };
        }

        if (error.Contains("permission") || error.Contains("auth") || error.Contains("unauthorized"))
        {
            return new[]
            {
                "srectl profile get  # Check authentication settings",
                "srectl init --resource-url <url>  # Reinitialize with auth",
                "Contact your admin for access permissions"
            };
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Interactive command builder for complex operations
    /// </summary>
    public static async Task<string?> InteractiveCommandBuilder(string baseCommand)
    {
        Console.WriteLine($"🛠️  Let's build a {baseCommand} command together!");
        Console.WriteLine();

        return baseCommand switch
        {
            "agent create" => await BuildAgentCreateCommand(),
            "tool create" => await BuildToolCreateCommand(),
            "agent test" => await BuildAgentTestCommand(),
            _ => null
        };
    }

    private static Task<string> BuildAgentCreateCommand()
    {
        Console.Write("📝 Agent name (required): ");
        var name = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("❌ Agent name is required");
            return Task.FromResult("");
        }

        Console.WriteLine();
        Console.WriteLine("🤖 Would you like AI assistance to generate instructions and recommend tools?");
        Console.Write("   Use --smart flag? [Y/n]: ");
        var useSmart = Console.ReadLine()?.Trim().ToLowerInvariant();
        var smartFlag = string.IsNullOrEmpty(useSmart) || useSmart.StartsWith('y');

        var command = new StringBuilder($"srectl agent create --name {name}");

        if (smartFlag)
        {
            command.Append(" --smart");
            Console.Write("💬 Brief description of what this agent should do: ");
            var description = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(description))
            {
                command.Append($" --instructions \"{description}\"");
            }
        }
        else
        {
            Console.Write("📋 Agent instructions (optional): ");
            var instructions = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(instructions))
            {
                command.Append($" --instructions \"{instructions}\"");
            }

            Console.Write("🔧 Tools to include (space-separated, optional): ");
            var tools = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(tools))
            {
                command.Append($" --tools {tools}");
            }
        }

        var finalCommand = command.ToString();
        Console.WriteLine();
        Console.WriteLine($"🚀 Generated command: {finalCommand}");
        Console.WriteLine();
        Console.Write("   Execute this command? [Y/n]: ");

        var execute = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(execute) || execute.StartsWith('y'))
        {
            return Task.FromResult(finalCommand);
        }

        return Task.FromResult("");
    }

    private static Task<string> BuildToolCreateCommand()
    {
        Console.Write("🔧 Tool name (required): ");
        var name = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("❌ Tool name is required");
            return Task.FromResult("");
        }

        Console.WriteLine();
        Console.WriteLine("📋 Available tool types:");
        Console.WriteLine("   • KustoTool     - Query Kusto/Log Analytics");
        Console.WriteLine("   • AzureTool     - Azure resource operations");
        Console.WriteLine("   • HttpTool      - HTTP API calls");
        Console.WriteLine("   • ScriptTool    - Custom scripts");
        Console.WriteLine();
        Console.Write("🎯 Tool type: ");
        var toolType = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(toolType))
        {
            Console.WriteLine("❌ Tool type is required");
            return Task.FromResult("");
        }

        var command = $"srectl tool create --name {name} --type {toolType}";

        Console.WriteLine();
        Console.WriteLine($"🚀 Generated command: {command}");
        Console.WriteLine();
        Console.Write("   Execute this command? [Y/n]: ");

        var execute = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(execute) || execute.StartsWith('y'))
        {
            return Task.FromResult(command);
        }

        return Task.FromResult("");
    }

    private static Task<string> BuildAgentTestCommand()
    {
        // Get available agents
        var agentDirs = Directory.Exists("agents") ? Directory.GetDirectories("agents") : Array.Empty<string>();

        if (agentDirs.Length == 0)
        {
            Console.WriteLine("❌ No agents found. Create an agent first with 'srectl agent create'");
            return Task.FromResult("");
        }

        Console.WriteLine("🤖 Available agents:");
        for (int i = 0; i < agentDirs.Length; i++)
        {
            var dirAgentName = Path.GetFileName(agentDirs[i]);
            Console.WriteLine($"   {i + 1}. {dirAgentName}");
        }

        Console.Write("👆 Select agent (number or name): ");
        var selection = Console.ReadLine()?.Trim();

        string? agentName = null;
        if (int.TryParse(selection, out var index) && index > 0 && index <= agentDirs.Length)
        {
            agentName = Path.GetFileName(agentDirs[index - 1]);
        }
        else if (!string.IsNullOrEmpty(selection))
        {
            agentName = selection;
        }

        if (string.IsNullOrEmpty(agentName))
        {
            Console.WriteLine("❌ Valid agent selection required");
            return Task.FromResult("");
        }

        Console.Write("💬 Test message: ");
        var message = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(message))
        {
            Console.WriteLine("❌ Test message is required");
            return Task.FromResult("");
        }

        var command = $"srectl agent test --name {agentName} --message \"{message}\"";

        Console.WriteLine();
        Console.WriteLine($"🚀 Generated command: {command}");
        Console.WriteLine();
        Console.Write("   Execute this command? [Y/n]: ");

        var execute = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(execute) || execute.StartsWith('y'))
        {
            return Task.FromResult(command);
        }

        return Task.FromResult("");
    }

    /// <summary>
    /// Show available options for a command in a user-friendly way
    /// </summary>
    public static void ShowCommandOptions(string command)
    {
        var options = GetCommandOptions(command);

        Console.WriteLine($"⚙️  Available options for '{command}':");
        Console.WriteLine();

        foreach (var option in options)
        {
            Console.WriteLine($"   {option.Flag.PadRight(20)} {option.Description}");
            if (!string.IsNullOrEmpty(option.Example))
            {
                Console.WriteLine($"                         Example: {option.Example}");
            }
            Console.WriteLine();
        }
    }

    private static List<CommandOption> GetCommandOptions(string command)
    {
        // This would be expanded to include all command options
        return command switch
        {
            "agent create" => new List<CommandOption>
            {
                new("--name", "Agent name (required)", "MyAgent"),
                new("--smart", "Use AI assistance", ""),
                new("--instructions", "Agent instructions", "\"Help with DevOps tasks\""),
                new("--tools", "Tools to include", "Tool1 Tool2"),
                new("--debug", "Enable debug logging", "")
            },
            "tool create" => new List<CommandOption>
            {
                new("--name", "Tool name (required)", "QueryMetrics"),
                new("--type", "Tool type (required)", "KustoTool"),
                new("--path", "Organization path", "\"Metrics/Performance\""),
                new("--debug", "Enable debug logging", "")
            },
            _ => new List<CommandOption>()
        };
    }

    private record CommandOption(string Flag, string Description, string Example);
}
