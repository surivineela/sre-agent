# Managed Resource Tracking in Knowledge Graph

## Overview

Add a nullable `managedResource` boolean property to graph nodes to distinguish resources within crawl roots (appear in tool listings) from discovered relationship targets (retained for topology but hidden from listings).

**Status**: Planning

---

## Problem Statement

When nodes are removed from crawl roots, they are not properly pruned from the knowledge graph. However, we cannot simply delete such nodes because:

1. The KG needs to retain nodes discovered through relationships (e.g., network connections, dependencies) even if they're not part of crawl roots
2. These discovered nodes are important for topology visualization and understanding application architecture
3. Deleting them would break graph traversals and relationship integrity

The issue is that these "unmanaged" nodes currently appear in tool calls that list resources, making it confusing for users who expect to see only the resources they've configured for monitoring.

---

## Solution Design

### Nullable `managedResource` Property

Add a nullable `bool? managedResource` property to graph nodes.

| Value | Meaning |
|-------|---------|
| `null` | Legacy/backcompat - treated as `true` in queries until crawler sets explicit value |
| `true` | Resource is within crawl root scope, actively managed, should appear in listings |
| `false` | Resource discovered through relationships, retained for topology but hidden from listings |

**Rationale**:
1. **Backward Compatibility**: Existing nodes without the property are treated as managed
2. **No Migration Required**: First crawl cycle will set explicit values
3. **Crawler Handles Reconciliation**: No separate background job needed - crawler continuously updates this property
4. **Clear Semantics**: Explicit true/false distinguishes intentional state from legacy data

### Distinction from `nonCrawled` Property

These are orthogonal concepts:

| Property | Meaning | Use Case |
|----------|---------|----------|
| `nonCrawled = true` | User-created entity, protected from automated cleanup | Source code repos, manual edges (e.g., `SERVES_CODE`, `DELEGATED_TO`) |
| `managedResource` | Within crawl root scope, should appear in resource listings | Any Azure/K8s resource node |

