using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace Agent.Plugins.Implementation;

public class PostgreSQLAutomationPlugin : IPostgreSQLAutomationPlugin
{
    private readonly ILogger<PostgreSQLAutomationPlugin> _logger;
    private readonly IThreadRepository _threadRepository;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
    private readonly PostgresSQLCommandHelper _postgresSQLCommandHelper;

    public Guid? ThreadId { get; set; }

    public PostgreSQLAutomationPlugin(
        ILogger<PostgreSQLAutomationPlugin> logger,
        IThreadRepository threadRepository,
        IAgentOutboundCommunicationService agentOutboundCommunicationService,
        PostgresSQLCommandHelper postgresSQLCommandHelper)
    {
        _logger = logger;
        _threadRepository = threadRepository;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
        _postgresSQLCommandHelper = postgresSQLCommandHelper;
    }

    public async Task<CliExecutionResult> RunPsqlReadCommandAsync(string command, string? database = null)
    {
        _logger.LogInternalInformation($"Starting PSQL read command execution for command: {command}");

        // First validate the command
        var validationSummary = await ValidatePsqlCommandAsync(command, database);
        if (validationSummary != null)
        {
            return new CliExecutionResult
            {
                Output = validationSummary,
                ErrorType = CliErrorType.ValidationError
            };
        }

        try
        {
            // Validate ThreadId is set
            if (ThreadId == null)
            {
                return new CliExecutionResult
                {
                    Output = "ThreadId is not set. Cannot execute PostgreSQL command.",
                    ErrorType = CliErrorType.Other
                };
            }

            // Create execution record first so UI can display it
            var executionId = Guid.NewGuid();
            var execution = CreatePsqlExecution(executionId, command, requiresApproval: false);

            // Save execution to database and create message for UI
            await _threadRepository.CreatePsqlExecutionAsync(ThreadId.Value, execution);
            var message = CreateExecutionMessage(execution);
            await _threadRepository.AddMessageAsync(ThreadId.Value, message);

            // Notify UI that execution was created
            await NotifyPgSqlExecutionCreated(execution, message.Id);

            // Execute with approval fallback pattern (same as Azure CLI and kubectl)
            var result = await ExecutePsqlWithApprovalFallback(execution, command, database, writeCommand: false);

            return new CliExecutionResult
            {
                Output = result,
                ErrorType = CliErrorType.None
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to execute PSQL command: {command}");
            return new CliExecutionResult
            {
                Output = ex.Message,
                ErrorType = CliErrorType.Other
            };
        }
    }

    public Task<string?> ValidatePsqlCommandAsync(string command, string? database = null)
    {
        try
        {
            // Use the PostgresSQLCommandHelper's comprehensive validation
            var validationResult = PostgresSQLCommandHelper.ValidateReadOnlyCommand(command);
            return Task.FromResult(validationResult);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to validate PSQL command: {command}");
            return Task.FromResult<string?>($"[Validation Failed]: Validation error: {ex.Message}");
        }
    }

    private PsqlExecution CreatePsqlExecution(
        Guid executionId,
        string command,
        bool requiresApproval)
    {
        return new PsqlExecution(
            Id: executionId,
            Command: command,
            Description: $"Execute PostgreSQL read command: {command.Substring(0, Math.Min(command.Length, 50))}...",
            Status: requiresApproval ? AzCliExecutionStatus.PendingAuthorization : AzCliExecutionStatus.Pending,
            OriginalFunctionCall: null,
            Output: null,
            Error: null,
            CreatedTimestamp: DateTime.UtcNow,
            StartedTimestamp: null,
            CompletedTimestamp: null,
            ExecutedBy: null,
            AgentContextId: null
        );
    }

    private async Task NotifyPgSqlExecutionCreated(PsqlExecution execution, Guid messageId)
    {
        var options = ArmPlugin.GetJsonSerializerOptions();
        await _agentOutboundCommunicationService.AppendAgentStreamMessage(
            ThreadId!.Value,
            JsonSerializer.Serialize(execution, options),
            StreamMessageType.Psql,
            messageId);
    }

    private static Message CreateExecutionMessage(PsqlExecution execution)
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
            KubectlExecution: null,
            PsqlExecution: execution,
            IncidentDiscussionId: null,
            IsDailyReport: false
        );
    }

    private async Task<string> ExecutePsqlWithApprovalFallback(
        PsqlExecution execution,
        string command,
        string? database,
        bool writeCommand)
    {
        string cmdType = writeCommand ? "write" : "read";

        try
        {
            // Try to execute immediately first (same pattern as Azure CLI and kubectl)
            var result = await TryExecutePsqlCommand(command, database);

            if (result.HasError)
            {
                // Check if it's an authorization error that requires approval
                if (IsAuthorizationError(result.ErrorMessage))
                {
                    await UpdateExecutionWithOboFlow(execution);
                    return $"PostgreSQL {cmdType} command has been prepared for approval. Please click 'Run' to execute or 'Cancel' to dismiss.";
                }
                else
                {
                    await UpdateExecutionWithFailure(execution, result.ErrorMessage);
                    return $"PostgreSQL {cmdType} command failed. Output: {result.ErrorMessage}";
                }
            }
            else
            {
                await UpdateExecutionWithSuccess(execution, result.Output);
                return $"PostgreSQL {cmdType} command completed successfully. Output: {result.Output}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to execute {cmdType} command: {command}");
            await UpdateExecutionWithFailure(execution, ex.Message);
            throw;
        }
    }

    private async Task<(bool HasError, string Output, string ErrorMessage)> TryExecutePsqlCommand(string command, string? database)
    {
        try
        {
            // Execute the PostgreSQL command using the helper
            var result = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(command, database: database);

            if (result.ErrorType != CliErrorType.None)
            {
                return (true, string.Empty, result.Output);
            }

            return (false, result.Output, string.Empty);
        }
        catch (Exception ex)
        {
            return (true, string.Empty, ex.Message);
        }
    }

    private bool IsAuthorizationError(string errorMessage)
    {
        // Check for common PostgreSQL authorization error patterns
        if (string.IsNullOrEmpty(errorMessage))
            return false;

        var authErrorPatterns = new[]
        {
            "permission denied",
            "insufficient privilege",
            "access denied",
            "authentication failed",
            "role does not exist",
            "password authentication failed",
            "connection refused"
        };

        return authErrorPatterns.Any(pattern =>
            errorMessage.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private async Task UpdateExecutionWithSuccess(PsqlExecution execution, string output)
    {
        var updatedExecution = execution with
        {
            Status = AzCliExecutionStatus.Completed,
            ExecutedBy = CreateAgentAuthor(),
            Output = output,
            StartedTimestamp = DateTime.UtcNow,
            CompletedTimestamp = DateTime.UtcNow
        };

        await _threadRepository.UpdatePsqlExecutionAsync(ThreadId!.Value, updatedExecution);

        var message = CreateExecutionMessage(updatedExecution);
        await _threadRepository.AddMessageAsync(ThreadId.Value, message);

        // Notify UI of execution completion
        await _agentOutboundCommunicationService.AppendAgentStreamMessage(
            ThreadId.Value,
            JsonSerializer.Serialize(updatedExecution, ArmPlugin.GetJsonSerializerOptions()),
            StreamMessageType.Psql,
            message.Id);
    }

    private async Task UpdateExecutionWithFailure(PsqlExecution execution, string errorMessage)
    {
        var updatedExecution = execution with
        {
            Status = AzCliExecutionStatus.Failed,
            ExecutedBy = CreateAgentAuthor(),
            Error = errorMessage,
            StartedTimestamp = DateTime.UtcNow,
            CompletedTimestamp = DateTime.UtcNow
        };

        await _threadRepository.UpdatePsqlExecutionAsync(ThreadId!.Value, updatedExecution);

        var message = CreateExecutionMessage(updatedExecution);
        await _threadRepository.AddMessageAsync(ThreadId.Value, message);

        // Notify UI of execution failure
        await _agentOutboundCommunicationService.AppendAgentStreamMessage(
            ThreadId.Value,
            JsonSerializer.Serialize(updatedExecution, ArmPlugin.GetJsonSerializerOptions()),
            StreamMessageType.Psql,
            message.Id);
    }

    private async Task UpdateExecutionWithOboFlow(PsqlExecution execution)
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

        await _threadRepository.UpdatePsqlExecutionAsync(ThreadId!.Value, updatedExecution);

        var message = CreateExecutionMessage(updatedExecution);
        await _threadRepository.AddMessageAsync(ThreadId.Value, message);

        await NotifyPgSqlExecutionCreated(updatedExecution, message.Id);
    }

    private Author CreateAgentAuthor()
    {
        return new Author(
            DisplayName: "SRE Agent",
            UserId: "agent-default",
            Role: Role.SREAgent
        );
    }
}
