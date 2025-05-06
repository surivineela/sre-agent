// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Interfaces;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.AI;
using static Microsoft.Graph.Constants;

namespace FirstPartyAgent.Core.Plugins.Implementation;

// [MENDATORY]
public class ContainerAppRevisionPlugin : IContainerAppRevisionPlugin
{
    private readonly IKustoPlugin _kustoPlugin;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
    public ContainerAppRevisionPlugin(IKustoPlugin kustoPlugin, IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
        _kustoPlugin = kustoPlugin;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
    }

    public async Task<string> ListRevisions(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("ListRevisions", region,
        new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
        });
    }

    public async Task<string> GetActiveRevisionSessions(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetActiveRevisionSessions", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public async Task<string> GetHpaHeartbeatMetrics(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetHpaHeartbeatMetrics", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public async Task<string> GetRevisionSpecChanges(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetRevisionSpecChanges", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public async Task<string> GetEventProcessorEventsWithoutReplica(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorEventsWithoutReplica", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public async Task<string> GetPodHeartbeatStatus(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHeartbeatStatus", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "revisionName", revisionName },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public async Task<string> GetInternalEventProcessorEventsForPod(string region, DateTime fromDate, DateTime toDate, string revisionName, string podName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetInternalEventProcessorEventsForPod", region,
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


    public async Task<string> GetRevisionTrafficWithReplicaCount(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetRevisionReplicaAndTraffic", region,
            new Dictionary<string, string> {
        { "fromDate", fromDate.ToString() },
        { "toDate", toDate.ToString() },
        { "revisionName", revisionName },
        { "containerAppName", containerAppName },
        { "resourceGroupName", resourceGroupName },
        { "subscriptionId", subscriptionId },});
    }

    public async Task<string> ContainerAppRevisionStatus(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetRevisionStatus", region,
            new Dictionary<string, string> {
        { "fromDate", fromDate.ToString() },
        { "toDate", toDate.ToString() },
        { "revisionName", revisionName },
        { "containerAppName", containerAppName },
        { "resourceGroupName", resourceGroupName },
        { "subscriptionId", subscriptionId },});
    }

    public async Task<string> GetHttpScalerEventsForContainerApp(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetHttpScalerEventsForContainerApp", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });
    }

    public async Task<string> GetASIPageForRevision(string region, DateTime fromDate, DateTime toDate, string containerAppName,string revisionName, string resourceGroupName, string subscriptionId)
    {
        
        var clusterName= await _kustoPlugin.ExecuteFunctionAsync("GetManagedClusterName", region,
            new Dictionary<string, string> {
            { "containerAppNameParam", containerAppName },
            { "resourceGroupParam", resourceGroupName },
            { "subscriptionParam", subscriptionId }
            });

        return $"https://asi.azure.ms/services/ACA%20Azure%20Container%20Apps/pages/Container%20App%20Revision?EnvironmentName={clusterName}&Name={revisionName}&globalFrom={fromDate}&globalTo={toDate}";

    }


    public async Task<string> GetKedaOperatorEventsForContainerApp(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
       
        // await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, new ChatMessage(ChatRole.Tool, $"Performing Kusto Query `{functionName}`"));
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetKedaOperatorEventsForContainerApp", region,
            new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "containerAppName", containerAppName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
            });

       

    }

    public async Task<string> GetReplicaCount(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
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
        return await _kustoPlugin.ExecuteKustoQuery(region, query);
    }
}
