# UX Plan: Merge TsgCrawler (PAT) Connectors with ARM Connectors List

## Current Code Analysis

### File 1: useAgentConnectors.ts

**Location:** `src/Agent/Agent.Web/Client/src/src/Space/Settings/Hooks/useAgentConnectors.ts`

**What it does:**
- Fetches connectors from **ARM only** via `SreAgentClient.listDataConnectors()`
- Enriches with secrets via `SreAgentClient.listConnectorsSecrets()`
- Fetches status for each connector via `ExtendedAgentClient.getConnectorStatus()`
- Provides `putConnector`, `deleteConnector` that call ARM API

**Key state:**
```typescript
const [connectors, setConnectors] = useState<Connector[]>([]);  // Only ARM connectors
const [connectionMap, setConnectionMap] = useState<Record<string, ConnectorStatus>>({});
```

**The gap:** Never calls dataplane for TsgCrawler PAT connectors.

---

### File 2: useAdoConnectorDataplane.ts

**Location:** `src/Agent/Agent.Web/Client/src/src/Space/Settings/Connectors/Hooks/useAdoConnectorDataplane.ts`

**What it does:**
- `createConnector()` → POST to dataplane
- `testConnectivity()` → POST test to dataplane  
- `deleteConnector()` → DELETE to dataplane

**Current interface (missing GET):**
```typescript
export interface TsgConnectorResponse {
    name: string;
    dataSource: string;
    authMode: 'managedIdentity' | 'pat';
    hasCredentials: boolean;
    status: string;
    lastValidated?: string;
    // Missing: cloneStatus, lastSuccessfulSync, localPath, latestCommit
}
```

**The gap:** No `getConnectors()` method even though backend supports `GET /api/v1/connectors/tsgcrawler`.

---

### File 3: Connector interface

**Location:** `src/Agent/Agent.Web/Client/src/src/Common/Contracts/Azure/SreAgent.ts`

```typescript
export interface Connector {
    name: string;
    dataConnectorType: string;
    dataSource?: string;
    extendedProperties?: Record<string, any>;
    keyVaultUri?: string;
    identity: string;
    source?: string;  // Can use this to mark "dataplane" vs "arm"
}
```

---

### File 4: Connectors.tsx

**Location:** `src/Agent/Agent.Web/Client/src/src/Space/Settings/Connectors/Connectors.tsx`

**Delete flow:**
- Uses `deleteConnector` from `useAgentConnectors` (ARM API)
- Calls `deleteConnector(connectorName)` for each selected

**The gap:** Doesn't know to use dataplane delete for PAT connectors.

---

## Implementation Plan

### Step 1: Update `TsgConnectorResponse` Interface

**File:** `useAdoConnectorDataplane.ts`

Add missing fields from backend:

```typescript
export interface TsgConnectorResponse {
    name: string;
    dataSource: string;
    authMode: 'managedIdentity' | 'pat';
    hasCredentials: boolean;
    status: string;
    lastValidated?: string;
    errorMessage?: string;        // NEW
    cloneStatus: string;          // NEW: NotStarted, Cloning, Syncing, Ready, Failed
    lastSuccessfulSync?: string;  // NEW
    localPath?: string;           // NEW
    latestCommit?: string;        // NEW
}
```

---

### Step 2: Add `getConnectors()` Method

**File:** `useAdoConnectorDataplane.ts`

Add new callback:

```typescript
const getConnectors = useCallback(
    async (): Promise<{ isSuccessful: boolean; data?: TsgConnectorResponse[]; error?: string }> => {
        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/connectors/tsgcrawler`, {
                method: 'GET',
                headers: getAgentHeaders(),
            });

            if (response.ok) {
                const data: TsgConnectorResponse[] = await response.json();
                return { isSuccessful: true, data };
            } else {
                return { isSuccessful: false, error: `Failed with status ${response.status}` };
            }
        } catch (error) {
            return { isSuccessful: false, error: error instanceof Error ? error.message : 'Unknown error' };
        }
    },
    [sreAgentEndpoint, log]
);
```

Update interface and return:
```typescript
export interface UseAdoConnectorDataplaneResult {
    getConnectors: () => Promise<{ isSuccessful: boolean; data?: TsgConnectorResponse[]; error?: string }>;  // NEW
    createConnector: ...
    testConnectivity: ...
    deleteConnector: ...
}

