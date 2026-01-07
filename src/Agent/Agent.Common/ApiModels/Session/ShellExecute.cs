using System.Text.Json.Serialization;

namespace Agent.Common.ApiModels.Session;

public class ShellExecuteRequest : SessionRequest
{
    // The shell scripts to be executed.
    public required string ShellScripts { get; set; }
    // Optional standard input
    public string? Stdin { get; set; }
}

public class AzCliExecutionRequest : ShellExecuteRequest
{
    // To be removed after 25.12.64.0 is deployed
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
