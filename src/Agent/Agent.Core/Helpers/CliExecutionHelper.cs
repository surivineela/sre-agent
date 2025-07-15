// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Microsoft.Extensions.AI;

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
    public async static Task<CliExecutionResult> ParseCliExecutionResult(IChatClient chatClient, string output)
    {
        var msg = new ChatMessage(ChatRole.System, $"You are a helpful assistant that analyzes Azure CLI and kubectl command outputs. " +
            "Your task is to determine if the output indicates an error." +
            $"Here is the command output:\n{output}");

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
            };
        }

        return new CliExecutionResult
        {
            Output = output,
        };
    }
}
