// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Kusto;

public class KustoRegionalGroupClientProvider
{
    private readonly ILogger<KustoRegionalGroupClient> _logger;
    private readonly KustoClient _kustoClient;
    private readonly KustoSettings _kustoSettings;

    public KustoRegionalGroupClientProvider(ILogger<KustoRegionalGroupClient> logger, KustoSettings kustoSettings, KustoClient kustoClient)
    {
        _logger = logger;
        _kustoClient = kustoClient;
        _kustoSettings = kustoSettings;
    }

    public KustoRegionalGroupClient GetRegionalGroupKustoClient(string groupName)
    {
        KustoRegionalGroupSettings groupSettings = _kustoSettings.RegionalClusterGroups.Single(x => string.Equals(x.Name, groupName, StringComparison.OrdinalIgnoreCase));

        return new KustoRegionalGroupClient(_logger, groupSettings, _kustoClient);
    }

}

