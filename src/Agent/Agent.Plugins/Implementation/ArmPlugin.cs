// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class ArmPlugin : IArmPlugin
    {
        private readonly ILogger<ArmPlugin> _logger;
        private readonly ArmHelper _armHelper;
        private readonly IThreadRepository _threadRepository;
        private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
        private readonly ActionSettings _actionSettings;
        private readonly IAgentRuntimeModifier<AgentContext> _agentRuntimeModifier;

        public Guid? ThreadId { get; set; }

        private static readonly ImmutableArray<string> _allowedReadVerbs = [
            "get",
            "list",
            "show",
        ];

        private static readonly string _allowedReadVerbString = string.Join(", ", _allowedReadVerbs);

        private static readonly ImmutableArray<string> _allowedWriteVerbs = [
            "add",
            "create",
            "query",
            "register", // for RPs and Features
            "restart",
            "scale",
            "set",
            "start",
            "stop",
            "update",
            "upgrade",
        ];

        private static readonly string _allowedWriteVerbString = string.Join(", ", _allowedWriteVerbs);

        private static readonly ImmutableArray<string> _blockedDeleteVerbs = [
            "delete",
            "remove",
        ];

        private static readonly ImmutableArray<string> _writeVerbs = [.. _allowedWriteVerbs, .. _blockedDeleteVerbs];

        public ArmPlugin(ILogger<ArmPlugin> logger, ArmHelper armHelper, IThreadRepository threadRepository, IAgentOutboundCommunicationService outboundCommunicationService, ActionSettings actionSettings, IAgentRuntimeModifier<AgentContext> agentRuntimeModifier)
        {
            _logger = logger;
            _armHelper = armHelper;
            _threadRepository = threadRepository;
            _outboundCommunicationService = outboundCommunicationService;
            _actionSettings = actionSettings;
            _agentRuntimeModifier = agentRuntimeModifier;
        }

        public async Task<string> SetMinimumTlsVersion(
            string appResourceId,
            string minimumTlsVersion)
        {
            var status = (await _armHelper.GetTlsSettings([appResourceId])).SingleOrDefault();
            bool success = false;
            string reason = string.Empty;

            if (status != null)
            {
                // Assume both versions are in the same format (e.g. "1.2" or "1.3").
                if (string.Equals(status.MinimumTlsVersion, minimumTlsVersion, StringComparison.InvariantCultureIgnoreCase))
                {
                    var msg = $"Resource {appResourceId} already has minimum TLS version set to {status.MinimumTlsVersion}. No action needed.";
                    _logger?.LogInternalInformation(msg);
                    return msg;
                }

                var response = await _armHelper.UpdateMinimumTlsVersion(status, minimumTlsVersion);
                success = response.Item1;
                reason = response.Item2;
            }
            else
            {
                success = false;
                reason = $"Resource {appResourceId} not found.";
            }

            var message = success switch
            {
                true => $"Resource {appResourceId} updated with minimum TLS version set to {minimumTlsVersion} at {DateTime.UtcNow:o}",
                false => $"Failed to update resource {appResourceId} at {DateTime.UtcNow:o}. Reason: {reason}",
            };


            _logger?.LogInternalInformation(message);
            return message;
        }
        public async Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds)
        {
            return await _armHelper.GetTlsSettings(resourceIds);
        }


        public async Task<bool> RestartWebApp(string appResourceId)
        {
            var response = await _armHelper.RestartWebAppAsync(appResourceId);

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CheckIfResourceExists(string appResourceId)
        {
            return await _armHelper.CheckIfResourceExistsAsync(appResourceId);
        }

        public async Task<bool> RestartContainerApp(string appResourceId, string revisionName)
        {
            return await _armHelper.RestartContainerAppAsync(appResourceId, revisionName);
        }

        public async Task<string> GetArmResourceAsJson(string resourceId)
        {
            return await _armHelper.GetArmResourceAsJsonAsync(resourceId);
        }

        public async Task<RemediationResult> PowerOnVirtualMachine(string resourceId)
        {
            bool vmPowerOnResult = true;
            string message = "Virtual machine powered on successfully";
            try
            {
                vmPowerOnResult = await _armHelper.PowerOnVirtualMachineAsync(resourceId);
                if (!vmPowerOnResult)
                {
                    message = "Failed to power on the virtual machine";
                }
            }
            catch (Exception ex)
            {
                vmPowerOnResult = false;
                message = $"Error powering on the virtual machine: {ex.Message}";
            }

            return new RemediationResult(
                    Success: vmPowerOnResult,
                    Action: "Power On Azure Virtual Machine",
                    Details: message,
                    OperationId: null,
                    FinishedTime: DateTime.Now);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetVirtualMachineBootDiagnostics(string resourceId)
        {
            var bootDiagnosticLogs = await _armHelper.GetVirtualMachineBootDiagnosticsAsync(resourceId);
            return bootDiagnosticLogs;
        }

        public async Task<string> CheckConnectivityToAzureWebJobsStorage(string resourceId, string providerType = "BlobStorage")
        {
            return await _armHelper.CheckConnectivityToAzureWebJobsStorage(resourceId, providerType);
        }

        public async Task<string> CheckTcpConnectivity(string resourceId, string host, int port)
        {
            return await _armHelper.CheckTcpConnectivityAsync(resourceId, host, port);
        }

        public async Task<string> CheckDnsResolution(string resourceId, string destinationUrl)
        {
            return await _armHelper.CheckDnsResolution(resourceId, destinationUrl);
        }

        public async Task<IDictionary<string, string>> GetAppSetting(string resourceId, string appSettingKey)
        {
            return await _armHelper.GetAppSetting(resourceId, appSettingKey);
        }

        public async Task<bool> ListKeysAndUpdateAppSettingsAsync(string storageResourceId, string appServiceResourceId, string appSettingKey)
        {
            return await _armHelper.ListKeysAndUpdateAppSettingsAsync(storageResourceId, appServiceResourceId, appSettingKey);
        }

        public async Task<bool> ConfigureAppSettingsForManagedIdentityStorage(string resourceId, string storageAccountName)
        {
            return await _armHelper.ConfigureAppSettingsForManagedIdentityStorage(resourceId, storageAccountName);
        }

        public async Task<bool> UpdateAppSettingsAsync(string resourceId, IDictionary<string, string> appSettings)
        {
            return await _armHelper.UpdateAppSettingsAsync(resourceId, appSettings);
        }

        public async Task<string> RunAzCliReadCommandsAsync(string command)
        {
            try
            {
                if (!IsReadOnlyCommand(command))
                {
                    return $"Error: This method only supports read operations ({_allowedReadVerbString}). Use RunAzCliWriteCommandsAsync for write operations.";
                }

                if (ThreadId == null)
                {
                    return "Error: ThreadId is not set. Please set the ThreadId before running commands.";
                }

                var executionId = Guid.NewGuid();

                // Create execution record in Pending state
                var execution = new AzCliExecution(
                    Id: executionId,
                    Command: command,
                    Description: GetCommandDescription(command),
                    Status: AzCliExecutionStatus.Running,
                    OriginalFunctionCall: null, // temporary, will be set later
                    Output: null,
                    Error: null,
                    CreatedTimestamp: DateTime.UtcNow,
                    StartedTimestamp: DateTime.UtcNow,
                    CompletedTimestamp: null,
                    ExecutedBy: null,
                    AgentContextId: null
                );

                await _threadRepository.CreateAzCliExecutionAsync(ThreadId.Value, execution);

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
                    AzCliExecution: execution,
                    IncidentDiscussionId: null,
                    IsDailyReport: false
                );

                await _threadRepository.AddMessageAsync(ThreadId.Value, message);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DictionaryKeyPolicy = new LowerCaseNamingPolicy(),
                    WriteIndented = true
                };
                options.Converters.Add(new JsonStringEnumConverter());

                // Stream the whole az cli execution to render the special AzCliExecution component
                await _outboundCommunicationService.AppendAgentStreamMessage(ThreadId.Value, JsonSerializer.Serialize(execution, options), StreamMessageType.AzCli);
                try
                {
                    // Execute the actual command synchronously - crawler triggering happens inside ArmHelper
                    var output = await _armHelper.RunAzCliCommandsAsync(command);

                    // Update execution with success
                    execution = execution with
                    {
                        Status = AzCliExecutionStatus.Completed,
                        ExecutedBy = new Author(
                            DisplayName: "SRE Agent",
                            UserId: "agent-default",
                            Role: Role.User
                        ),
                        Output = output,
                        CompletedTimestamp = DateTime.UtcNow
                    };

                    await _threadRepository.UpdateAzCliExecutionAsync(ThreadId.Value, execution);

                    await _outboundCommunicationService.AppendAgentStreamMessage(ThreadId.Value, JsonSerializer.Serialize(execution, options), StreamMessageType.AzCli);

                    // Return the actual output
                    return $"Azure CLI command completed successfully. Output: {output}";
                }
                catch (Exception ex)
                {
                    _logger?.LogInternalError(ex, "Failed to execute read command: {Command}", command);

                    // Update execution with failure
                    execution = execution with
                    {
                        Status = AzCliExecutionStatus.Failed,
                        ExecutedBy = new Author(
                            DisplayName: "SRE Agent",
                            UserId: "agent-default",
                            Role: Role.User
                        ),
                        Error = ex.Message,
                        CompletedTimestamp = DateTime.UtcNow
                    };

                    await _threadRepository.UpdateAzCliExecutionAsync(ThreadId.Value, execution);

                    await _outboundCommunicationService.AppendAgentStreamMessage(ThreadId.Value, JsonSerializer.Serialize(execution, options), StreamMessageType.AzCli);

                    throw; // Re-throw to let the caller handle the error
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Failed to create execution for command: {Command}", command);
                return $"Failed to prepare command execution: {ex.Message}";
            }
        }

        public async Task<string> RunAzCliWriteCommandsAsync(string command)
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

                // Create and persist execution record
                var execution = await CreateAndPersistAzCliExecution(executionId, command, isAutonomousMode);

                // Handle execution based on mode
                return isAutonomousMode
                    ? await ExecuteAutonomousAzCliCommand(command, execution)
                    : "Azure CLI write command has been prepared for approval. Please click 'Run' to execute or 'Cancel' to dismiss.";
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Failed to create execution for write command: {Command}", command);
                return $"Failed to prepare command execution: {ex.Message}";
            }
        }

        private string? ValidateWriteCommandRequest(string command)
        {
            // Validate it's a write command and not a delete
            if (!IsWriteCommand(command))
            {
                return $"Error: This method only supports write operations ({_allowedWriteVerbString}).";
            }

            if (IsDeleteCommand(command))
            {
                return "Error: Delete operations are not allowed for safety reasons. Please use the Azure Portal for deletions.";
            }

            if (ThreadId == null)
            {
                return "Error: ThreadId is not set. Please set the ThreadId before running commands.";
            }

            return null;
        }

        private async Task<AzCliExecution> CreateAndPersistAzCliExecution(
            Guid executionId,
            string command,
            bool isAutonomousMode)
        {
            var execution = CreateAzCliExecution(executionId, command, isAutonomousMode);

            await _threadRepository.CreateAzCliExecutionAsync(ThreadId!.Value, execution);

            var message = CreateAzCliExecutionMessage(execution);
            await _threadRepository.AddMessageAsync(ThreadId.Value, message);

            await NotifyAzCliExecutionCreated(execution);

            return execution;
        }

        private AzCliExecution CreateAzCliExecution(
            Guid executionId,
            string command,
            bool isAutonomousMode)
        {
            return new AzCliExecution(
                Id: executionId,
                Command: command,
                Description: GetCommandDescription(command),
                Status: isAutonomousMode ? AzCliExecutionStatus.Running : AzCliExecutionStatus.Pending,
                OriginalFunctionCall: null,
                Output: null,
                Error: null,
                CreatedTimestamp: DateTime.UtcNow,
                StartedTimestamp: isAutonomousMode ? DateTime.UtcNow : null,
                CompletedTimestamp: null,
                ExecutedBy: null,
                AgentContextId: null
            );
        }

        private static Message CreateAzCliExecutionMessage(AzCliExecution execution)
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
                AzCliExecution: execution,
                IncidentDiscussionId: null,
                IsDailyReport: false
            );
        }

        private async Task<string> ExecuteAutonomousAzCliCommand(
            string command,
            AzCliExecution execution)
        {
            try
            {
                var output = await _armHelper.RunAzCliCommandsAsync(command);

                await UpdateAzCliExecutionWithSuccess(execution, output);

                return $"Azure CLI command completed successfully. Output: {output}";
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Failed to execute write command: {Command}", command);

                await UpdateAzCliExecutionWithFailure(execution, ex.Message);

                return $"Failed to execute command: {ex.Message}";
            }
        }

        private async Task UpdateAzCliExecutionWithSuccess(AzCliExecution execution, string output)
        {
            var updatedExecution = execution with
            {
                Status = AzCliExecutionStatus.Completed,
                ExecutedBy = CreateAgentAuthor(),
                Output = output,
                CompletedTimestamp = DateTime.UtcNow
            };

            await _threadRepository.UpdateAzCliExecutionAsync(ThreadId!.Value, updatedExecution);
            await NotifyAzCliExecutionUpdated(updatedExecution);
        }

        private async Task UpdateAzCliExecutionWithFailure(AzCliExecution execution, string errorMessage)
        {
            var updatedExecution = execution with
            {
                Status = AzCliExecutionStatus.Failed,
                ExecutedBy = CreateAgentAuthor(),
                Error = errorMessage,
                CompletedTimestamp = DateTime.UtcNow
            };

            await _threadRepository.UpdateAzCliExecutionAsync(ThreadId!.Value, updatedExecution);
            await NotifyAzCliExecutionUpdated(updatedExecution);
        }

        private static Author CreateAgentAuthor()
        {
            return new Author(
                DisplayName: "SRE Agent",
                UserId: "agent-default",
                Role: Role.User
            );
        }

        private async Task NotifyAzCliExecutionCreated(AzCliExecution execution)
        {
            var options = GetJsonSerializerOptions();
            await _outboundCommunicationService.AppendAgentStreamMessage(
                ThreadId!.Value,
                JsonSerializer.Serialize(execution, options),
                StreamMessageType.AzCli);
        }

        private async Task NotifyAzCliExecutionUpdated(AzCliExecution execution)
        {
            var options = GetJsonSerializerOptions();
            await _outboundCommunicationService.AppendAgentStreamMessage(
                ThreadId!.Value,
                JsonSerializer.Serialize(execution, options),
                StreamMessageType.AzCli);
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

        public async Task<string> GetAzCliHelpAsync(string helpTopic, string grepPattern = null)
        {
            try
            {
                // Use the ArmHelper to get help information
                var helpCommand = $"az {helpTopic} --help";
                var helpOutput = await _armHelper.RunAzCliCommandsAsync(helpCommand);

                // If grep pattern is provided, filter the output
                if (!string.IsNullOrEmpty(grepPattern))
                {
                    var lines = helpOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var filteredLines = lines.Where(line =>
                        line.Contains(grepPattern, StringComparison.InvariantCultureIgnoreCase))
                        .ToList();

                    if (filteredLines.Any())
                    {
                        return string.Join('\n', filteredLines);
                    }
                    else
                    {
                        return $"No help information found for pattern '{grepPattern}' in topic '{helpTopic}'";
                    }
                }

                return helpOutput;
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Failed to get Azure CLI help for topic: {HelpTopic}", helpTopic);
                return $"Failed to retrieve help information for '{helpTopic}': {ex.Message}";
            }
        }

        public async Task<string> GetResourceIdFromStorageServiceUri(string storageServiceUri, string subscriptionId)
        {
            if (string.IsNullOrWhiteSpace(storageServiceUri))
            {
                return $"Error: Storage Service URI cannot be null or empty";
            }

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return $"Error: Subscription ID cannot be null or empty";
            }

            try
            {
                // Parse the URI to extract the storage account name
                if (!Uri.TryCreate(storageServiceUri, UriKind.Absolute, out var uri))
                {
                    return $"Error: Invalid storage service URI format: {storageServiceUri}";
                }

                // Extract storage account name from URI host
                // Example: "eventgridblobtriggerstrg.blob.core.windows.net" -> "eventgridblobtriggerstrg"
                string host = uri.Host;
                string[] hostParts = host.Split('.');

                if (hostParts.Length < 4 || !host.Contains(".blob.core.windows.net"))
                {
                    return $"Error: URI does not appear to be a valid Azure Blob Storage URI: {storageServiceUri}";
                }

                string storageAccountName = hostParts[0];

                // Search only in the specified subscription
                var resourceIds = await _armHelper.GetAllResourceUriAsync(subscriptionId);

                // Filter for storage account resources that match our name
                var storageAccountResourceIds = resourceIds
                    .Where(r => r.Contains("/Microsoft.Storage/storageAccounts/") &&
                                r.EndsWith($"/{storageAccountName}", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (storageAccountResourceIds.Any())
                {
                    // Return the first matching resource ID
                    return storageAccountResourceIds.First();
                }

                return $"Error: Could not find storage account '{storageAccountName}' in subscription '{subscriptionId}'";
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Error getting resource ID from storage service URI {Uri} in subscription {SubscriptionId}", storageServiceUri, subscriptionId);
                return $"Error: Exception occurred while processing storage service URI: {ex.Message}";
            }
        }

        private bool IsReadOnlyCommand(string command)
        {
            var commandLower = command.ToLower();

            // Check if command contains read-only verbs
            return _allowedReadVerbs.Any(verb => commandLower.Contains($" {verb} ") || commandLower.Contains($" {verb}"));
        }

        private bool IsWriteCommand(string command)
        {
            var commandLower = command.ToLower();

            // Check if command contains write verbs
            return _writeVerbs.Any(verb => commandLower.Contains($" {verb} ") || commandLower.Contains($" {verb}"));
        }

        private bool IsDeleteCommand(string command)
        {
            var deleteVerbs = new[] { "delete", "remove" };
            var commandLower = command.ToLower();

            // Check if command contains delete verbs as primary action
            return _blockedDeleteVerbs.Any(verb => commandLower.Contains($" {verb} ") || commandLower.Contains($" {verb}"));
        }

        private string GetCommandDescription(string command)
        {
            // Extract a user-friendly description from the command
            if (command.Contains("create"))
                return "Creating new Azure resource";
            if (command.Contains("update"))
                return "Updating Azure resource";
            if (command.Contains("set"))
                return "Setting resource configuration";
            if (command.Contains("scale"))
                return "Scaling Azure resource";
            if (command.Contains("start"))
                return "Starting Azure resource";
            if (command.Contains("stop"))
                return "Stopping Azure resource";
            if (command.Contains("restart"))
                return "Restarting Azure resource";

            // Extract the main verb and resource type if possible
            var parts = command.Split(' ');
            if (parts.Length >= 3)
            {
                return $"Executing {parts[1]} {parts[2]}";
            }

            return "Executing Azure CLI write command";
        }
    }
}

public sealed class LowerCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) => name.ToLowerInvariant();
}
