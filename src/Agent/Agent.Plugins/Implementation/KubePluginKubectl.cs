// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Helpers;
using k8s;
using k8s.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

namespace Agent.Plugins
{
    public partial class KubePlugin : IKubePlugin
    {
        public async Task<string> RunKubectlCommandHelpAsync(
            string resourceId,
            string command)
        {
            try
            {
                return await ExecuteCommandSafely(resourceId, $"{command} --help");
            }
            catch (Exception ex)
            {
                return $"[Exception encountered]: Failed to execute command: {ex.ToString()}";
            }
        }

        public async Task<string> RunKubectlReadCommandAsync(
            string resourceId,
            string command)
        {
            // Validate command format
            var validationSummary = ValidateKubectlReadCommand(command.Trim());
            if (validationSummary != null)
            {
                return validationSummary; // Return the validation error message
            }

            // Execute the command
            try
            {
                return await ExecuteCommandSafely(resourceId, command);
            }
            catch (Exception ex)
            {
                return $"[Exception encountered]: Failed to execute command: {ex.ToString()}";
            }
        }

        public async Task<string> RunKubectlWriteCommandAsync(
            string resourceId,
            string command)
        {
            // Validate command format
            var validationSummary = ValidateKubectlWriteCommand(command.Trim());
            if (validationSummary != null)
            {
                return validationSummary; // Return the validation error message
            }

            // Execute the command
            try
            {
                return await ExecuteCommandSafely(resourceId, command);
            }
            catch (Exception ex)
            {
                return $"[Exception encountered]: Failed to execute command: {ex.ToString()}";
            }
        }

        private string? CommonValidateKubectlCommand(string command)
        {
            // 1. Validate that "kubectl" only appears once (at the beginning)
            var kubectlCount = Regex.Matches(command, @"\bkubectl\b", RegexOptions.IgnoreCase).Count;
            if (kubectlCount != 1)
            {
                return "[Validation Failed]: Command must contain exactly one 'kubectl' keyword";
            }

            // 2. Check for dangerous characters that could indicate command injection
            var dangerousPatterns = new string[]
            {
                ";",        // Command separator
                "&&",       // Command chaining
                "||",       // Command chaining
                "|",        // Pipe (could be dangerous)
                ">",        // Output redirection
                "<",        // Input redirection
                "`",        // Command substitution
                "$(",       // Command substitution
                "\\",       // Escape character
                "\n",       // Newline
                "\r"        // Carriage return
            };

            foreach (var pattern in dangerousPatterns)
            {
                if (command.Contains(pattern))
                {
                    return $"[Validation Failed]: Command contains potentially dangerous character(s): {pattern}";
                }
            }

            return null; // No validation errors
        }

        private string? ValidateKubectlReadCommand(string command)
        {
            // Common validation for kubectl commands
            var commonValidationError = CommonValidateKubectlCommand(command);
            if (commonValidationError != null)
            {
                return commonValidationError; // Return the common validation error
            }

            var subcommand = ParseKubectlSubcommand(command);
            switch (subcommand)
            {
                case "get":
                    var oMatches = Regex.Matches(command, @"(^|\s)-o\s+[^\s]+", RegexOptions.IgnoreCase);
                    if (oMatches.Count == 0)
                        return "[Validation Failed]: Command must include the '-o' output option";
                    if (oMatches.Count > 1)
                        return "[Validation Failed]: Command must contain only one '-o' option";

                    var fmt = Regex.Match(command, @"(^|\s)-o\s+(?<fmt>[^\s]+)", RegexOptions.IgnoreCase)
                        .Groups["fmt"].Value;

                    bool allowed =
                        fmt.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                        fmt.Equals("wide", StringComparison.OrdinalIgnoreCase) ||
                        fmt.StartsWith("custom-columns", StringComparison.OrdinalIgnoreCase);

                    if (!allowed)
                        return $"[Validation Failed]: Unsupported '-o' value '{fmt}'. Allowed: name, wide, custom-columns[=...]";
                    break;
                case "describe":
                case "logs":
                case "exec":
                case "top":
                case "api-resources":
                case "api-versions":
                    // These are valid subcommands for read operations
                    break;
                default:
                    return $"[Validation Failed]: Unsupported subcommand '{subcommand}'. Supported: get, describe, logs, exec, top, api-resources, api-versions";
            }

            return null; // No validation errors
        }

