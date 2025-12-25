// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Core.Helpers;

public enum CliErrorType
{
    None,
    AuthorizationError,
    ValidationError,
    NotFoundError,
    Other
}

public class CliExecutionResult
{
    public string Output { get; set; } = string.Empty;
    public CliErrorType ErrorType { get; set; } = CliErrorType.None;
    public string? RequiredScopes { get; set; }

    public bool ErrorOccurred => ErrorType != CliErrorType.None;
}

public class CliErrorIndicator
{
    [Description("""
    The error type if error is detected in the output. Allowed values are:
    - None: No error detected
    - AuthorizationError: Indicates an authorization issue, such as insufficient permissions or invalid credentials.
    - ValidationError: Indicates a validation issue, such as incorrect command syntax or invalid parameters.
    - NotFoundError: Indicates the resource was not found.
    - Other: Any other type of error that does not fit into the above categories.
    """)]
    public string ErrorType { get; set; } = string.Empty;

    [Description("""
    The required AAD scopes/audiences for the Azure CLI command to succeed, inferred from the command and error message.
    """)]
    public string? RequiredScopes { get; set; }

    // [Description("""
    // A one-line summary of the error, if any. If no error is detected, this should be an empty string.
    // The summary should be user friendly and descriptive.
    // The summary should not include any technical jargon,  error codes, further actions.
    // Make sure to use first-person perspective, like 'I don't xxx', 'I can't xxx'. 
    // """)]
    // public string ErrorSummary { get; set; } = string.Empty;
}

public static class CliExecutionHelper
{
    public static async Task<CliExecutionResult> ParseCliExecutionResult(IChatClient chatClient, string output, string? command = null)
    {
        var msg = new ChatMessage(ChatRole.System, $"""
            You are a helpful assistant that analyzes Azure CLI and kubectl command outputs to determine if the COMMAND EXECUTION itself failed.
            IMPORTANT: You must distinguish between:
            1. Command execution errors (the command itself failed to run properly)
            2. Resource status information (the command ran successfully but shows unhealthy resource states)
            
            Command execution errors include:
            - Authentication/authorization failures
            - Invalid command syntax or parameters
            - Network connectivity issues
            - Resource not found errors
            - Permission denied errors
            - Command timeouts or connection failures
            
            Resource status information (NOT command errors):
            - Pods in CrashLoopBackOff, Failed, or Pending states
            - Services showing unhealthy endpoints
            - Nodes showing NotReady status
            - Deployments with failed replicas
            - Events showing resource problems
            - Any other status information about Kubernetes resources being unhealthy
            
            Examples of successful command execution (even if resources are unhealthy):
            - "kubectl get pods" returning pods in CrashLoopBackOff state
            - "kubectl describe pod" showing crash events
            - "az vm show" returning a stopped VM
            - "kubectl get events" showing error events
            
            Examples of command execution errors:
            - "Error: You must be logged in to the server"
            - "command not found"
            - "Invalid resource name"
            - "Forbidden: insufficient permissions"
            - "Connection refused"
            - "The resource 'xyz' was not found"
            
            Also, infer the required AAD scopes based on the command and the output. Always consider that if commands may require multiple scopes; in such cases, return a comma-separated list of scopes.
            
            Analyze determine if the COMMAND EXECUTION failed:
            Command:
            {command}

            Output:
            {output}
            """);

        var chatOptions = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.3f,
        };

        var (_, obj) = await chatClient.GetResponseAsync([msg], typeof(CliErrorIndicator), chatOptions);

        if (obj is CliErrorIndicator result)
        {
            var errorType = Enum.TryParse<CliErrorType>(result.ErrorType, true, out var parsedErrorType)
                ? parsedErrorType
                : CliErrorType.None;

            return new CliExecutionResult
            {
                Output = output,
                ErrorType = errorType,
                RequiredScopes = errorType == CliErrorType.AuthorizationError ? result.RequiredScopes : null,
            };
        }

        return new CliExecutionResult
        {
            Output = output,
        };
    }

    public static bool IsPending(this AzCliExecutionStatus status)
    {
        return status == AzCliExecutionStatus.Pending || status == AzCliExecutionStatus.PendingAuthorization;
    }

    public static bool IsPending(this KubectlExecutionStatus status)
    {
        return status == KubectlExecutionStatus.Pending || status == KubectlExecutionStatus.PendingAuthorization;
    }
}