Example combinations:
- `SourceCodeRepoNode`: `nonCrawled = true` (user-created, not an Azure resource - doesn't have `managedResource`)
- Container App within crawl root: `managedResource = true` (crawled, should be listed)
- Redis cache discovered via connection: `managedResource = false` (discovered, retained for topology only)

---

## Implementation Steps

### Step 1: Create `AzureResourceNode` Base Class

**File**: `src/Agent/Agent.Data/DatabaseClients/GraphDbClient/Nodes/AzureResourceNode.cs` (new file)

Create an intermediate abstract class between `GraphNode` and the resource-specific nodes:

```csharp
public abstract class AzureResourceNode : GraphNode
{
    [GraphProperty("managedResource")]
    public bool? ManagedResource { get; set; }  // null = backcompat (treated as true in queries)
}
```

### Step 2: Update Node Inheritance Hierarchy

Update `ArmResourceNode` and `KubernetesResourceNode` to inherit from `AzureResourceNode`:

**File**: `src/Agent/Agent.Data/DatabaseClients/GraphDbClient/Nodes/ArmResourceNode.cs`
```csharp
public class ArmResourceNode : AzureResourceNode  // Changed from GraphNode
{
    // ... rest unchanged
}
```

**File**: `src/Agent/Agent.Data/DatabaseClients/GraphDbClient/Nodes/KubernetesResourceNode.cs`
```csharp
public class KubernetesResourceNode : AzureResourceNode  // Changed from GraphNode
{
    // ... rest unchanged
}
```

**Updated hierarchy:**

```
IResourceGraphNode (interface)
    │
    ▼
GraphNode (abstract)
    │
    ├── AzureResourceNode (abstract) ← NEW: has managedResource property
    │       │
    │       ├── ArmResourceNode (Azure ARM resources)
    │       │       ├── ContainerAppNode, AppServiceNode, AksNode, etc.
    │       │       ├── SubscriptionNode
    │       │       └── ResourceGroupNode
    │       │
    │       └── KubernetesResourceNode (K8s resources)
    │               └── KubernetesNamespacedResourceNode
    │
    ├── SourceCodeRepoNode (has nonCrawled=true, NOT an Azure resource)
    ├── ApicDependencyNode
    ├── PagerDutyIncidentNode
    └── AzMonitorAlertNode
```

**Why this structure?**
- Only Azure/K8s resources are governed by crawl roots
- `SourceCodeRepoNode`, `PagerDutyIncidentNode`, etc. are external entities not subject to managed/unmanaged semantics
- These non-Azure nodes use `nonCrawled = true` for cleanup protection instead

### Step 3: Update Crawlers to Set `managedResource = true`

**Files**: All resource crawlers in `src/Agent/Agent.Runtime/Crawler/`

When persisting nodes within crawl root scope, explicitly set `managedResource = true`:

```csharp
node.ManagedResource = true;
await _graphDbClient.AddOrUpdateNodeAsync(node);
```

When creating nodes for discovered relationships (resources outside crawl roots), set `managedResource = false`:

```csharp
discoveredNode.ManagedResource = false;
await _graphDbClient.AddOrUpdateNodeAsync(discoveredNode);
```

### Step 4: Update `IGraphDBPlugin` Listing Methods

**File**: `src/Agent/Agent.Data/DatabaseClients/GraphDbClient/GraphDBPlugin.cs`

Modify Gremlin queries to filter by `managedResource`. Use pattern that treats null as true for backward compatibility:

```gremlin
.or(__.not(__.has('managedResource')), __.has('managedResource', true))
```

**Methods to update**:
- `ListResourcesByTypeAsync`
- `SearchResourceAsync`
- `SearchResourceByNameAsync`
- `ListSubscriptionsAsync`
- `ListResourceGroupsAsync`
- `GetResourceCountAsync`
- `GetManagedResourcesInfoAsync`

### Step 5: Keep Topology/Visualization Methods Unchanged

**Methods that should NOT filter by `managedResource`** (traverse ALL nodes):
- `VisualizeApplicationComponents`
- `GetApplicationComponentsSummary`
- `VisualizeAKSMicroserviceTopology`
- `FindAllNetworkConnectedResources`
- `DiscoverApplications`

These methods need to show complete application topology including discovered dependencies outside crawl roots.

---

## GraphDBPluginDefinition Tool Behavior Summary

| Tool | Filter by `managedResource`? | Rationale |
|------|------------------------------|-----------|
| `ListResourcesByType` | ✅ Yes | User expects to see only managed resources |
| `SearchResource` | ✅ Yes | Search should return managed resources |
| `SearchResourceByName` | ✅ Yes | Search should return managed resources |
| `ListSubscriptions` | ✅ Yes | List subscriptions in crawl scope |
| `ListResourceGroups` | ✅ Yes | List resource groups in crawl scope |
| `GetResourceCount` | ✅ Yes | Count managed resources only |
| `GetManagedResourcesInfo` | ✅ Yes | Explicitly about managed resources |
| `VisualizeApplicationComponents` | ❌ No | Show full topology including dependencies |
| `GetApplicationComponentsSummary` | ❌ No | Show full topology including dependencies |
| `VisualizeAKSMicroserviceTopology` | ❌ No | Show full K8s topology |
| `FindAllNetworkConnectedResources` | ❌ No | Show all network connections |
| `DiscoverApplications` | ❌ No | Discover full application graph |
| `GetResourceBasicProperties` | ❌ No | Get properties of any known resource |
| `GetResourceDetailedProperties` | ❌ No | Get properties of any known resource |
| `GetResourceHealthInfo` | ❌ No | Get health of any known resource |
| `GetResourcePropertiesRealTime` | ❌ No | Real-time query, not KG listing |
| `Query` (generic) | ❌ No | Raw Gremlin, user controls filtering |

---

## Index Optimization

Add `managedResource` to CosmosDB/Gremlin index since it will be used in most listing queries.

---

## Testing Plan

1. **Unit Tests**: Verify Gremlin query generation includes `managedResource` filter
2. **Integration Tests**:
   - Create nodes with `managedResource = true/false/null`
   - Verify listing methods return only managed resources
   - Verify visualization methods return all resources
