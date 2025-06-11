// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Data;
using Kusto.Cloud.Platform.Data;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Kusto;

public class KustoRegionalGroupClient
{
    private readonly ILogger<KustoRegionalGroupClient> _logger;
    private readonly KustoClient _kustoClient;
    private readonly KustoRegionalGroupSettings _groupSettings;
    private readonly Dictionary<string, KustoCluster> _regionsToClusters = new();

    public KustoRegionalGroupClient(ILogger<KustoRegionalGroupClient> logger, KustoRegionalGroupSettings groupSettings, KustoClient kustoClient)
    {
        _logger = logger;
        _kustoClient = kustoClient;
        _groupSettings = groupSettings;
        _regionsToClusters = groupSettings.Regions.ToDictionary(c => c.Region, c => c);
    }

    public  Task<IDataReader> PerformQueryAsync(string query, string region)
    {
        KustoCluster kustoCluster = GetCluster(region);

        return _kustoClient.PerformQueryAsync(kustoCluster.ClusterUri, kustoCluster.Database, query);
    }


    public async Task<IEnumerable<T>> PerformQueryAsync<T>(string query, string region)
    {
        KustoCluster kustoCluster = GetCluster(region);

        IDataReader result = await _kustoClient.PerformQueryAsync(kustoCluster.ClusterUri, kustoCluster.Database, query);

        return result.ToEnumerable<T>();
    }

    public Task<IDataReader> PerformQueryWithParametersAsync(string query, string region, Dictionary<string, object> parameters)
    {
        KustoCluster kustoCluster = GetCluster(region);

        return _kustoClient.PerformQueryWithParametersAsync(kustoCluster.ClusterUri, kustoCluster.Database, query, parameters);
    }

    public KustoCluster GetCluster(string region)
    {
        try
        {
            return _regionsToClusters[region];
        }
        catch (KeyNotFoundException)
        {
            throw new InvalidOperationException($"Region {region} is not configured in Kusto settings for {_groupSettings.Name}");
        }
    }
}

