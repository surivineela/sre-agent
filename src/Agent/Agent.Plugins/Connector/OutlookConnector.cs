// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Plugins.Connector;

public class OutlookConnector : DataConnectorDefinitionBase
{
    public string ConnectionRuntimeUrl { get; set; } = string.Empty;

    public override void ConfigureFromDataSource(string dataSource)
    {
        ConnectionRuntimeUrl = dataSource;
    }

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
    }
}
