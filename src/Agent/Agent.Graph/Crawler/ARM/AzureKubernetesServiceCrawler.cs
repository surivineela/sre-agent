// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Configuration;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.Core;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Agent.Graph.Crawler.ARM;

public class AzureKubernetesServiceCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<AzureKubernetesServiceCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmClient _armClient;
    private readonly IKubernetesService _k8sService;
    private readonly AzureResourceGraphClient _graphClient;

    public AzureKubernetesServiceCrawler(ILogger<AzureKubernetesServiceCrawler> logger, IGraphDatabaseClient graphDbClient, ILoggerFactory loggerFactory, ArmClient armClient, IKubernetesService k8sService, AzureResourceGraphClient graphClient)
        : base(logger, graphDbClient, armClient, false)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        _k8sService = k8sService;
        _graphClient = graphClient;
    }    
    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode clusterNode)
    {
        await foreach (var n in base.Crawl(clusterNode))
        {
            yield return n;
        }

        // Add the cluster node to the graph.
        await _graphDbClient.AddOrUpdateNodeAsync(clusterNode);

        var aksNode = (AksNode)clusterNode;
        _logger.LogDebug($"Crawling Kubernetes cluster: {aksNode.GetNodeId()}");

        // Find connected Azure Monitor workspaces using Azure Resource Graph
        // This query looks for Azure Monitor workspaces that are connected to this AKS cluster
        List<AzureMonitorWorkspaceNode> monitorWorkspaces = new List<AzureMonitorWorkspaceNode>();
        try
        {
            monitorWorkspaces = await FindConnectedMonitorWorkspaces(aksNode);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to find connected Azure Monitor workspaces for {aksNode.ResourceId}: {ex.Message}");
        }
        
        foreach (var workspaceNode in monitorWorkspaces)
        {
            _logger.LogDebug($"Found Azure Monitor workspace: {workspaceNode.ResourceName} connected to cluster: {aksNode.GetNodeId()}");
            await _graphDbClient.AddOrUpdateNodeAsync(workspaceNode);
            var edge = new ArmResourceEdge(aksNode.GetNodeId(), workspaceNode.GetNodeId(), Constants.Relationships.MonitoredBy);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            yield return workspaceNode;
        }

        var namespaces = await _k8sService.GetNamespacesAsync(aksNode.ResourceId);
        _logger.LogDebug($"Found {namespaces.Items?.Count} namespaces in cluster: {aksNode.GetNodeId()}");
        foreach (var ns in namespaces)
        {
            _logger.LogDebug($"Namespace: {ns.Name()} in cluster: {aksNode.GetNodeId()}");
            // TODO: GVK are nulls
            var nsNode = new KubernetesResourceNode(ns, aksNode.ResourceId, aksNode.SubscriptionId, aksNode.ResourceGroupName, aksNode.Location, ns.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesNamespaceType, ns.Annotations(), ns.Labels());
            await _graphDbClient.AddOrUpdateNodeAsync(nsNode);
            var edge = new ArmResourceEdge(clusterNode.GetNodeId(), nsNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            yield return nsNode;
        }

        // non-namespaced resources
        // nodes
        var nodes = await _k8sService.GetNodesAsync(aksNode.ResourceId);
        foreach (var node in nodes)
        {
            _logger.LogDebug($"Node: {node.Name()} in cluster: {aksNode.GetNodeId()}");
            var nodeNode = new KubernetesResourceNode(node, aksNode.ResourceId, aksNode.SubscriptionId, aksNode.ResourceGroupName, aksNode.Location, node.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesNodeType, node.Annotations(), node.Labels());
            await _graphDbClient.AddOrUpdateNodeAsync(nodeNode);
            var edge = new ArmResourceEdge(clusterNode.GetNodeId(), nodeNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            yield return nodeNode;
        }

        // pvcs
        // list all pvs
        var persistentVolumes = await _k8sService.GetPersistentVolumesAsync(aksNode.ResourceId);
        _logger.LogDebug($"Found {persistentVolumes.Items?.Count} persistent volumes in cluster: {aksNode.GetNodeId()}");
        foreach (var pv in persistentVolumes.Items)
        {
            _logger.LogDebug($"PersistentVolume: {pv.Name()} in cluster: {aksNode.GetNodeId()}");
            var pvNode = new KubernetesResourceNode(pv, aksNode.ResourceId, aksNode.SubscriptionId, aksNode.ResourceGroupName, aksNode.Location, pv.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesPersistentVolumeType, pv.Annotations(), pv.Labels());
            await _graphDbClient.AddOrUpdateNodeAsync(pvNode);
            var edge = new ArmResourceEdge(clusterNode.GetNodeId(), pvNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            yield return pvNode;
        }
    }    
    
    /// <summary>
    /// Find Azure Monitor workspaces connected to an AKS cluster
    /// </summary>
    /// <param name="aksNode">The AKS node to check for connected monitor workspaces</param>
    /// <returns>List of AzureMonitorWorkspaceNode instances that are connected to this AKS cluster</returns>
    private async Task<List<AzureMonitorWorkspaceNode>> FindConnectedMonitorWorkspaces(AksNode aksNode)
    {
        List<AzureMonitorWorkspaceNode> workspaces = new List<AzureMonitorWorkspaceNode>();        // This query identifies Azure Monitor workspaces connected to an AKS cluster through data collection rules
        string query = $@"
        resources        
        | where type == ""microsoft.insights/datacollectionrules""        
        | extend ma = properties.destinations.monitoringAccounts        
        | extend flows = properties.dataFlows        
        | mv-expand flows        
        | where flows.streams contains ""Microsoft-PrometheusMetrics""        
        | mv-expand ma        
        | where array_index_of(flows.destinations, tostring(ma.name)) != -1        
        | project dcrId = tolower(id), azureMonitorWorkspaceResourceId=tolower(tostring(ma.accountResourceId))        
        | join (
            insightsresources           
            | extend clusterId = split(tolower(id), '/providers/microsoft.insights/datacollectionruleassociations/')[0]         
            | where clusterId =~ ""{aksNode.ResourceId.ToLowerInvariant()}""
            | project clusterId = tostring(clusterId), dcrId = tolower(tostring(parse_json(properties).dataCollectionRuleId)), dcraName = name 
            ) on dcrId        
        | join kind=leftouter (
            resources 
            | where type == ""microsoft.monitor/accounts""        
            | extend prometheusQueryEndpoint=tostring(properties.metrics.prometheusQueryEndpoint) 
            | extend amwLocation = location 
            | project azureMonitorWorkspaceResourceId=tolower(id), prometheusQueryEndpoint, amwLocation) on azureMonitorWorkspaceResourceId        
        | project-away dcrId1, azureMonitorWorkspaceResourceId1         
        | join kind=leftouter (
            resources    
            | where type == ""microsoft.dashboard/grafana""    
            | extend amwIntegrations = properties.grafanaIntegrations.azureMonitorWorkspaceIntegrations    
            | mv-expand amwIntegrations    
            | extend azureMonitorWorkspaceResourceId = tolower(tostring(amwIntegrations.azureMonitorWorkspaceResourceId))    
            | where azureMonitorWorkspaceResourceId != """"    
            | extend grafanaObject = pack(""grafanaResourceId"", tolower(id), ""grafanaWorkspaceName"", name, ""grafanaEndpoint"", properties.endpoint)    
            | summarize associatedGrafanas=make_list(grafanaObject) by azureMonitorWorkspaceResourceId) on azureMonitorWorkspaceResourceId    
        | extend amwToGrafana = pack(""azureMonitorWorkspaceResourceId"", azureMonitorWorkspaceResourceId, ""prometheusQueryEndpoint"", prometheusQueryEndpoint, ""amwLocation"", amwLocation, ""associatedGrafanas"", associatedGrafanas)   
        | summarize amwToGrafanas=make_list(amwToGrafana) by dcrResourceId = dcrId, dcraName
        | order by dcrResourceId";
        
        try
        {
            // We search in the cluster's subscription
            var subscriptions = new List<string> { aksNode.SubscriptionId };
            var queryResult = await _graphClient.Query(subscriptions, query);
            
            if (queryResult != null && queryResult.Data != null)
            {
                var jsonData = queryResult.Data.ToString();
                var results = JsonDocument.Parse(jsonData).RootElement;
                
                if (results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in results.EnumerateArray())
                    {
                        // Get the list of Azure Monitor workspaces from the amwToGrafanas array
                        if (item.TryGetProperty("amwToGrafanas", out var amwToGrafanas) && 
                            amwToGrafanas.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var amw in amwToGrafanas.EnumerateArray())
                            {
                                if (amw.TryGetProperty("azureMonitorWorkspaceResourceId", out var resourceIdElement) &&
                                    amw.TryGetProperty("prometheusQueryEndpoint", out var prometheusEndpointElement) &&
                                    amw.TryGetProperty("amwLocation", out var locationElement))
                                {
                                    string resourceId = resourceIdElement.GetString();
                                    string prometheusQueryEndpoint = prometheusEndpointElement.ValueKind != JsonValueKind.Null ? 
                                        prometheusEndpointElement.GetString() : null;
                                    string location = locationElement.ValueKind != JsonValueKind.Null ? 
                                        locationElement.GetString() : null;
                                    
                                    // Parse the resource ID to extract necessary components
                                    var resourceIdentifier = new ResourceIdentifier(resourceId);
                                    string subscriptionId = resourceIdentifier.SubscriptionId;
                                    string resourceGroupName = resourceIdentifier.ResourceGroupName;
                                    string resourceName = resourceIdentifier.Name;
                                    
                                    _logger.LogDebug($"Found Azure Monitor workspace: {resourceName} with Prometheus endpoint: {prometheusQueryEndpoint}");
                                    
                                    var workspaceNode = new AzureMonitorWorkspaceNode(
                                        Constants.AzureMonitorWorkspaceType,
                                        resourceId,
                                        subscriptionId,
                                        resourceGroupName,
                                        resourceName,
                                        prometheusQueryEndpoint,
                                        location
                                    );
                                    
                                    workspaces.Add(workspaceNode);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Error querying Azure Resource Graph for Monitor workspaces: {ex.Message}");
            throw;
        }
        
        return workspaces;
    }
}

