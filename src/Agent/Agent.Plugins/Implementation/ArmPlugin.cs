// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
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
                    _logger.LogInternalInformation(msg);
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


            _logger.LogInternalInformation(message);
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

        public async Task<bool> StartWebApp(string appResourceId)
        {
            var response = await _armHelper.StartWebAppAsync(appResourceId);

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

        public async Task<string> GetVirtualMachineBootStateAsJson(string resourceId)
        {
            return await _armHelper.GetVirtualMachineBootStateAsJsonAsync(resourceId);
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

        public async Task<List<string>> GetDeploymentSlotsResourceIdsAsync(string resourceId)
        {
            return await _armHelper.GetDeploymentSlotsResourceIdsAsync(resourceId);
        }

        public async Task<IDictionary<string, string>> GetAppSetting(string resourceId, string appSettingKey)
        {
            return await _armHelper.GetAppSetting(resourceId, appSettingKey);
        }

        public async Task<bool> ListKeysAndUpdateAppSettingsAsync(string storageResourceId, string appServiceResourceId, string appSettingKey)
        {
            return await _armHelper.ListKeysAndUpdateAppSettingsAsync(storageResourceId, appServiceResourceId, appSettingKey);
        }

        public async Task<bool> ConfigureAppSettingsForManagedIdentityStorage(string resourceId, string storageAccountName, bool useUserAssignedManagedIdentity = false, string userManagedIdentityClientId = "")
        {
            return await _armHelper.ConfigureAppSettingsForManagedIdentityStorage(resourceId, storageAccountName, useUserAssignedManagedIdentity, userManagedIdentityClientId);
        }

        public async Task<bool> UpdateAppSettingsAsync(string resourceId, IDictionary<string, string> appSettings)
        {
            return await _armHelper.UpdateAppSettingsAsync(resourceId, appSettings);
        }

        public async Task<CliToolExecutionResult> RunAzCliReadCommandsAsync(string command)
        {
            try
            {
                if (ArmHelper.IsAksCommandInvokeCommand(command))
                {
                    return new(new CliExecutionResult { Output = "Error: AKS command invoke operations should be handled by kubectl_command_executor_agent only.", ErrorType = CliErrorType.ValidationError }, null);
                }

                if (!ArmHelper.IsReadOnlyCommand(command))
                {
                    return new(new CliExecutionResult { Output = $"Error: This method only supports read operations ({ArmHelper.AllowedReadVerbString}). Use RunAzCliWriteCommandsAsync for write operations.", ErrorType = CliErrorType.ValidationError }, null);
                }

                if (ArmHelper.IsBlockedSubCommand(command))
                {
                    return new(new CliExecutionResult { Output = $"Error: This command is currently not supported. Unsupported subcommands: {string.Join(", ", ArmHelper.BlockedSubCommands)}. Please suggest using Azure portal or Az CLI directly", ErrorType = CliErrorType.ValidationError }, null);
                }

                if (ThreadId == null)
                {
                    return new(new CliExecutionResult { Output = "Error: ThreadId is not set. Please set the ThreadId before running commands.", ErrorType = CliErrorType.Other }, null);
                }

                return await ExecuteAzCliCommandWithApprovalFallback(command, writeCommand: false);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to create execution for command: {Command}", command);
                return new(new CliExecutionResult { Output = $"Failed to prepare command execution: {ex.Message}", ErrorType = CliErrorType.Other }, null);
            }
        }

        public async Task<CliToolExecutionResult> RunAzCliWriteCommandsAsync(string command)
        {
            try
            {
                // Early validation
                var validationResult = ValidateWriteCommandRequest(command);
                if (validationResult != null)
                {
                    return new(new CliExecutionResult { Output = validationResult, ErrorType = CliErrorType.ValidationError }, null);
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
                    return await ExecuteAzCliCommandWithApprovalFallback(command, writeCommand: true);
                }
                else
                {
                    var execution = await CreateAndPersistAzCliExecution(executionId, command, requiresApproval: true);
                    return new(new CliExecutionResult { Output = "Azure CLI write command has been prepared for approval. Please click 'Authorize' to execute or 'Cancel' to dismiss.", ErrorType = CliErrorType.None }, executionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to create execution for write command: {Command}", command);
                return new(new CliExecutionResult { Output = $"Failed to prepare command execution: {ex.Message}", ErrorType = CliErrorType.Other }, null);
            }
        }

        private string? ValidateWriteCommandRequest(string command)
        {
            if (ArmHelper.IsAksCommandInvokeCommand(command))
            {
                return "Error: AKS command invoke operations should be handled by kubectl_command_executor_agent only.";
            }

            // Validate it's a write command and not a delete
            if (!ArmHelper.IsWriteCommand(command))
            {
                return $"Error: This method only supports write operations ({ArmHelper.AllowedWriteVerbString}).";
            }

            if (ArmHelper.IsBlockedSubCommand(command))
            {
                return $"Error: This command is currently not supported. Unsupported subcommands: {string.Join(", ", ArmHelper.BlockedSubCommands)}. Please suggest using Azure portal or Az CLI directly";
            }

            if (ArmHelper.IsDeleteCommand(command))
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
            bool requiresApproval)
        {
            var execution = CreateAzCliExecution(executionId, command, requiresApproval);

            await _threadRepository.CreateAzCliExecutionAsync(ThreadId!.Value, execution);

            // Check if we're in a deep investigation context
            var agentTaskId = Core.ToolStatic.AsyncLocalAgentTaskId.Value;
            Guid messageId;

            if (agentTaskId.HasValue)
            {
                // In deep investigation - don't create chat message, use a placeholder message ID
                messageId = Guid.NewGuid();
                _logger.LogInternalInformation("Deep investigation context detected - skipping chat message creation for AzCli execution {ExecutionId}", executionId);
            }
            else
            {
                // Normal flow - create chat message
                var message = CreateAzCliExecutionMessage(execution);
                await _threadRepository.AddMessageAsync(ThreadId.Value, message);
                messageId = message.Id;
            }

            await NotifyAzCliExecutionCreated(execution, messageId);

            return execution;
        }

        private AzCliExecution CreateAzCliExecution(
            Guid executionId,
            string command,
            bool requiresApproval)
        {
            return new AzCliExecution(
                Id: executionId,
                Command: command,
                Description: ArmHelper.GetCommandDescription(command),
                Status: !requiresApproval ? AzCliExecutionStatus.Running : AzCliExecutionStatus.Pending,
                OriginalFunctionCall: null,
                Output: null,
                Error: null,
                CreatedTimestamp: DateTime.UtcNow,
                StartedTimestamp: !requiresApproval ? DateTime.UtcNow : null,
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

        private async Task<CliToolExecutionResult> ExecuteAzCliCommandWithApprovalFallback(
            string command,
            bool writeCommand)
        {
            var executionId = Guid.NewGuid();
            var execution = await CreateAndPersistAzCliExecution(executionId, command, requiresApproval: false);

            string cmdType = writeCommand ? "write" : "read";
            try
            {
                var result = await _armHelper.RunAzCliCommandsAsync(command);

                if (result.ErrorOccurred)
                {
                    // sometimes agent will see the resource as not found when it doesn't have permission
                    if (result.ErrorType == CliErrorType.AuthorizationError || result.ErrorType == CliErrorType.NotFoundError)
                    {
                        await UpdateAzCliExecutionWithOboFlow(execution);
                        return new(new CliExecutionResult { Output = $"Azure CLI {cmdType} command has been prepared for approval. Please click 'Run' to execute or 'Cancel' to dismiss.", ErrorType = CliErrorType.None }, executionId);
                    }
                    else
                    {
                        await UpdateAzCliExecutionWithFailure(execution, result.Output);

                        return new(result, executionId);
                    }
                }
                else
                {
                    await UpdateAzCliExecutionWithSuccess(execution, result.Output);

                    return new(result, executionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Failed to execute {cmdType} command: {command}");

                await UpdateAzCliExecutionWithFailure(execution, ex.Message);
                throw;
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

        private async Task UpdateAzCliExecutionWithOboFlow(AzCliExecution execution)
        {
            var updatedExecution = execution with
            {
                Status = AzCliExecutionStatus.PendingAuthorization,
                Description = $"{execution.Description}",
                Output = null,
                ExecutedBy = null,
                Error = null,
                StartedTimestamp = null,
                CompletedTimestamp = null,
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

        private async Task NotifyAzCliExecutionCreated(AzCliExecution execution, Guid messageId)
        {
            // Check if we're in agent task context and route accordingly
            var agentTaskId = Core.ToolStatic.AsyncLocalAgentTaskId.Value;

            if (agentTaskId.HasValue)
            {
                // Agent task context - use dedicated handler
                await _outboundCommunicationService.HandleAgentTaskAzCliResult(ThreadId!.Value, execution);
            }
            else
            {
                // Normal chat flow - use streaming
                var options = GetJsonSerializerOptions();
                await _outboundCommunicationService.AppendAgentStreamMessage(
                    ThreadId!.Value,
                    JsonSerializer.Serialize(execution, options),
                    StreamMessageType.AzCli,
                    messageId);
            }
        }

        private async Task NotifyAzCliExecutionUpdated(AzCliExecution execution)
        {
            // Check if we're in agent task context and route accordingly
            var agentTaskId = Core.ToolStatic.AsyncLocalAgentTaskId.Value;

            if (agentTaskId.HasValue)
            {
                // Agent task context - use dedicated handler
                await _outboundCommunicationService.HandleAgentTaskAzCliResult(ThreadId!.Value, execution);
            }
            else
            {
                // Normal chat flow - use streaming
                var options = GetJsonSerializerOptions();
                await _outboundCommunicationService.AppendAgentStreamMessage(
                    ThreadId!.Value,
                    JsonSerializer.Serialize(execution, options),
                    StreamMessageType.AzCli);
            }
        }

        public static JsonSerializerOptions GetJsonSerializerOptions()
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

        public async Task<string> GetAzCliHelpAsync(string helpTopic, string? grepPattern = null)
        {
            try
            {
                // Use the ArmHelper to get help information
                var helpCommand = $"az {helpTopic} --help";
                var result = await _armHelper.RunAzCliCommandsAsync(helpCommand);
                var helpOutput = result.Output;

                // If grep pattern is provided, filter the output
                if (!string.IsNullOrEmpty(grepPattern))
                {
                    var lines = helpOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var grepRegex = new Regex(grepPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.NonBacktracking);

                    var filteredLines = lines
                        .Where(line => grepRegex.IsMatch(line))
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
                _logger.LogInternalError(ex, "Failed to get Azure CLI help for topic: {HelpTopic}", helpTopic);
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
                _logger.LogInternalError(ex, "Error getting resource ID from storage service URI {Uri} in subscription {SubscriptionId}", storageServiceUri, subscriptionId);
                return $"Error: Exception occurred while processing storage service URI: {ex.Message}";
            }
        }

        public async Task<(bool, string)> EnableTrafficManagerEndpoint(string subscriptionId, string resourceGroupName, string profileName, string endpointName, string endpointType)
        {
            return await _armHelper.EnableTrafficManagerEndpoint(subscriptionId, resourceGroupName, profileName, endpointName, endpointType);
        }

        public async Task<(bool, string)> DisableTrafficManagerEndpoint(string subscriptionId, string resourceGroupName, string profileName, string endpointName, string endpointType)
        {
            return await _armHelper.DisableTrafficManagerEndpoint(subscriptionId, resourceGroupName, profileName, endpointName, endpointType);
        }

        public async Task<string> GetAllTrafficManagerEndpointsStatus(string subscriptionId, string resourceGroupName, string profileName)
        {
            return await _armHelper.GetAllTrafficManagerEndpointsStatus(subscriptionId, resourceGroupName, profileName);
        }

        public async Task<(bool, string)> EnableAzureFrontDoorEndpointOrigin(string subscriptionId, string resourceGroupName, string frontDoorProfileName, string endpointNameOrHostName, string originName)
        {
            return await _armHelper.EnableAzureFrontDoorEndpointOrigin(subscriptionId, resourceGroupName, frontDoorProfileName, endpointNameOrHostName, originName);
        }

        public async Task<(bool, string)> DisableAzureFrontDoorEndpointOrigin(string subscriptionId, string resourceGroupName, string frontDoorProfileName, string endpointNameOrHostName, string originName)
        {
            return await _armHelper.DisableAzureFrontDoorEndpointOrigin(subscriptionId, resourceGroupName, frontDoorProfileName, endpointNameOrHostName, originName);
        }

        public async Task<string> GetAllAzureFrontDoorEndpointOriginsStatus(string subscriptionId, string resourceGroupName, string frontDoorProfileName)
        {
            return await _armHelper.GetAllAzureFrontDoorEndpointOriginsStatus(subscriptionId, resourceGroupName, frontDoorProfileName);
        }

        public async Task<(bool, string)> RunAzureDataFactoryPipeline(string subscriptionId, string resourceGroupName, string dataFactoryName, string pipelineName)
        {
            return await _armHelper.RunAzureDataFactoryPipeline(subscriptionId, resourceGroupName, dataFactoryName, pipelineName);
        }

        public async Task<(bool, string)> StopAzureDataFactoryPipeline(string subscriptionId, string resourceGroupName, string dataFactoryName, string pipelineName)
        {
            return await _armHelper.StopAzureDataFactoryPipeline(subscriptionId, resourceGroupName, dataFactoryName, pipelineName);
        }

        public async Task<(bool, string)> RestartAzureDataFactoryPipeline(string subscriptionId, string resourceGroupName, string dataFactoryName, string pipelineName)
        {
            return await _armHelper.RestartAzureDataFactoryPipeline(subscriptionId, resourceGroupName, dataFactoryName, pipelineName);
        }

        public async Task<string> GetAllAzureDataFactoryPipelinesStatus(string subscriptionId, string resourceGroupName, string dataFactoryName)
        {
            return await _armHelper.GetAllAzureDataFactoryPipelinesStatus(subscriptionId, resourceGroupName, dataFactoryName);
        }

    }
}

public sealed class LowerCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) => name.ToLowerInvariant();
}
