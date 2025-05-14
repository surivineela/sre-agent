// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Clients;

public class KustoRegionalGroupClientProvider
{
    private readonly ILogger<KustoClient> _logger;
    private readonly KustoClient _kustoClient;
    private readonly KustoSettings _kustoSettings;

    public KustoRegionalGroupClientProvider(ILogger<KustoClient> logger, KustoSettings kustoSettings, KustoClient kustoClient)
    {
        _logger = logger;
        _kustoClient = kustoClient;
        _kustoSettings = kustoSettings;
    }

    public KustoRegionalGroupClient GetContainerAppsKustoClient()
    {
        KustoRegionalGroupSettings groupSettings = _kustoSettings.RegionalClusterGroups.Single(x => string.Equals(x.Name, "ContainerApps", StringComparison.OrdinalIgnoreCase));

        return new KustoRegionalGroupClient(_logger, groupSettings, _kustoClient);
    }

}

