// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text;
using Agent.Cli.Helpers;

namespace Agent.Cli.Services;

/// <summary>
/// Service for generating comprehensive instructions.md file with all srectl commands and their help output.
/// </summary>
public static class InstructionsFileService
{
    /// <summary>
    /// Creates an instructions.md file in the .github folder with all srectl commands and their help outputs.
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    public static async Task CreateInstructionsFileAsync()
    {
        try
        {
            ConsoleUI.WriteBullet("Creating instructions.md file...", ConsoleColor.Cyan);

            // Create .github directory if it doesn't exist
            Directory.CreateDirectory(".github");

            var instructionsContent = await GenerateInstructionsContentAsync();

            var filePath = Path.Combine(".github", "instructions.md");

            await File.WriteAllTextAsync(filePath, instructionsContent);

            ConsoleUI.WriteStatus(true, $"Created instructions file: {filePath}");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Warning: Failed to create instructions file: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates the complete content for the instructions.md file.
    /// </summary>
    /// <returns>The markdown content as a string</returns>
    private static async Task<string> GenerateInstructionsContentAsync()
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# SRECTL - SRE Agent CLI Instructions");
        sb.AppendLine();
        sb.AppendLine("This file contains comprehensive documentation for all SRECTL commands and their usage.");
        sb.AppendLine($"Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("## Table of Contents");
        sb.AppendLine();
        sb.AppendLine("1. [Main Command](#main-command)");
        sb.AppendLine("2. [General Commands](#general-commands)");
        sb.AppendLine("   - [init](#init-command)");
        sb.AppendLine("   - [list](#list-command)");
        sb.AppendLine("   - [apply-yaml](#apply-yaml-command)");
        sb.AppendLine("3. [Agent Commands](#agent-commands)");
        sb.AppendLine("   - [agent create](#agent-create-command)");
        sb.AppendLine("   - [agent validate](#agent-validate-command)");
        sb.AppendLine("   - [agent apply](#agent-apply-command)");
        sb.AppendLine("   - [agent run](#agent-run-command)");
        sb.AppendLine("4. [Tool Commands](#tool-commands)");
        sb.AppendLine("   - [tool create](#tool-create-command)");
        sb.AppendLine("   - [tool validate](#tool-validate-command)");
        sb.AppendLine("   - [tool apply](#tool-apply-command)");
        sb.AppendLine("   - [tool show-types](#tool-show-types-command)");
        sb.AppendLine("   - [tool show-connectors](#tool-show-connectors-command)");
        sb.AppendLine();

        // Command definitions with help outputs
        var commands = new[]
        {
            new { Section = "Main Command", Title = "Main Command", Command = "srectl --help" },
            new { Section = "General Commands", Title = "init Command", Command = "srectl init --help" },
            new { Section = "", Title = "list Command", Command = "srectl list --help" },
            new { Section = "", Title = "list agents Command", Command = "srectl list agents --help" },
            new { Section = "", Title = "list tools Command", Command = "srectl list tools --help" },
            new { Section = "", Title = "apply-yaml Command", Command = "srectl apply-yaml --help" },
            new { Section = "Agent Commands", Title = "agent Command", Command = "srectl agent --help" },
            new { Section = "", Title = "agent create Command", Command = "srectl agent create --help" },
            new { Section = "", Title = "agent validate Command", Command = "srectl agent validate --help" },
            new { Section = "", Title = "agent apply Command", Command = "srectl agent apply --help" },
            new { Section = "", Title = "agent run Command", Command = "srectl agent run --help" },
            new { Section = "Tool Commands", Title = "tool Command", Command = "srectl tool --help" },
            new { Section = "", Title = "tool create Command", Command = "srectl tool create --help" },
            new { Section = "", Title = "tool validate Command", Command = "srectl tool validate --help" },
            new { Section = "", Title = "tool apply Command", Command = "srectl tool apply --help" },
            new { Section = "", Title = "tool show-types Command", Command = "srectl tool show-types --help" },
            new { Section = "", Title = "tool show-connectors Command", Command = "srectl tool show-connectors --help" }
        };

        foreach (var cmd in commands)
        {
            // Add section header if it's a new section
            if (!string.IsNullOrEmpty(cmd.Section))
            {
                sb.AppendLine($"## {cmd.Section}");
                sb.AppendLine();
            }

            // Add command title and anchor
            var anchor = cmd.Title.ToLower().Replace(" ", "-").Replace(".", "");
            sb.AppendLine($"### {cmd.Title} {{#{anchor}}}");
            sb.AppendLine();

            // Execute command and get help output
            var helpOutput = await ExecuteCommandAsync(cmd.Command);

            sb.AppendLine("```");
            sb.AppendLine($"$ {cmd.Command}");
            sb.AppendLine();
            sb.AppendLine(helpOutput);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Add usage examples section
        sb.AppendLine("## Common Usage Examples");
        sb.AppendLine();
        sb.AppendLine("### Quick Start");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Initialize SRECTL with a resource URL");
        sb.AppendLine("srectl init --resource-url https://localhost:7023");
        sb.AppendLine();
        sb.AppendLine("# Create a new agent");
        sb.AppendLine("srectl agent create --name my_agent --instructions \"Agent instructions\" --tools MyTool");
        sb.AppendLine();
        sb.AppendLine("# Validate the agent");
        sb.AppendLine("srectl agent validate --name my_agent");
        sb.AppendLine();
        sb.AppendLine("# Apply the agent to the server");
        sb.AppendLine("srectl agent apply --name my_agent");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("### Creating Tools");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Create a basic tool");
        sb.AppendLine("srectl tool create --name MyTool --type KustoQuery");
        sb.AppendLine();
        sb.AppendLine("# Create a KustoTool with auto-generated template");
        sb.AppendLine("srectl tool create --name GetServiceLogs --type KustoTool");
        sb.AppendLine();
        sb.AppendLine("# Validate and apply the tool");
        sb.AppendLine("srectl tool validate --name MyTool");
        sb.AppendLine("srectl tool apply --name MyTool");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("### Smart Agent Generation");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# Use AI to generate comprehensive agent instructions and recommended tools");
        sb.AppendLine("srectl agent create --name \"RedisContainerAppDown\" --smart");
        sb.AppendLine();
        sb.AppendLine("# Smart generation with custom guidance");
        sb.AppendLine("srectl agent create --name \"DatabasePerformanceIssue\" --smart \\");
        sb.AppendLine("  --instructions \"Focus on PostgreSQL performance optimization\"");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("### Remote Server Operations");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("# List all agents on the remote server");
        sb.AppendLine("srectl list agents");
        sb.AppendLine();
        sb.AppendLine("# List all tools on the remote server");
        sb.AppendLine("srectl list tools");
        sb.AppendLine();
        sb.AppendLine("# Apply a YAML file directly");
        sb.AppendLine("srectl apply-yaml --file my-config.yaml");
        sb.AppendLine("```");
        sb.AppendLine();

        // Add footer
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This file was automatically generated by `srectl init`. For the most up-to-date information,*");
        sb.AppendLine("*refer to the individual command help outputs using `srectl <command> --help`.*");

        return sb.ToString();
    }

    /// <summary>
    /// Executes a srectl command and returns its output.
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <returns>The command output</returns>
    private static async Task<string> ExecuteCommandAsync(string command)
    {
        try
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var executable = parts[0];
            var arguments = string.Join(" ", parts.Skip(1));

            var processInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return "Error: Could not start process";
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            // Return output or error message if command failed
            if (!string.IsNullOrEmpty(error) && string.IsNullOrEmpty(output))
            {
                return $"Error: {error}";
            }

            return output.TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Error executing command: {ex.Message}";
        }
    }
}