        private string? ValidateKubectlWriteCommand(string command)
        {
            // Common validation for kubectl commands
            var commonValidationError = CommonValidateKubectlCommand(command);
            if (commonValidationError != null)
            {
                return commonValidationError; // Return the common validation error
            }

            var subcommand = ParseKubectlSubcommand(command);
            switch (subcommand)
            {
                case "apply":
                case "create":
                case "delete":
                case "patch":
                case "replace":
                case "scale":
                case "label":
                case "annotate":
                case "set":
                case "rollout":
                    // These are valid subcommands for write operations
                    break;
                case "get":
                case "describe":
                case "logs":
                case "exec":
                case "top":
                case "api-resources":
                case "api-versions":
                    return $"[Validation Failed]: '{subcommand}' is a read-only command. Use RunKubectlReadCommandAsync instead.";
                default:
                    return $"[Validation Failed]: Unsupported subcommand '{subcommand}'. Supported write commands: apply, create, delete, patch, replace, scale, label, annotate, set, rollout";
            }

            // For apply and create commands, ensure we have a -f flag or YAML content
            if (subcommand == "apply" || subcommand == "create")
            {
                var hasFile = Regex.IsMatch(command, @"(^|\s)-f\s+[^\s]+", RegexOptions.IgnoreCase);
                var hasYaml = command.Contains("--yaml") || command.Contains("--filename=");

                if (!hasFile && !hasYaml)
                {
                    return $"[Validation Failed]: '{subcommand}' command must include a file reference (-f flag) or YAML content.";
                }
            }

            // For delete commands, ensure we have a specific resource type and name
            if (subcommand == "delete")
            {
                var resourceSpecified = Regex.IsMatch(command, @"delete\s+(\w+/\w+|\w+\s+\w+)", RegexOptions.IgnoreCase);
                if (!resourceSpecified)
                {
                    return "[Validation Failed]: Delete commands must specify a resource type and name to avoid accidental bulk deletions.";
                }
            }

            // Ensure rollout commands are restrictive
            if (subcommand == "rollout")
            {
                var validRolloutCommands = new[] { "restart", "undo", "pause", "resume", "status", "history" };
                var rolloutAction = ParseRolloutAction(command);

                if (rolloutAction == null || !validRolloutCommands.Contains(rolloutAction))
                {
                    return $"[Validation Failed]: Unsupported rollout action. Supported: {string.Join(", ", validRolloutCommands)}";
                }
            }

            return null; // No validation errors
        }

        private string? ParseRolloutAction(string command)
        {
            var match = Regex.Match(command, @"kubectl\s+rollout\s+(?<action>\w+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["action"].Value.ToLowerInvariant() : null;
        }

        private async Task<string> ExecuteCommandSafely(
            string resourceId,
            string command)
        {
            // get the cluster kubeconfig
            // todo: change to create a `view` clusterrolebinding with a service account, and use that guy's config
            var kubeConfig = await _kubernetesClientFactory.GetOrAddCachedK8SConfiguration(resourceId);
            if (kubeConfig is null)
            {
                return $"[Unexpected Error]: Unable to retrieve kubeconfig for cluster.";
            }

            // write to temp file
            var kubeConfigPath = Path.GetTempFileName();
            await File.WriteAllTextAsync(kubeConfigPath, _configJsonSerializer.Serialize(kubeConfig.Configuration));

            var cmd = command.Substring("kubectl ".Length); // Remove "kubectl " prefix

            return await ExecuteCommandHelper.ExecuteCommand(
                "kubectl",
                cmd,
                $"--kubeconfig=\"{kubeConfigPath}\"",
                $"--cache-dir=\"{Path.Combine(Path.GetTempPath(), ".kube")}\"");
        }



        /// <summary>
        /// Parses the subcommand from a kubectl command.
        /// </summary>
        /// <param name="command">The kubectl command to parse</param>
        /// <returns>The subcommand (e.g., 'get', 'logs', 'describe', etc.) or null if parsing failed</returns>
        private string? ParseKubectlSubcommand(string command)
        {
            // First, ensure the command starts with kubectl
            if (string.IsNullOrEmpty(command) || !command.StartsWith("kubectl", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning("Command does not start with kubectl: {Command}", command);
                return null;
            }

            // Split the command into parts
            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Must have at least kubectl + subcommand
            if (parts.Length < 2)
            {
                return null;
            }            // Find the subcommand, skipping flags and their values
            for (int i = 1; i < parts.Length; i++)
            {
                // Skip flags that start with -
                if (parts[i].StartsWith("-"))
                {
                    // Check if this is a flag with a separate value (not using equals sign)
                    // For flags like -n test or --namespace test
                    // Format: -flag value or --flag value
                    bool isShortFlag = parts[i].StartsWith("-") && !parts[i].StartsWith("--") && parts[i].Length == 2;
                    bool isLongFlag = parts[i].StartsWith("--") && !parts[i].Contains('=');

                    // Skip the next part if it's a value for this flag
                    if ((isShortFlag || isLongFlag) && i + 1 < parts.Length && !parts[i + 1].StartsWith("-"))
                    {
                        i++; // Skip the value of the flag
                    }
                    continue;
                }

                // Found the subcommand
                _logger?.LogDebug("Parsed kubectl subcommand: {Subcommand} from command: {Command}", parts[i], command);
                return parts[i].ToLowerInvariant();
            }

            // No subcommand found
            _logger?.LogWarning("No subcommand found in kubectl command: {Command}", command);
            return null;
        }
    }
}
