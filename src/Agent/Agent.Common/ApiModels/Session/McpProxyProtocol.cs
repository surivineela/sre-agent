namespace Agent.Common.ApiModels.Session;

/// <summary>
/// Constants for the MCP Proxy Protocol.
/// </summary>
public static class McpProxyProtocol
{
    /// <summary>
    /// Channel indicator for stdout messages (JSON-RPC).
    /// </summary>
    public const char ChannelStdout = '1';

    /// <summary>
    /// Channel indicator for stderr messages (diagnostic/error output).
    /// </summary>
    public const char ChannelStderr = '2';

    /// <summary>
    /// Numeric channel value for stdout.
    /// </summary>
    public const int ChannelStdoutValue = 1;

    /// <summary>
    /// Numeric channel value for stderr.
    /// </summary>
    public const int ChannelStderrValue = 2;
}
