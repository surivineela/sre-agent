using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Data.DatabaseClients.Attributes;
using Azure;
using Microsoft.Extensions.Logging;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public class ConnectionNode : ArmResourceNode
    {
        [GraphProperty("connectorId")] public string? ConnectorId { get; set; }

        public ConnectionNode(
                string resourceType,
                string resourceId,
                string subscriptionId,
                string resourceGroupName,
                string resourceName,
                string? location = null)
                : base(resourceType,
                      resourceId,
                      subscriptionId,
                      resourceGroupName,
                      resourceName,
                      location)
        {
        }

        public ConnectionNode(IDictionary<string, object> properties)
            : base(properties)
        {
            if (properties.TryGetValue("connectorId", out var connectorIdObj) && connectorIdObj != null)
            {
                try
                {
                    if (connectorIdObj is IEnumerable<object> connectorIdList)
                    {
                        var connectorIdString = connectorIdList.OfType<string>().FirstOrDefault();
                        if (!string.IsNullOrEmpty(connectorIdString))
                        {
                            ConnectorId = connectorIdString;
                        }
                    }
                    else if (connectorIdObj is string connectorIdString)
                    {
                        ConnectorId = connectorIdString;
                    }
                }
                catch
                {
                    ConnectorId = null;
                }
            }
        }

        public string ConnectorName => ConnectorId?.ToLowerInvariant().Split("/managedapis/").LastOrDefault() ?? string.Empty;
    }
}
