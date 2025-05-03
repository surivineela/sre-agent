// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Plugins.Implementation;
public class ContainerAppEnvoyPlugin : IContainerAppEnvoyPlugin
{
    private readonly ILogger<ContainerAppRevisionPlugin> _logger;
    private readonly IKustoPlugin _kustoPlugin;

    public ContainerAppEnvoyPlugin(ILogger<ContainerAppRevisionPlugin> logger, IKustoPlugin kustoPlugin)
    {
        _logger = logger;
        _kustoPlugin = kustoPlugin;
    }

    private Task<string> Execute(string functionName, string region, Dictionary<string, string> args)
    {
        var fileName = Path.Combine(AppContext.BaseDirectory, "Plugins", "Definitions", "Queries", $"{functionName}.kql");

        if (File.Exists(fileName))
        {
            var formatted = File.ReadAllText(fileName);
            // replace ##placeholder## with value
            foreach (var arg in args)
            {
                formatted = formatted.Replace($"##{arg.Key}##", arg.Value);
            }

            if (formatted.Contains("##"))
            {
                _logger.LogError($"Not all placeholders were replaced in the query");
                throw new Exception($"Not all placeholders were replaced in the query, {formatted}");
            }

            return _kustoPlugin.ExecuteKustoQuery(region, formatted);
        }
        else
        {
            return _kustoPlugin.ExecuteFunctionAsync(functionName, region, args);
        }
    }

    public Task<string> GetEnvoyAbnormalLogs(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetEnvoyAbnormalLogs", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }

    public Task<string> GetEnvoyControllerLogs(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetEnvoyControllerLogs", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }


    public Task<string> GetEnvoyAccessLogs(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetEnvoyAccessLogs", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }

    public Task<string> GetSwiftNetworkingEvents(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetSwiftNetworkingEvents", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
        });
    }
}
