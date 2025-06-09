// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    public class ArmPlugin : IArmPlugin
    {
        private readonly ILogger<ArmPlugin> _logger;
        private readonly ArmHelper _armHelper;
        private readonly IThreadRepository _threadRepository;

        public Guid? ThreadId { get; set; }

        public ArmPlugin(ILogger<ArmPlugin> logger, ArmHelper armHelper, IThreadRepository threadRepository)
        {
            _logger = logger;
            _armHelper = armHelper;
            _threadRepository = threadRepository;
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

        public async Task<string> CheckConnectivityToAzureWebJobsStorage(string resourceId)
        {
            return await _armHelper.CheckConnectivityToAzureWebJobsStorage(resourceId);
        }

        public async Task<string> CheckTcpConnectivity(string resourceId, string host, int port)
        {
            return await _armHelper.CheckTcpConnectivityAsync(resourceId, host, port);
        }

        public async Task<string> CheckDnsResolution(string resourceId, string desinationUrl)
        {
            return await _armHelper.CheckDnsResolution(resourceId, desinationUrl);
        }

        public async Task<IDictionary<string, string>> GetAppSetting(string resourceId, string appSettingKey)
        {
            return await _armHelper.GetAppSetting(resourceId, appSettingKey);
        }

        public async Task<bool> ListKeysAndUpdateAppSettingsAsync(string storageResourceId, string appServiceResourceId, string appSettingKey)
        {
            return await _armHelper.ListKeysAndUpdateAppSettingsAsync(storageResourceId, appServiceResourceId, appSettingKey);
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
                    return "Error: This method only supports read operations (list, show, get). Use RunAzCliWriteCommandsAsync for write operations.";
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
                try
                {
                    // Execute the actual command synchronously
                    var output = await _armHelper.RunAzCliReadCommandsAsync(command);

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
                            UserId: "SRE Agent",
                            Role: Role.User
                        ),
                        Error = ex.Message,
                        CompletedTimestamp = DateTime.UtcNow
                    };

                    await _threadRepository.UpdateAzCliExecutionAsync(ThreadId.Value, execution);

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
                // Validate it's a write command and not a delete
                if (!IsWriteCommand(command))
                {
                    return "Error: This method only supports write operations (create, update, set, scale, start, stop, restart).";
                }

                if (IsDeleteCommand(command))
                {
                    return "Error: Delete operations are not allowed for safety reasons. Please use the Azure Portal for deletions.";
                }

                if (ThreadId == null)
                {
                    return "Error: ThreadId is not set. Please set the ThreadId before running commands.";
                }

                var executionId = Guid.NewGuid();

                // Create execution record in Pending state for approval
                var execution = new AzCliExecution(
                    Id: executionId,
                    Command: command,
                    Description: GetCommandDescription(command),
                    Status: AzCliExecutionStatus.Pending,
                    OriginalFunctionCall: null, // temporary, will be set later
                    Output: null,
                    Error: null,
                    CreatedTimestamp: DateTime.UtcNow,
                    StartedTimestamp: null,
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

                return "Azure CLI write command has been prepared for approval. Please click 'Run' to execute or 'Cancel' to dismiss.";
            }
            catch (Exception ex)
            {
                _logger?.LogInternalError(ex, "Failed to create execution for write command: {Command}", command);
                return $"Failed to prepare command execution: {ex.Message}";
            }
        }

        public async Task<string> GetAzCliHelpAsync(string helpTopic, string grepPattern = null)
        {
            try
            {
                // Use the ArmHelper to get help information
                var helpCommand = $"az {helpTopic} --help";
                var helpOutput = await _armHelper.RunAzCliReadCommandsAsync(helpCommand);

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
        private bool IsReadOnlyCommand(string command)
        {
            var readOnlyVerbs = new[] { "list", "show", "get" };
            var commandLower = command.ToLower();

            // Check if command contains read-only verbs
            return readOnlyVerbs.Any(verb => commandLower.Contains($" {verb} ") || commandLower.Contains($" {verb}"));
        }

        private bool IsWriteCommand(string command)
        {
            var writeVerbs = new[] { "create", "update", "set", "scale", "start", "stop", "restart", "add", "remove", "upgrade", "query" };
            var commandLower = command.ToLower();

            // Check if command contains write verbs
            return writeVerbs.Any(verb => commandLower.Contains($" {verb} ") || commandLower.Contains($" {verb}"));
        }

        private bool IsDeleteCommand(string command)
        {
            var deleteVerbs = new[] { "delete", "remove" };
            var commandLower = command.ToLower();

            // Check if command contains delete verbs as primary action
            return deleteVerbs.Any(verb => commandLower.Contains($" {verb} ") || commandLower.Contains($" {verb}"));
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

