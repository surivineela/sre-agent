using Microsoft.Extensions.Logging;

namespace Session.Proxy.Services.McpProtocol;

/// <summary>
/// Factory for creating protocol handlers based on version.
/// </summary>
public class ProtocolHandlerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ProtocolHandlerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IProtocolHandler CreateHandler(int protocolVersion)
    {
        return protocolVersion switch
        {
            1 => new ProtocolHandlerV1(_loggerFactory.CreateLogger<ProtocolHandlerV1>()),
            2 => new ProtocolHandlerV2(_loggerFactory.CreateLogger<ProtocolHandlerV2>()),
            _ => throw new ArgumentException($"Unsupported protocol version {protocolVersion}. Supported versions: 1, 2.", nameof(protocolVersion))
        };
    }
}
