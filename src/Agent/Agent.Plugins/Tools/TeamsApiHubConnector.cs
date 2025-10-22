using Agent.Core.DataConnectors;
using Agent.Framework;

namespace Agent.Plugins.Tools;

public class TeamsApiHubConnector : DataConnectorDefinitionBase
{
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;

    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(ConnectionRuntimeUrl))
        {
            throw new ArgumentException("ConnectionRuntimeUrl cannot be null or empty.", nameof(ConnectionRuntimeUrl));
        }

        if (!Uri.TryCreate(ConnectionRuntimeUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("ConnectionRuntimeUrl must be a valid absolute URI.", nameof(ConnectionRuntimeUrl));
        }

        if (!Guid.TryParse(GroupId, out _))
        {
            throw new ArgumentException("GroupId must be a valid GUID.", nameof(GroupId));
        }

        if (string.IsNullOrWhiteSpace(ChannelId))
        {
            throw new ArgumentException("ChannelId cannot be null or empty.", nameof(ChannelId));
        }
    }
}
