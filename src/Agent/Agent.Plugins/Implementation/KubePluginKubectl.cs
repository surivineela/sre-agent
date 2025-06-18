// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Graph.Crawler.ARM;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins
{
    public partial class KubePlugin : IKubePlugin
    {
        // Add thread repository and ThreadId property for command execution tracking
        private readonly IThreadRepository? _threadRepository;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

        public Guid? ThreadId { get; set; }

        [GeneratedRegex(@"^([a-z0-9.-]+)\/([a-z0-9.-]+) (created|configured|patched|edited|scaled|deleted)$", RegexOptions.IgnoreCase)]
        private static partial Regex KubectlOutputRegex();

        public async Task<string> RunKubectlCommandHelpAsync(
            string resourceId,
            string command)
        {
            try
            {
                return await ExecuteKubectlCommandSafely(resourceId, $"{command} --help", "");
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
            try
            {
                // Validate command format
                var validationSummary = ValidateKubectlReadCommand(command.Trim());
                if (validationSummary != null)
                {
                    return validationSummary; // Return the validation error message
                }

                if (ThreadId == null || _threadRepository == null)
                {
                    return "Error: ThreadId is not set or ThreadRepository is not available. Please set the ThreadId before running commands.";
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = new LowerCaseNamingPolicy(),
                    DictionaryKeyPolicy = new LowerCaseNamingPolicy(),
                    WriteIndented = true
                };

                var executionId = Guid.NewGuid();

                // Create execution record in Running state
                var execution = new KubectlExecution(
                    Id: executionId,
                    Command: command,
                    Stdin: "",
                    Description: GetCommandDescription(command),
                    Status: KubectlExecutionStatus.Running,
                    ClusterResourceId: resourceId,
                    OriginalFunctionCall: null, // temporary, will be set later
                    Output: null,
                    Error: null,
                    CreatedTimestamp: DateTime.UtcNow,
                    StartedTimestamp: DateTime.UtcNow,
                    CompletedTimestamp: null,
                    ExecutedBy: null,
                    AgentContextId: null
                );

                await _threadRepository.CreateKubectlExecutionAsync(ThreadId.Value, execution);

                // Create a new message with the execution
                var message = new Message(
                    Id: Guid.NewGuid(),
                    TimeStamp: DateTime.UtcNow,
                    Author: new Author(
                        DisplayName: "SRE Agent",
                        UserId: "agent-default",
                        Role: Role.SREAgent
                    ),
                    Text: "",
                    IsImageContent: false,
                    Posted: new Posted(false),
                    Approval: null,
                    AzCliExecution: null,
                    KubectlExecution: execution,
                    IncidentDiscussionId: null,
                    IsDailyReport: false
                );

                await _threadRepository.AddMessageAsync(ThreadId.Value, message);

                await _agentOutboundCommunicationService.AppendAgentStreamMessage(ThreadId.Value, JsonSerializer.Serialize(execution, options), StreamMessageType.Kubectl);

                try
                {
                    // Execute the actual command
                    var output = await ExecuteKubectlCommandSafely(resourceId, command, null);

                    // Update execution with success
                    execution = execution with
                    {
                        Status = KubectlExecutionStatus.Completed,
                        ExecutedBy = new Author(
                            DisplayName: "SRE Agent",
                            UserId: "agent-default",
                            Role: Role.User
                        ),
                        Output = output,
                        CompletedTimestamp = DateTime.UtcNow
                    };

                    await _threadRepository.UpdateKubectlExecutionAsync(ThreadId.Value, execution);

                    await _agentOutboundCommunicationService.AppendAgentStreamMessage(ThreadId.Value, JsonSerializer.Serialize(execution, options), StreamMessageType.Kubectl);

                    // Return the actual output
                    return $"Kubectl command completed successfully. Output:\n{output}";
                }
                catch (Exception ex)
                {
                    _logger?.LogInternalError(ex, "Failed to execute read command: {Command}", command);

                    // Update execution with failure
                    execution = execution with
                    {
                        Status = KubectlExecutionStatus.Failed,
                        ExecutedBy = new Author(
                            DisplayName: "SRE Agent",
                            UserId: "SRE Agent",
                            Role: Role.User
                        ),
                        Error = ex.Message,
                        CompletedTimestamp = DateTime.UtcNow
                    };

                    await _threadRepository.UpdateKubectlExecutionAsync(ThreadId.Value, execution);

                    await _agentOutboundCommunicationService.AppendAgentStreamMessage(ThreadId.Value, JsonSerializer.Serialize(execution, options), StreamMessageType.Kubectl);

                    return $"Failed to execute command: {ex.Message}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Failed to create execution for read command: {Command}", command);
                return $"Failed to prepare command execution: {ex.Message}";
            }
        }

        public async Task<string> RunKubectlWriteCommandAsync(
            string resourceId,
            string command,
            string stdin = "")
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = new LowerCaseNamingPolicy(),
                    DictionaryKeyPolicy = new LowerCaseNamingPolicy(),
                    WriteIndented = true
                };

                // Validate command format
                var validationSummary = ValidateKubectlWriteCommand(command.Trim());
                if (validationSummary != null)
                {
                    return validationSummary; // Return the validation error message
                }

                if (ThreadId == null || _threadRepository == null)
                {
                    return "Error: ThreadId is not set or ThreadRepository is not available. Please set the ThreadId before running commands.";
                }

                var executionId = Guid.NewGuid();

                // Create execution record in Pending state for approval
                var execution = new KubectlExecution(
                    Id: executionId,
                    Command: command,
                    Stdin: stdin,
                    Description: GetCommandDescription(command),
                    Status: KubectlExecutionStatus.Pending,
                    ClusterResourceId: resourceId,
                    OriginalFunctionCall: null, // temporary, will be set later
                    Output: null,
                    Error: null,
                    CreatedTimestamp: DateTime.UtcNow,
                    StartedTimestamp: null,
                    CompletedTimestamp: null,
                    ExecutedBy: null,
                    AgentContextId: null
                );

                await _threadRepository.CreateKubectlExecutionAsync(ThreadId.Value, execution);

                // Create a new message with the execution
                var message = new Message(
                    Id: Guid.NewGuid(),
                    TimeStamp: DateTime.UtcNow,
                    Author: new Author(
                        DisplayName: "SRE Agent",
                        UserId: "agent-default",
                        Role: Role.SREAgent
                    ),
                    Text: "",
                    IsImageContent: false,
                    Posted: new Posted(false),
                    Approval: null,
                    AzCliExecution: null,
                    KubectlExecution: execution,
                    IncidentDiscussionId: null,
                    IsDailyReport: false
                );

                await _threadRepository.AddMessageAsync(ThreadId.Value, message);

                await _agentOutboundCommunicationService.AppendAgentStreamMessage(ThreadId.Value, JsonSerializer.Serialize(execution, options), StreamMessageType.Kubectl);


                return "Kubectl write command has been prepared for approval. Please click 'Run' to execute or 'Cancel' to dismiss.";
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Failed to create execution for write command: {Command}", command);
                return $"Failed to prepare command execution: {ex.Message}";
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

            // For apply commands, ensure we have a -f flag or YAML content
            // please note 'create' don't need "-f", e.g. `kubectl create deployment my-deployment --image=my-image`
            if (subcommand == "apply")
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

        public async Task<string> ExecuteKubectlCommandSafely(
            string resourceId,
            string command,
            string stdin = "",
            string oboToken = "")
        {
            // get the cluster kubeconfig
            // todo: change to create a `view` clusterrolebinding with a service account, and use that guy's config
            var kubeConfig = await _kubernetesClientFactory.GetOrAddCachedK8sConfiguration(resourceId);
            if (kubeConfig is null)
            {
                return $"[Unexpected Error]: Unable to retrieve kubeconfig for cluster.";
            }

            if (!string.IsNullOrEmpty(oboToken))
            {
                var user = kubeConfig.Configuration.Users.FirstOrDefault();
                if (user != null)
                {
                    bool useAAD = false;
                    foreach (var ext in user.UserCredentials.Extensions)
                    {
                        if (ext.Name.Equals("UseAADAuth", StringComparison.OrdinalIgnoreCase) && bool.TryParse(ext.Extension.ToString(), out useAAD) && useAAD)
                        {
                            _logger?.LogInternalInformation("Using OBO token for kubectl command execution.");
                            user.UserCredentials.Token = oboToken;
                        }
                    }
                }
            }

            // write to temp file
            var kubeConfigPath = Path.GetTempFileName();
            await File.WriteAllTextAsync(kubeConfigPath, _configJsonSerializer.Serialize(kubeConfig.Configuration));

            // Remove "kubectl" prefix more safely
            var cmd = command.Trim();
            if (cmd.StartsWith("kubectl ", StringComparison.OrdinalIgnoreCase))
            {
                cmd = cmd.Substring(8); // Remove "kubectl " (7 chars + 1 space)
            }

            try
            {
                var output = await ExecuteCommandHelper.ExecuteCommand(
                    "kubectl",
                    stdin,
                    [
                        cmd,
                        $"--kubeconfig=\"{kubeConfigPath}\"",
                        $"--cache-dir=\"{Path.Combine(Path.GetTempPath(), ".kube")}\""
                    ]);

                // trigger recrawl for modified resources
                TriggerRecrawl(resourceId, command, output);

                return output;
            }
            catch (Exception ex) when (ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = "Failed to run kubectl command. Error from AKS API Server: Forbidden.\n" +
                    $"Please ensure the following permissions are provided on the AKS cluster scope to {_agentKubeCtlIdentity}:\n" +
                    "For reader mode: Azure Kubernetes Service Cluster User Role and Azure Kubernetes Service RBAC Reader.\n" +
                    "For agent mode: Azure Kubernetes Service Cluster Admin Role and Azure Kubernetes Service RBAC Cluster Admin.";

                throw new Exception(errorMessage);
            }
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

        /// <summary>
        /// Generates a human-readable description for a kubectl command.
        /// </summary>
        private string GetCommandDescription(string command)
        {
            var subcommand = ParseKubectlSubcommand(command);
            if (string.IsNullOrEmpty(subcommand))
            {
                return "Execute kubectl command";
            }

            // Extract the resource type and name if present
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string resourceType = string.Empty;
            string resourceName = string.Empty;

            // Find resource type and name after the subcommand
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals(subcommand, StringComparison.OrdinalIgnoreCase) && i + 2 < parts.Length)
                {
                    // Check if next parts are resource type and name (not flags)
                    if (!parts[i + 1].StartsWith("-"))
                    {
                        resourceType = parts[i + 1];

                        // Check if the next part is a name and not a flag
                        if (!parts[i + 2].StartsWith("-"))
                        {
                            resourceName = parts[i + 2];
                        }
                        break;
                    }
                }
            }

            switch (subcommand.ToLowerInvariant())
            {
                case "apply":
                    return $"Apply Kubernetes configuration";
                case "create":
                    return $"Create Kubernetes resource";
                case "patch":
                    return !string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceName)
                        ? $"Patch {resourceType} '{resourceName}'"
                        : $"Patch Kubernetes resource";
                case "replace":
                    return !string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceName)
                        ? $"Replace {resourceType} '{resourceName}'"
                        : $"Replace Kubernetes resource";
                case "scale":
                    return !string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceName)
                        ? $"Scale {resourceType} '{resourceName}'"
                        : $"Scale Kubernetes resource";
                case "label":
                    return !string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceName)
                        ? $"Add/update labels for {resourceType} '{resourceName}'"
                        : $"Add/update Kubernetes resource labels";
                case "annotate":
                    return !string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceName)
                        ? $"Add/update annotations for {resourceType} '{resourceName}'"
                        : $"Add/update Kubernetes resource annotations";
                case "rollout":
                    var rolloutAction = ParseRolloutAction(command);
                    return !string.IsNullOrEmpty(resourceType) && !string.IsNullOrEmpty(resourceName)
                        ? $"Rollout {rolloutAction} for {resourceType} '{resourceName}'"
                        : $"Kubernetes rollout {rolloutAction}";
                default:
                    return $"Execute kubectl {subcommand} command";
            }
        }

        private void TriggerRecrawl(string clusterResourceId, string command, string output)
        {
            var lines = output.Trim().Split(["\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries);

            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string? namespaceName = null;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Equals("-n", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("--namespace", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < parts.Length)
                    {
                        namespaceName = parts[i + 1];
                        break;
                    }
                }
                else if (part.StartsWith("-n=") ||
                        part.StartsWith("--namespace="))
                {
                    namespaceName = part.Split('=')[1];
                    break;
                }
            }

            foreach (var line in lines)
            {
                var match = KubectlOutputRegex().Match(line);
                if (match.Success)
                {
                    // Extract resource type and name
                    var groupKind = match.Groups[1].Value;
                    var name = match.Groups[2].Value;
                    var action = match.Groups[3].Value.ToLowerInvariant();

                    int index = groupKind.IndexOf('.');
                    var group = Constants.KubernetesCoreGroup;
                    var kind = groupKind;
                    if (index > 0)
                    {
                        kind = GetPluralFormForKind(groupKind.Substring(0, index));
                        group = groupKind.Substring(index + 1);
                    }
                    bool isDelete = action == "deleted";

                    _crawlerTriggerService.TriggerKubernetesCrawl(clusterResourceId, namespaceName, name, group, string.Empty, kind, isDelete);
                    _logger?.LogInternalInformation(
                        $"Triggered recrawl for Kubernetes resource: {group}/{kind} '{name}' in namespace '{namespaceName ?? ""}' (action: {action})");
                }
            }
        }
    }
}