return { getConnectors, createConnector, testConnectivity, deleteConnector };
```

---

### Step 3: Update `useAgentConnectors.ts` to Merge Sources

**File:** `useAgentConnectors.ts`

#### 3a. Import the dataplane hook functions directly (or inline fetch)

Since hooks can't be called conditionally, we'll inline the fetch or create a non-hook helper:

```typescript
// Add helper function at top of file (not a hook)
const fetchTsgConnectors = async (sreAgentEndpoint: string): Promise<TsgConnectorResponse[]> => {
    try {
        const response = await fetch(`${sreAgentEndpoint}/api/v1/connectors/tsgcrawler`, {
            method: 'GET',
            headers: getAgentHeaders(),
        });
        if (response.ok) {
            return await response.json();
        }
    } catch (e) {
        console.error('Failed to fetch TSG connectors:', e);
    }
    return [];
};
```

#### 3b. Modify `getConnectors()` to merge both sources

```typescript
const getConnectors = useCallback(async () => {
    setIsConnectorsLoading(true);
    setConnectorsUpdateFailure('');

    // Fetch from both sources in parallel
    const armPromise = SreAgentClient.listDataConnectors(agentResourceId);
    const armSecretsPromise = SreAgentClient.listConnectorsSecrets(agentResourceId);
    const tsgPromise = sreAgentEndpoint ? fetchTsgConnectors(sreAgentEndpoint) : Promise.resolve([]);

    const [armResponse, armSecretsResponse, tsgConnectors] = await Promise.all([
        armPromise,
        armSecretsPromise,
        tsgPromise
    ]);

    let connectorsArray: Connector[] = [];

    // Process ARM connectors (existing logic)
    if (armResponse?.metadata?.success && armResponse.data) {
        connectorsArray = armResponse.data.value.map(armObj => armObj.properties);
        
        // Enrich with secrets
        if (armSecretsResponse?.metadata?.success && armSecretsResponse.data) {
            const secretsArray = armSecretsResponse.data.value.map(armObj => armObj.properties);
            connectorsArray.forEach(connector => {
                const matchingSecret = secretsArray.find(dc => dc.name === connector.name);
                if (matchingSecret) {
                    connector.dataSource = matchingSecret.dataSource;
                }
            });
        }
    }

    // Convert TSG dataplane connectors to Connector format and merge
    const tsgAsConnectors: Connector[] = tsgConnectors.map(tsg => ({
        name: tsg.name,
        dataConnectorType: 'TsgCrawler',
        dataSource: tsg.dataSource,
        identity: 'pat',  // Indicates PAT auth
        source: 'dataplane',  // Mark as dataplane-sourced
        extendedProperties: {
            authMode: tsg.authMode,
            hasCredentials: tsg.hasCredentials,
            cloneStatus: tsg.cloneStatus,
            lastSuccessfulSync: tsg.lastSuccessfulSync,
            localPath: tsg.localPath,
            latestCommit: tsg.latestCommit,
        }
    }));

    // Merge: ARM connectors + TSG dataplane connectors (dedupe by name, dataplane wins)
    const armNames = new Set(connectorsArray.map(c => c.name));
    const nonDuplicateTsg = tsgAsConnectors.filter(tsg => !armNames.has(tsg.name));
    const mergedConnectors = [...connectorsArray, ...nonDuplicateTsg];

    setConnectors(mergedConnectors);
    setIsConnectorsLoading(false);
}, [agentResourceId, sreAgentEndpoint, azPortalContext]);
```

---

### Step 4: Add Dataplane Delete for PAT Connectors

**File:** `useAgentConnectors.ts`

Add a new delete method that checks source:

```typescript
const deleteConnectorBySource = useCallback(
    async (connectorName: string) => {
        const connector = connectors.find(c => c.name === connectorName);
        
        // If it's a dataplane connector, use dataplane API
        if (connector?.source === 'dataplane') {
            try {
                const response = await fetch(
                    `${sreAgentEndpoint}/api/v1/connectors/tsgcrawler/${encodeURIComponent(connectorName)}`,
                    { method: 'DELETE', headers: getAgentHeaders() }
                );
                return { 
                    metadata: { success: response.ok, error: response.ok ? null : await response.text() } 
                };
            } catch (error) {
                return { metadata: { success: false, error } };
            }
        }
        
        // Otherwise use ARM API (existing)
        return SreAgentClient.deleteDataConnector(`${agentResourceId}/DataConnectors/${connectorName}`);
    },
    [agentResourceId, sreAgentEndpoint, connectors]
);
```

Update the return to include both:
```typescript
return {
    connectors,
    deleteConnector: deleteConnectorBySource,  // Use smart delete
    // ... rest
};
```

---

### Step 5: Import `getAgentHeaders` in useAgentConnectors

**File:** `useAgentConnectors.ts`

Add import:
```typescript
import { getAgentHeaders } from '../../../Common/Helpers/headers';
```

---

### Step 6: Add TsgConnectorResponse Type to useAgentConnectors

Either:
- Import from `useAdoConnectorDataplane.ts`, or
- Define inline in `useAgentConnectors.ts`

---

## ASCII Wireframe (No Visual Changes)

The merged connectors will appear in the same grid:

```
┌────────────────────────────────────────────────────────────────────────┐
│  Connectors                                         [+ Add] [↻ Refresh]│
├────────────────────────────────────────────────────────────────────────┤
│  □ Name            │ Type           │ Service        │ Status          │
├────────────────────┼────────────────┼────────────────┼─────────────────┤
│  □ kusto-logs      │ Query          │ Azure Data Exp │ ● Healthy       │ ← ARM
│  □ my-wiki-docs    │ Documentation  │ Azure DevOps   │ ● Ready (Synced)│ ← Dataplane PAT
│  □ icm-alerts      │ Query          │ Azure Data Exp │ ● Healthy       │ ← ARM
│  □ team-runbooks   │ Documentation  │ Azure DevOps   │ ◐ Syncing...    │ ← Dataplane PAT
└────────────────────────────────────────────────────────────────────────┘
```

---

## Summary of Changes

| File | Changes |
|------|---------|
| `useAdoConnectorDataplane.ts` | Add `cloneStatus`, `lastSuccessfulSync` etc. to interface; Add `getConnectors()` method |
| `useAgentConnectors.ts` | Import headers; Add `fetchTsgConnectors()` helper; Merge ARM+dataplane in `getConnectors()`; Smart `deleteConnector` by source |

---

## Edge Cases Handled

1. **No dataplane endpoint** → Skip TSG fetch, show ARM only
2. **Dataplane fetch fails** → Log error, still show ARM connectors
3. **Duplicate names** → Dataplane version takes precedence (shouldn't happen in practice)
4. **Delete routing** → Check `source` field to route to correct API
