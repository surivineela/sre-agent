using System.Text.Json.Serialization;

namespace Agent.Common.ApiModels.Session;

public class SessionRequest
{
    public List<string> Commands { get; set; } = [];

    public int TimeoutInSeconds { get; set; } = 60;
}

/// <summary>
/// Represents a response from the session pool service.
/// </summary>
public class SessionResponse
{
    /// <summary>
    /// The identifier of the session.
    /// </summary>
    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    /// <summary>
    /// The exit code of the execution.
    /// </summary>
    [JsonPropertyName("exitCode")]
    public int? ExitCode { get; set; }

    /// <summary>
    /// The result of the command execution.
    /// </summary>
    [JsonPropertyName("result")]
    public CommandResult? Result { get; set; }
}

/// <summary>
/// Represents the result of a command execution.
/// </summary>
public class CommandResult
{
    /// <summary>
    /// The standard output of the command.
    /// </summary>
    [JsonPropertyName("stdout")]
    public string? Stdout { get; set; }

    /// <summary>
    /// The standard error output of the command.
    /// </summary>
    [JsonPropertyName("stderr")]
    public string? Stderr { get; set; }

    /// <summary>
    /// The execution time in milliseconds.
    /// </summary>
    [JsonPropertyName("executionTimeInMilliseconds")]
    public long? ExecutionTimeInMilliseconds { get; set; }
}
