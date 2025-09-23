using System.Text.Json.Serialization;

namespace Agent.Core.Models.Session;

public class ShellExecuteRequest : SessionRequest
{
    // The shell scripts to be executed.
    public required string ShellScripts { get; set; }
    // Optional standard input
    public string? Stdin { get; set; }
}

public class AzCliExecutionRequest : ShellExecuteRequest
{
    public required Dictionary<string, string> AccessTokens { get; set; }
}

public class KubectlExecutionRequest : ShellExecuteRequest
{
}

public class ShellExecuteResponse : SessionResponse
{
    // Any error message encountered during execution. This is not the output of the command.
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
