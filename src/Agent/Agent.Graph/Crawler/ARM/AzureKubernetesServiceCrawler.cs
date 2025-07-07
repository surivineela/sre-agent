// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerService;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

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
        _logger.LogInternalInformation($"Crawling Kubernetes cluster: {aksNode.GetNodeId()}");

        // Get AKS cluster details to extract network, identity, and disk information
        List<GraphNode> extractedNodes = new List<GraphNode>();
        try
        {
            var aksResourceId = new ResourceIdentifier(aksNode.ResourceId);
            var aksResource = _armClient.GetContainerServiceManagedClusterResource(aksResourceId);
            var cluster = await aksResource.GetAsync();

            if (cluster != null && cluster.Value.HasData)
            {
                var clusterData = cluster.Value.Data;

                // Extract and create network connections (VNet and Subnet)
                await foreach (var networkNode in ExtractNetworkConnections(aksNode, clusterData))
                {
                    extractedNodes.Add(networkNode);
                }

                // Extract and create identity connections
                await foreach (var identityNode in ExtractIdentityConnections(aksNode, clusterData))
                {
                    extractedNodes.Add(identityNode);
                }

                // Extract and create disk connections from agent pools
                await foreach (var diskNode in ExtractDiskConnections(aksNode, clusterData))
                {
                    extractedNodes.Add(diskNode);
                }

                // Extract all infrastructure resources from node resource group (VMSS, LB, PIP, NSG, etc.)
                await foreach (var infraNode in ExtractNodeResourceGroupResources(aksNode, clusterData))
                {
                    extractedNodes.Add(infraNode);
                }

                // Extract container registry connections
                await foreach (var acrNode in ExtractContainerRegistryConnections(aksNode, clusterData))
                {
                    extractedNodes.Add(acrNode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to get AKS cluster details for {aksNode.ResourceId}: {ex.Message}");
        }

        // Yield all extracted nodes
        foreach (var node in extractedNodes)
        {
            yield return node;
        }

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
        var nodes = new V1NodeList([]);
        try
        {
            nodes = await _k8sService.GetNodesAsync(aksNode.ResourceId);
            _logger.LogDebug($"Found {nodes.Items?.Count} nodes in cluster: {aksNode.GetNodeId()}");
        }
        // Azure Kubernetes Service RBAC Reader role does not have permission to list nodes
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogInternalWarning($"No permission to list nodes under cluster {aksNode.ResourceId}");
        }

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
        var persistentVolumes = new V1PersistentVolumeList([]);
        try
        {
            persistentVolumes = await _k8sService.GetPersistentVolumesAsync(aksNode.ResourceId);
            _logger.LogDebug($"Found {persistentVolumes.Items?.Count} persistent volumes in cluster: {aksNode.GetNodeId()}");
        }
        // Azure Kubernetes Service RBAC Reader role does not have permission to list persistent volumes
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogInternalWarning($"No permission to list persistent volumes under cluster {aksNode.ResourceId}");
        }

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

    /// <summary>
    /// Extract network connections (VNet and Subnet) from AKS cluster configuration
    /// </summary>
    private async IAsyncEnumerable<GraphNode> ExtractNetworkConnections(AksNode aksNode, ContainerServiceManagedClusterData clusterData)
    {
        if (clusterData.NetworkProfile?.NetworkPlugin != null)
        {
            foreach (var agentPool in clusterData.AgentPoolProfiles)
            {
                if (agentPool.VnetSubnetId != null)
                {
                    var subnetResourceId = new ResourceIdentifier(agentPool.VnetSubnetId);

                    // Extract VNet resource ID from subnet ID
                    // Subnet ID format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{subnet}
                    var vnetResourceId = subnetResourceId.Parent;

                    // Create VNet node
                    var vnetNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(vnetResourceId.ToString());
                    if (vnetNode != null)
                    {
                        await _graphDbClient.AddOrUpdateNodeAsync(vnetNode);
                        var vnetEdge = new ArmResourceEdge(aksNode.GetNodeId(), vnetNode.GetNodeId(), Constants.Relationships.Connected);
                        vnetEdge.AddOrUpdateEdgeProperty("connectionType", "network");
                        await _graphDbClient.AddOrUpdateEdgeAsync(vnetEdge);
                        _logger.LogDebug($"Connected AKS cluster {aksNode.ResourceName} to VNet {vnetNode.ResourceName}");
                        yield return vnetNode;
                    }

                    // Create Subnet node
                    var subnetNode = new ArmResourceNode(
                        Constants.VirtualNetworkType,
                        agentPool.VnetSubnetId,
                        subnetResourceId.SubscriptionId,
                        subnetResourceId.ResourceGroupName,
                        subnetResourceId.Name,
                        aksNode.Location);

                    await _graphDbClient.AddOrUpdateNodeAsync(subnetNode);
                    var subnetEdge = new ArmResourceEdge(aksNode.GetNodeId(), subnetNode.GetNodeId(), Constants.Relationships.Connected);
                    subnetEdge.AddOrUpdateEdgeProperty("connectionType", "network");
                    subnetEdge.AddOrUpdateEdgeProperty("agentPool", agentPool.Name);
                    await _graphDbClient.AddOrUpdateEdgeAsync(subnetEdge);
                    _logger.LogDebug($"Connected AKS cluster {aksNode.ResourceName} to Subnet {subnetNode.ResourceName} for agent pool {agentPool.Name}");
                    yield return subnetNode;
                }
            }
        }
    }

    /// <summary>
    /// Extract identity connections from AKS cluster configuration
    /// </summary>
    private async IAsyncEnumerable<GraphNode> ExtractIdentityConnections(AksNode aksNode, ContainerServiceManagedClusterData clusterData)
    {
        // Handle system-assigned identity
        if (clusterData.Identity?.ManagedServiceIdentityType == Azure.ResourceManager.Models.ManagedServiceIdentityType.SystemAssigned ||
            clusterData.Identity?.ManagedServiceIdentityType == Azure.ResourceManager.Models.ManagedServiceIdentityType.SystemAssignedUserAssigned)
        {
            if (clusterData.Identity.PrincipalId != null)
            {
                var systemIdentityNode = new ManagedIdentityNode(
                    ManagedIdentityNode.SystemAssignedManagedIdentityType,
                    aksNode.ResourceId, // System MI uses the parent resource ID
                    aksNode.SubscriptionId,
                    aksNode.ResourceGroupName,
                    aksNode.ResourceName + "-system",
                    aksNode.Location);

                systemIdentityNode.PrincipalId = clusterData.Identity.PrincipalId.ToString();
                systemIdentityNode.TenantId = clusterData.Identity.TenantId?.ToString();

                await _graphDbClient.AddOrUpdateNodeAsync(systemIdentityNode);
                var edge = new ArmResourceEdge(aksNode.GetNodeId(), systemIdentityNode.GetNodeId(), Constants.Relationships.HasIdentity);
                edge.AddOrUpdateEdgeProperty("identityType", "system-assigned");
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                _logger.LogDebug($"Connected AKS cluster {aksNode.ResourceName} to system-assigned identity");
                yield return systemIdentityNode;
            }
        }

        // Handle user-assigned identities
        if (clusterData.Identity?.UserAssignedIdentities != null)
        {
            foreach (var identity in clusterData.Identity.UserAssignedIdentities)
            {
                var identityResourceId = identity.Key;
                var identityNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(identityResourceId.ToString());
                if (identityNode != null)
                {
                    await _graphDbClient.AddOrUpdateNodeAsync(identityNode);
                    var edge = new ArmResourceEdge(aksNode.GetNodeId(), identityNode.GetNodeId(), Constants.Relationships.HasIdentity);
                    edge.AddOrUpdateEdgeProperty("identityType", "user-assigned");
                    edge.AddOrUpdateEdgeProperty("clientId", identity.Value.ClientId?.ToString());
                    edge.AddOrUpdateEdgeProperty("principalId", identity.Value.PrincipalId?.ToString());
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    _logger.LogDebug($"Connected AKS cluster {aksNode.ResourceName} to user-assigned identity {identityNode.ResourceName}");
                    yield return identityNode;
                }
            }
        }
    }

    /// <summary>
    /// Extract disk connections from AKS agent pools
    /// </summary>
    private async IAsyncEnumerable<GraphNode> ExtractDiskConnections(AksNode aksNode, ContainerServiceManagedClusterData clusterData)
    {
        // Query for managed disks attached to VMs in the node resource group
        var nodeResourceGroup = clusterData.NodeResourceGroup;
        if (string.IsNullOrEmpty(nodeResourceGroup))
        {
            yield break;
        }

        List<GraphNode> diskNodes = new List<GraphNode>();

        try
        {
            var diskQuery = $@"
            resources
            | where type == ""microsoft.compute/disks""
            | where resourceGroup =~ ""{nodeResourceGroup}""
            | project id, name, location, properties";

            var diskResults = await _graphClient.Query(new[] { aksNode.SubscriptionId }, diskQuery);

            if (diskResults != null && diskResults.Data != null)
            {
                var jsonData = diskResults.Data.ToString();
                var results = JsonDocument.Parse(jsonData).RootElement;

                if (results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var disk in results.EnumerateArray())
                    {
                        if (disk.TryGetProperty("id", out var diskIdElement))
                        {
                            var diskResourceId = diskIdElement.GetString();
                            var diskNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(diskResourceId);

                            if (diskNode != null)
                            {
                                await _graphDbClient.AddOrUpdateNodeAsync(diskNode);
                                var edge = new ArmResourceEdge(aksNode.GetNodeId(), diskNode.GetNodeId(), Constants.Relationships.Uses);
                                edge.AddOrUpdateEdgeProperty("resourceType", "disk");
                                edge.AddOrUpdateEdgeProperty("nodeResourceGroup", nodeResourceGroup);
                                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                                _logger.LogDebug($"Connected AKS cluster {aksNode.ResourceName} to disk {diskNode.ResourceName}");
                                diskNodes.Add(diskNode);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to query disks for AKS cluster {aksNode.ResourceName}: {ex.Message}");
        }

        foreach (var node in diskNodes)
        {
            yield return node;
        }
    }

    /// <summary>
    /// Extract all infrastructure resources from the node resource group
    /// </summary>
    private async IAsyncEnumerable<GraphNode> ExtractNodeResourceGroupResources(AksNode aksNode, ContainerServiceManagedClusterData clusterData)
    {
        var nodeResourceGroup = clusterData.NodeResourceGroup;
        if (string.IsNullOrEmpty(nodeResourceGroup))
        {
            _logger.LogDebug($"No node resource group found for AKS cluster {aksNode.ResourceName}");
            yield break;
        }

        _logger.LogDebug($"Extracting resources from node resource group: {nodeResourceGroup}");
        List<GraphNode> resourceNodes = new List<GraphNode>();

        try
        {
            // Query for all relevant resources in the node resource group
            var resourceQuery = $@"
            resources
            | where resourceGroup =~ ""{nodeResourceGroup}""
            | where type in~ (
                ""microsoft.compute/virtualmachinescalesets"",
                ""microsoft.network/loadbalancers"", 
                ""microsoft.network/publicipaddresses"",
                ""microsoft.network/networksecuritygroups"",
                ""microsoft.network/routetables"",
                ""microsoft.storage/storageaccounts""
            )
            | project id, name, type, location, properties";

            var queryResults = await _graphClient.Query(new[] { aksNode.SubscriptionId }, resourceQuery);

            if (queryResults != null && queryResults.Data != null)
            {
                var jsonData = queryResults.Data.ToString();
                var results = JsonDocument.Parse(jsonData).RootElement;

                if (results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var resource in results.EnumerateArray())
                    {
                        if (resource.TryGetProperty("id", out var resourceIdElement) &&
                            resource.TryGetProperty("type", out var typeElement))
                        {
                            var resourceId = resourceIdElement.GetString();
                            var resourceType = typeElement.GetString().ToLowerInvariant();

                            var resourceNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(resourceId);
                            if (resourceNode != null)
                            {
                                await _graphDbClient.AddOrUpdateNodeAsync(resourceNode);

                                // Create appropriate relationship based on resource type
                                var relationshipType = DetermineRelationshipType(resourceType);
                                var edge = new ArmResourceEdge(aksNode.GetNodeId(), resourceNode.GetNodeId(), relationshipType);
                                edge.AddOrUpdateEdgeProperty("nodeResourceGroup", nodeResourceGroup);
                                edge.AddOrUpdateEdgeProperty("managedBy", "aks");

                                // Add specific metadata based on resource type
                                if (resourceType.Contains("virtualmachinescalesets"))
                                {
                                    edge.AddOrUpdateEdgeProperty("resourceRole", "nodePool");
                                    // Try to extract agent pool name from VMSS name
                                    var vmssName = resource.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "";
                                    if (!string.IsNullOrEmpty(vmssName))
                                    {
                                        // AKS VMSS names typically follow pattern: aks-{poolname}-{id}-vmss
                                        var parts = vmssName.Split('-');
                                        if (parts.Length >= 2)
                                        {
                                            edge.AddOrUpdateEdgeProperty("agentPoolName", parts[1]);
                                        }
                                    }
                                }
                                else if (resourceType.Contains("loadbalancers"))
                                {
                                    edge.AddOrUpdateEdgeProperty("resourceRole", "networking");
                                }
                                else if (resourceType.Contains("publicipaddresses"))
                                {
                                    edge.AddOrUpdateEdgeProperty("resourceRole", "networking");
                                }
                                else if (resourceType.Contains("networksecuritygroups"))
                                {
                                    edge.AddOrUpdateEdgeProperty("resourceRole", "security");
                                }
                                else if (resourceType.Contains("routetables"))
                                {
                                    edge.AddOrUpdateEdgeProperty("resourceRole", "networking");
                                }
                                else if (resourceType.Contains("storageaccounts"))
                                {
                                    edge.AddOrUpdateEdgeProperty("resourceRole", "diagnostics");
                                }

                                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                                _logger.LogDebug($"Connected AKS cluster {aksNode.ResourceName} to {resourceType}: {resourceNode.ResourceName}");
                                resourceNodes.Add(resourceNode);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to query node resource group resources for AKS cluster {aksNode.ResourceName}: {ex.Message}");
        }

        foreach (var node in resourceNodes)
        {
            yield return node;
        }
    }

    /// <summary>
    /// Extract container registry connections
    /// </summary>     
    private async IAsyncEnumerable<GraphNode> ExtractContainerRegistryConnections(AksNode aksNode, ContainerServiceManagedClusterData clusterData)
    {
        // Check if there are any configured container registries in the cluster
        if (clusterData.ServicePrincipalProfile?.ClientId == null && clusterData.Identity == null)
        {
            yield break;
        }

        List<GraphNode> acrNodes = new List<GraphNode>();

        try
        {
            // Query for container registries that might be connected via role assignments
            var acrQuery = $@"
            resources
            | where type == ""microsoft.containerregistry/registries""
            | where subscriptionId == ""{aksNode.SubscriptionId}""
            | project id, name, location";

            var acrResults = await _graphClient.Query(new[] { aksNode.SubscriptionId }, acrQuery);

            if (acrResults != null && acrResults.Data != null)
            {
                var jsonData = acrResults.Data.ToString();
                var results = JsonDocument.Parse(jsonData).RootElement;

                if (results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var acr in results.EnumerateArray())
                    {
                        if (acr.TryGetProperty("id", out var acrIdElement))
                        {
                            var acrResourceId = acrIdElement.GetString();

                            // Check if this ACR has role assignments from the AKS identity
                            bool hasConnection = await CheckACRConnection(aksNode, clusterData, acrResourceId);

                            if (hasConnection)
                            {
                                var acrNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(acrResourceId);
                                if (acrNode != null)
                                {
                                    await _graphDbClient.AddOrUpdateNodeAsync(acrNode);
                                    var edge = new ArmResourceEdge(aksNode.GetNodeId(), acrNode.GetNodeId(), Constants.Relationships.PullsFrom);
                                    edge.AddOrUpdateEdgeProperty("connectionType", "containerRegistry");
                                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                                    _logger.LogDebug($"Connected AKS cluster {aksNode.ResourceName} to ACR {acrNode.ResourceName}");
                                    acrNodes.Add(acrNode);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to query container registries for AKS cluster {aksNode.ResourceName}: {ex.Message}");
        }

        foreach (var node in acrNodes)
        {
            yield return node;
        }
    }

    /// <summary>
    /// Check if AKS has access to the specified ACR
    /// </summary>
    private async Task<bool> CheckACRConnection(AksNode aksNode, ContainerServiceManagedClusterData clusterData, string acrResourceId)
    {
        try
        {
            var principalIds = new List<string>();

            // Collect all possible principal IDs that might have ACR access
            if (clusterData.Identity?.PrincipalId != null)
            {
                principalIds.Add(clusterData.Identity.PrincipalId.ToString());
            }

            if (clusterData.IdentityProfile != null && clusterData.IdentityProfile.ContainsKey("kubeletidentity"))
            {
                var kubeletIdentity = clusterData.IdentityProfile["kubeletidentity"];
                if (kubeletIdentity.ObjectId != null)
                {
                    principalIds.Add(kubeletIdentity.ObjectId.ToString());
                }
            }

            if (principalIds.Count == 0)
            {
                return false;
            }

            // Query for role assignments
            var roleQuery = $@"
            authorizationresources
            | where type == ""microsoft.authorization/roleassignments""
            | where properties.scope =~ ""{acrResourceId}""
            | where properties.principalId in~ ({string.Join(",", principalIds.Select(p => $"'{p}'"))})
            | project principalId = properties.principalId";

            var roleResults = await _graphClient.Query(new[] { aksNode.SubscriptionId }, roleQuery);
            return roleResults != null && roleResults.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determine the appropriate relationship type based on resource type
    /// </summary>
    private string DetermineRelationshipType(string resourceType)
    {
        if (resourceType.Contains("virtualmachinescalesets"))
            return Constants.Relationships.Manages;
        if (resourceType.Contains("loadbalancers") || resourceType.Contains("publicip"))
            return Constants.Relationships.Uses;
        if (resourceType.Contains("networksecuritygroups"))
            return Constants.Relationships.Linked;
        if (resourceType.Contains("routetables"))
            return Constants.Relationships.Linked;
        if (resourceType.Contains("storage"))
            return Constants.Relationships.StoresIn;

        return Constants.Relationships.Uses;
    }
}
