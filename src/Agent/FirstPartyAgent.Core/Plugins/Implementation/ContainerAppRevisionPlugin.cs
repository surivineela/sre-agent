// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Plugins.Implementation;

// [MENDATORY]
public class ContainerAppRevisionPlugin : IContainerAppRevisionPlugin
{
    private readonly ILogger<ContainerAppRevisionPlugin> _logger;
    private readonly IRevisionService _RevisionService;
    private readonly IKustoPlugin _kustoPlugin;

    public ContainerAppRevisionPlugin(ILogger<ContainerAppRevisionPlugin> logger, IKustoPlugin kustoPlugin, IRevisionService RevisionService)
    {
        _logger = logger;
        _RevisionService = RevisionService;
        _kustoPlugin = kustoPlugin;
    }

    private Task<string> Execute(string functionName, string region, Dictionary<string, string> args)
    {
        var fileName = Path.Combine(AppContext.BaseDirectory,"Plugins", "Definitions", "Queries", $"{functionName}.kql");
        
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

    public Task<string> ListRevisions(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("ListRevisions", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
        });
    }

    public Task<string> GetActiveRevisionSessions(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetActiveRevisionSessions", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public Task<string> GetHpaHeartbeatMetrics(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetHpaHeartbeatMetrics", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public Task<string> GetRevisionSpecChanges(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetRevisionSpecChanges", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public Task<string> GetEventProcessorEventsWithoutReplica(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetEventProcessorEventsWithoutReplica", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public Task<string> GetPodHeartbeatStatus(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetPodHeartbeatStatus", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public Task<string> GetInternalEventProcessorEventsForPod(string region, DateTime fromDate, DateTime toDate, string revisionName, string podName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetInternalEventProcessorEventsForPod", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "podName", podName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }


    public Task<string> GetRevisionTrafficWithReplicaCount(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetRevisionReplicaAndTraffic", region,
            new Dictionary<string, string> {
        { "fromDate", fromDate.ToString() },
        { "toDate", toDate.ToString() },
        { "revisionName", revisionName },
        { "containerAppName", containerAppName },
        { "resourceGroupName", resourceGroupName },
        { "subscriptionId", subscriptionId },});
    }

    public Task<string> ContainerAppRevisionStatus(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetRevisionStatus", region,
            new Dictionary<string, string> {
        { "fromDate", fromDate.ToString() },
        { "toDate", toDate.ToString() },
        { "revisionName", revisionName },
        { "containerAppName", containerAppName },
        { "resourceGroupName", resourceGroupName },
        { "subscriptionId", subscriptionId },});
    }

    public Task<string> GetHttpScalerEventsForContainerApp(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetHttpScalerEventsForContainerApp", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public Task<string> GetKedaOperatorEventsForContainerApp(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return Execute("GetKedaOperatorEventsForContainerApp", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public Task<string> GetReplicaCount(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        string query = $@"
let startTime = datetime(""{fromDate}"");
let endTime = datetime(""{toDate}"");
let cappSubscription = ""{subscriptionId}"";
let cappResourceGroup = ""{resourceGroupName}"";
let cappName = ""{containerAppName}"";
let cappRevisionName = ""{revisionName}"";
let appArmId = strcat(""/subscriptions/"",cappSubscription,""/resourceGroups/"",cappResourceGroup,""/providers/Microsoft.App/containerApps/"",cappName);
let genevaAccountName = ""ContainerAppsMdm"";
let dimension_list = ""'containerAppArmId','revisionName'"";
let theSchema = datatable (TimestampUtc: datetime, revisionName: string, Max: real) [];
let sampling = ""Max"";
let duration = endTime - startTime;
let bins = datatable(span: timespan, bucket: timespan, mdm_bucket: string) [
        5m, 1m, '1m',
        1d, 1m, '1m',
        2d, 15m, '15m',
        3d, 30m, '30m',
        7d, 1h, '1h',
    ];
let spans = bins | where duration >= span | top 1 by span desc;
let bucket = coalesce(toscalar(spans | project bucket), 1d);
let mdm_bucket = coalesce(toscalar(spans | project mdm_bucket), '1d');
let mdmData = evaluate geneva_metrics_request(
	genevaAccountName, 
	strcat(
		@""metricNamespace('k4apps-metrics')""
		@"".metric('Replicas')""
		@"".dimensions("",dimension_list, "")""
		@"".samplingTypes('"",sampling,""')""
		@""| where containerAppArmId == '"", appArmId,""' ""
		@""| zoom Max = max("",sampling,"") by "", mdm_bucket
    ),
    startTime,
    endTime
);
union theSchema, mdmData
| project Timestamp = TimestampUtc, Revision = revisionName, Max, appArmId
| where Revision == cappRevisionName
| order by Timestamp asc, Revision asc;
";        
        return _kustoPlugin.ExecuteKustoQuery(region, query);
    }
}
