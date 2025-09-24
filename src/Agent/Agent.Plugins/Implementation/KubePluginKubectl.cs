// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using KubectlCliExecution = Agent.Core.Services.KubectlExecution;

namespace Agent.Plugins
{
    public partial class KubePlugin : IKubePlugin
    {
        public Guid? ThreadId { get; set; }

        [GeneratedRegex(@"^([a-z0-9.-]+)\/([a-z0-9.-]+) (created|configured|patched|edited|scaled|deleted)$", RegexOptions.IgnoreCase)]
        private static partial Regex KubectlOutputRegex();

        public async Task<string> RunKubectlCommandHelpAsync(
            string resourceId,
            string command)
        {
            try
            {
                var result = await ExecuteKubectlCommandSafely(resourceId, $"{command} --help", "");
                return result.Output;
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

                return await ExecuteKubectlWithApprovalFallback(
                    resourceId,
                    command,
                    stdin: "",
                    writeCommand: false);
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
                // Early validation
                var validationResult = ValidateWriteCommandRequest(command);
                if (validationResult != null)
                {
                    return validationResult;
                }

                var executionId = Guid.NewGuid();

                // Get the agent context and determine the real agent mode at thread level
                bool isAutonomousMode;
                var threadAgentMode = _actionSettings.Mode.ToString();
                if (ThreadId.HasValue && _threadRepository != null)
                {
                    var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(ThreadId.Value);
                    var agentContext = agentContexts?.FirstOrDefault();
                    if (agentContext != null)
                    {
                        // Use agent runtime modifier to get the real agent mode for the thread
                        threadAgentMode = _agentRuntimeModifier.GetThreadAgentMode(agentContext);
                    }

                }

                isAutonomousMode = string.Equals(threadAgentMode, ActionMode.Autonomous.ToString(), StringComparison.OrdinalIgnoreCase);
                if (isAutonomousMode)
                {
                    return await ExecuteKubectlWithApprovalFallback(resourceId, command, stdin, writeCommand: true);
                }
                else
                {
                    await CreateAndPersistKubectlExecution(executionId, resourceId, command, stdin, requiresApproval: true);
                    return "Kubectl write command has been prepared for approval. Please click 'Authorize' to execute or 'Cancel' to dismiss.";

                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Failed to create execution for write command: {Command}", command);
                return $"Failed to prepare command execution: {ex.Message}";
            }
        }

        private string? ValidateWriteCommandRequest(string command)
        {
            var validationSummary = ValidateKubectlWriteCommand(command.Trim());
            if (validationSummary != null)
            {
                return validationSummary;
            }

            if (ThreadId == null || _threadRepository == null)
            {
                return "Error: ThreadId is not set or ThreadRepository is not available. Please set the ThreadId before running commands.";
            }

            return null;
        }

        private async Task<KubectlExecution> CreateAndPersistKubectlExecution(
            Guid executionId,
            string resourceId,
            string command,
            string stdin,
            bool requiresApproval)
        {
            var execution = CreateKubectlExecution(executionId, resourceId, command, stdin, requiresApproval);
            await _threadRepository!.CreateKubectlExecutionAsync(ThreadId!.Value, execution);

            var message = CreateExecutionMessage(execution);
            await _threadRepository.AddMessageAsync(ThreadId.Value, message);

            await NotifyExecutionCreated(execution, message.Id);

            return execution;
        }

        private KubectlExecution CreateKubectlExecution(
            Guid executionId,
            string resourceId,
            string command,
            string stdin,
            bool requiresApproval)
        {
            return new KubectlExecution(
                Id: executionId,
                Command: command,
                Stdin: stdin,
                Description: GetCommandDescription(command),
                Status: requiresApproval ? KubectlExecutionStatus.Pending : KubectlExecutionStatus.Running,
                ClusterResourceId: resourceId,
                OriginalFunctionCall: null,
                Output: null,
                Error: null,
                CreatedTimestamp: DateTime.UtcNow,
                StartedTimestamp: requiresApproval ? null : DateTime.UtcNow,
                CompletedTimestamp: null,
                ExecutedBy: null,
                AgentContextId: null
            );
        }

        private static Message CreateExecutionMessage(KubectlExecution execution)
        {
            return new Message(
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
        }

        private async Task<string> ExecuteKubectlWithApprovalFallback(
            string resourceId,
            string command,
            string stdin,
            bool writeCommand)
        {
            var executionId = Guid.NewGuid();
            var execution = await CreateAndPersistKubectlExecution(executionId, resourceId, command, stdin, requiresApproval: false);

            string cmdType = writeCommand ? "write" : "read";

            try
            {
                var result = await ExecuteKubectlCommandSafely(
                    resourceId,
                    command,
                    stdin);

                if (result.ErrorOccurred)
                {
                    if (result.ErrorType == CliErrorType.AuthorizationError)
                    {
                        await UpdateExecutionWithOboFlow(execution);
                        return $"Kubectl {cmdType} command has been prepared for approval. Please click 'Authorize' to execute or 'Cancel' to dismiss.";
                    }
                    else
                    {
                        await UpdateExecutionWithFailure(execution, result.Output);
                        return $"Kubectl {cmdType} command failed. Output:\n{result.Output}";
                    }
                }
                else
                {
                    await UpdateExecutionWithSuccess(execution, result.Output);
                    return $"Kubectl {cmdType} command completed successfully. Output:\n{result.Output}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, $"Failed to execute {cmdType} command: {command}");
                await UpdateExecutionWithFailure(execution, ex.Message);
                throw;
            }
        }

        private async Task UpdateExecutionWithSuccess(KubectlExecution execution, string output)
        {
            var updatedExecution = execution with
            {
                Status = KubectlExecutionStatus.Completed,
                ExecutedBy = CreateAgentAuthor(),
                Output = output,
                CompletedTimestamp = DateTime.UtcNow
            };

            await _threadRepository!.UpdateKubectlExecutionAsync(ThreadId!.Value, updatedExecution);
            await NotifyExecutionUpdated(updatedExecution);
        }

        private async Task UpdateExecutionWithFailure(KubectlExecution execution, string errorMessage)
        {
            var updatedExecution = execution with
            {
                Status = KubectlExecutionStatus.Failed,
                ExecutedBy = CreateAgentAuthor(),
                Error = errorMessage,
                CompletedTimestamp = DateTime.UtcNow
            };

            await _threadRepository!.UpdateKubectlExecutionAsync(ThreadId!.Value, updatedExecution);
            await NotifyExecutionUpdated(updatedExecution);
        }

        private async Task UpdateExecutionWithOboFlow(KubectlExecution execution)
        {
            var updatedExecution = execution with
            {
                Status = KubectlExecutionStatus.PendingAuthorization,
                Description = $"{execution.Description}",
                Output = null,
                ExecutedBy = null,
                Error = null,
                StartedTimestamp = null,
                CompletedTimestamp = null,
            };

            await _threadRepository!.UpdateKubectlExecutionAsync(ThreadId!.Value, updatedExecution);
            await NotifyExecutionUpdated(updatedExecution);
        }

        private static Author CreateAgentAuthor()
        {
            return new Author(
                DisplayName: "SRE Agent",
                UserId: "agent-default",
                Role: Role.User
            );
        }
        private async Task NotifyExecutionCreated(KubectlExecution execution, Guid messageId)
        {
            var options = GetJsonSerializerOptions();
            await _agentOutboundCommunicationService.AppendAgentStreamMessage(
                ThreadId!.Value,
                JsonSerializer.Serialize(execution, options),
                StreamMessageType.Kubectl,
                messageId);
        }

        private async Task NotifyExecutionUpdated(KubectlExecution execution)
        {
            var options = GetJsonSerializerOptions();
            await _agentOutboundCommunicationService.AppendAgentStreamMessage(
                ThreadId!.Value,
                JsonSerializer.Serialize(execution, options),
                StreamMessageType.Kubectl);
        }

        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = new LowerCaseNamingPolicy(),
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
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
                "\\",       // Escape character (except in valid paths)
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
                    // Check for both -o and --output flags
                    var oMatches = Regex.Matches(command, @"(^|\s)(-o|--output)\s+[^\s]+", RegexOptions.IgnoreCase);
                    if (oMatches.Count == 0)
                        return "[Validation Failed]: Command must include the '-o' or '--output' option";
                    if (oMatches.Count > 1)
                        return "[Validation Failed]: Command must contain only one output option";

                    var fmt = Regex.Match(command, @"(^|\s)(-o|--output)\s+(?<fmt>[^\s]+)", RegexOptions.IgnoreCase)
                        .Groups["fmt"].Value;

                    bool allowed =
                        fmt.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                        fmt.Equals("wide", StringComparison.OrdinalIgnoreCase) ||
                        fmt.Equals("yaml", StringComparison.OrdinalIgnoreCase) ||
                        fmt.Equals("json", StringComparison.OrdinalIgnoreCase) ||
                        fmt.StartsWith("custom-columns", StringComparison.OrdinalIgnoreCase) ||
                        fmt.StartsWith("jsonpath", StringComparison.OrdinalIgnoreCase);

                    if (!allowed)
                        return $"[Validation Failed]: Unsupported output value '{fmt}'. Allowed: name, wide, yaml, json, jsonpath, custom-columns[=...]";
                    break;
                case "describe":
                case "logs":
                case "exec":
                case "top":
                case "api-resources":
                case "api-versions":
                case "explain":
                case "version":
                case "cluster-info":
                    // These are valid subcommands for read operations
                    break;
                case "config":
                    // Handle read-only config operations
                    var configArgs = command.ToLowerInvariant();
                    if (configArgs.Contains("view") || configArgs.Contains("get-contexts") || configArgs.Contains("get-clusters") || configArgs.Contains("current-context"))
                    {
                        // These are read-only config operations
                        break;
                    }
                    else
                    {
                        return "[Validation Failed]: 'config' subcommand only supports read operations: view, get-contexts, get-clusters, current-context. Use RunKubectlWriteCommandAsync for write operations.";
                    }
                case "rollout":
                    // Handle read-only rollout operations
                    var rolloutAction = ParseRolloutAction(command);
                    var readOnlyRolloutActions = new[] { "status", "history" };

                    if (rolloutAction == null)
                    {
                        return "[Validation Failed]: Rollout command missing action. Supported read actions: status, history";
                    }

                    if (!readOnlyRolloutActions.Contains(rolloutAction))
                    {
                        return $"[Validation Failed]: '{rolloutAction}' is a write operation. Use RunKubectlWriteCommandAsync instead. Supported read actions: {string.Join(", ", readOnlyRolloutActions)}";
                    }
                    break;
                default:
                    return $"[Validation Failed]: Unsupported subcommand '{subcommand}'. Supported: get, describe, logs, exec, top, api-resources, api-versions, explain, version, cluster-info, config, rollout";
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
                case "config":
                    // All config operations should go through write validation to ensure proper handling
                    return "[Validation Failed]: 'config' operations should use RunKubectlReadCommandAsync for read operations or RunKubectlWriteCommandAsync for write operations.";
                case "rollout":
                    // Handle rollout operations - check if it's a read-only action
                    var rolloutAction = ParseRolloutAction(command);
                    var readOnlyRolloutActions = new[] { "status", "history" };

                    if (rolloutAction != null && readOnlyRolloutActions.Contains(rolloutAction))
                    {
                        return $"[Validation Failed]: 'rollout {rolloutAction}' is a read-only command. Use RunKubectlReadCommandAsync instead.";
                    }
                    break;
                default:
                    return $"[Validation Failed]: Unsupported subcommand '{subcommand}'. Supported write commands: apply, create, delete, patch, replace, scale, label, annotate, set, rollout, config";
            }

            // Check for delete commands and return error with the command for manual execution
            if (subcommand == "delete")
            {
                return $"Error: Delete operations are not allowed for safety reasons. Please manually execute this command: {command}";
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

            // Ensure rollout commands are restrictive - only allow write operations
            if (subcommand == "rollout")
            {
                var validWriteRolloutCommands = new[] { "restart", "undo", "pause", "resume" };
                var rolloutAction = ParseRolloutAction(command);

                if (rolloutAction == null || !validWriteRolloutCommands.Contains(rolloutAction))
                {
                    return $"[Validation Failed]: Unsupported write rollout action. Supported write actions: {string.Join(", ", validWriteRolloutCommands)}";
                }
            }

            return null; // No validation errors
        }

        private string? ParseRolloutAction(string command)
        {
            var match = Regex.Match(command, @"kubectl\s+rollout\s+(?<action>\w+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["action"].Value.ToLowerInvariant() : null;
        }

        public async Task<CliExecutionResult> ExecuteKubectlCommandSafely(
            string resourceId,
            string command,
            string stdin = "",
            TimeSpan? timeoutMin = null)
        {
            // get the cluster kubeconfig
            // todo: change to create a `view` clusterrolebinding with a service account, and use that guy's config
            CachedK8sConfiguration? kubeConfig;
            try
            {
                kubeConfig = await _kubernetesClientFactory.GetOrAddCachedK8sConfiguration(resourceId);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 403)
            {
                return new CliExecutionResult
                {
                    // do not trigger obo flow because the obo token audience is AKS, cannot work with ARM to get kubeconfig
                    ErrorType = CliErrorType.Other,
                    Output = await GetPermissionErrorMessageAsync(resourceId),
                };
            }

            if (kubeConfig is null)
            {
                return new CliExecutionResult
                {
                    ErrorType = CliErrorType.Other,
                    Output = "[Unexpected Error]: Unable to retrieve kubeconfig for cluster."
                };
            }

            var serializedKubeConfig = _configJsonSerializer.Serialize(kubeConfig.Configuration);

            try
            {
                var cliExecution = new KubectlCliExecution(
                    _logger!,
                    serializedKubeConfig,
                    command,
                    stdin);
                var output = await cliExecution.ExecuteAsync(
                    timeout: timeoutMin
                );

                if (MaybeWriteCommand(command))
                {
                    // trigger recrawl for modified resources
                    TriggerRecrawl(resourceId, command, output);
                }

                // assumes cli exit code would be non-zero if unauthorized
                // we do not parse the output when success because the the normal cli output may also contain error info which will be a false positive
                return new CliExecutionResult
                {
                    Output = output,
                    ErrorType = CliErrorType.None
                };
            }
            catch (Exception ex)
            {
                var executionResult = await CliExecutionHelper.ParseCliExecutionResult(_chatClient, ex.Message);
                return executionResult;
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
            }

            // Find the subcommand, skipping flags and their values
            for (int i = 1; i < parts.Length; i++)
            {
                // Skip flags that start with -
                if (parts[i].StartsWith("-"))
                {
                    // Handle different flag formats:
                    // -f file, --flag value, --flag=value, -n=namespace
                    if (parts[i].Contains('='))
                    {
                        // Flag with embedded value (--flag=value or -f=file)
                        continue;
                    }
                    else
                    {
                        // Check if this is a flag with a separate value
                        bool isShortFlag = parts[i].StartsWith("-") && !parts[i].StartsWith("--") && parts[i].Length == 2;
                        bool isLongFlag = parts[i].StartsWith("--") && !parts[i].Contains('=');

                        // Skip the next part if it's a value for this flag and doesn't start with -
                        if ((isShortFlag || isLongFlag) && i + 1 < parts.Length && !parts[i + 1].StartsWith("-"))
                        {
                            i++; // Skip the value of the flag
                        }
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

        private bool MaybeWriteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            var subcommand = ParseKubectlSubcommand(command);
            if (string.IsNullOrEmpty(subcommand))
            {
                return false;
            }

            // Define write operations that modify cluster state
            var writeCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "apply",
                "create",
                "delete",
                "patch",
                "replace",
                "scale",
                "label",
                "annotate",
                "set",
                "rollout",
                "edit",
                "drain",
                "cordon",
                "uncordon",
                "taint"
            };

            return writeCommands.Contains(subcommand);
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
