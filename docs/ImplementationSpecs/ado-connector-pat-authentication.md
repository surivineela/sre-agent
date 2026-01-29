# Azure DevOps Connector PAT Authentication Implementation Spec

**Author**: UX Planning Agent
**Date**: January 26, 2026
**Status**: Draft

## Overview

This specification outlines the implementation of Personal Access Token (PAT) authentication support for the Azure DevOps (TsgCrawler) connector. Currently, the connector only supports Managed Identity authentication via the ARM control plane. This enhancement adds PAT-based authentication through a new dataplane API, enabling cross-tenant scenarios without requiring Federated Identity Credentials (FIC).

## Problem Statement

1. **Current Limitation**: The TsgCrawler connector requires Managed Identity authentication, which:
   - Requires Azure resources to be in the same tenant or FIC configuration for cross-tenant access
   - Uses ARM control plane APIs that store secrets in the Azure resource definition
   - Creates friction for users who want simple PAT-based authentication

2. **Use Cases**:
   - Access Azure DevOps repositories in different tenants without FIC setup
   - Quick onboarding without complex identity configuration
   - Personal/development scenarios where PAT is more practical

## Proposed Solution

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Current Flow (Control Plane)                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  Frontend → ARM API → Control Plane → Store in ARM Resource Properties     │
│  PUT /subscriptions/{sub}/resourceGroups/{rg}/providers/.../DataConnectors  │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         New Flow (Dataplane)                                │
├─────────────────────────────────────────────────────────────────────────────┤
│  Frontend → Dataplane API → Store PAT securely (KeyVault/CosmosDB)         │
│  POST /api/v1/connectors/tsgcrawler                                        │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Authentication Options

The updated connector will support two authentication modes:

| Mode | Storage | API | Use Case |
|------|---------|-----|----------|
| **Managed Identity** (existing) | ARM Resource Properties | Control Plane | Same-tenant or FIC cross-tenant |
| **PAT Token** (new) | CosmosDB (encrypted) | Dataplane API | Cross-tenant, quick setup |

#### Managed Identity Mode Details

When using Managed Identity, users can optionally specify:
- **Federated Client ID**: The client ID of the target application in the remote tenant
- **Federated Tenant ID**: The tenant ID where the target application resides

This enables cross-tenant access using Federated Identity Credentials (FIC) where the managed identity is configured to impersonate an application in another tenant.

---

## Component Structure

### Frontend Changes

```
src/Agent/Agent.Web/Client/src/src/Space/Settings/Connectors/
├── Wizard/
│   ├── Common/
│   │   ├── ConnectorType.ts              # No changes
│   │   ├── ValidationHelper.ts           # Add PAT validation
│   │   └── AuthModeSelector.tsx          # NEW: Toggle Identity vs PAT
│   └── SetupForm/
│       └── AzureConnectorForm.tsx        # Add auth mode selection
├── Hooks/
│   └── useAdoConnectorDataplane.ts       # NEW: Dataplane API hook
└── Edit/
    └── ConnectorEditDialogFormik.tsx     # Support editing PAT connectors
```

### Backend Changes

```
src/Agent/
├── Agent.Web/
│   └── Controllers/v1/
│       └── TsgConnectorController.cs     # NEW: Dataplane connector API
├── Agent.Core/
│   └── Configuration/
│       └── TsgCrawlerSettings.cs         # Add PAT storage settings
├── Agent.Data/
│   └── Connectors/
│       └── TsgConnectorRepository.cs     # NEW: Secure PAT storage
└── Agent.Plugins/
    └── Implementation/
        └── AzureDevOpsWorkItemPlugin.cs  # Support PAT auth mode
```

---

## Detailed Implementation Plan

### Phase 1: Backend - Dataplane API

#### 1.1 Create New Controller: `TsgConnectorController.cs`

**Location**: `src/Agent/Agent.Web/Controllers/v1/TsgConnectorController.cs`

```csharp
[ApiController]
[Route("api/v1/connectors/tsgcrawler")]
public class TsgConnectorController : ControllerBase
{
    // POST /api/v1/connectors/tsgcrawler
    // Create/Update TSG connector with PAT authentication
    [HttpPost]
    public async Task<IActionResult> CreateOrUpdateConnector(
        [FromBody] TsgConnectorRequest request)

    // GET /api/v1/connectors/tsgcrawler/{name}
    // Get connector details (PAT masked)
    [HttpGet("{name}")]
    public async Task<IActionResult> GetConnector(string name)

    // DELETE /api/v1/connectors/tsgcrawler/{name}
    // Delete connector and PAT
    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteConnector(string name)

    // POST /api/v1/connectors/tsgcrawler/{name}/test
    // Test connectivity with PAT
    [HttpPost("{name}/test")]
    public async Task<IActionResult> TestConnectivity(string name)
}
```

#### 1.2 Request/Response Models

**Location**: `src/Agent/Agent.Web/Models/Connectors/TsgConnectorModels.cs`

```csharp
public record TsgConnectorRequest
{
    [Required]
    public string Name { get; init; }

    [Required]
    public string DataSource { get; init; }  // ADO repo/wiki URL

    [Required]
    public TsgConnectorAuthMode AuthMode { get; init; }

    // For PAT auth mode
    public string? PersonalAccessToken { get; init; }

    // For Managed Identity auth mode (existing flow will be deprecated for dataplane)
    public string? ManagedIdentityId { get; init; }
}

public enum TsgConnectorAuthMode
{
    ManagedIdentity,
    PersonalAccessToken
}

public record TsgConnectorResponse
{
    public string Name { get; init; }
    public string DataSource { get; init; }
    public TsgConnectorAuthMode AuthMode { get; init; }
    public bool HasCredentials { get; init; }  // PAT present but masked
    public string Status { get; init; }
    public DateTime? LastValidated { get; init; }
}
```

#### 1.3 Secure PAT Storage

**Location**: `src/Agent/Agent.Data/Connectors/TsgConnectorRepository.cs`

**Storage**: CosmosDB with encryption at rest. PAT values are encrypted before storage and decrypted only when needed for API calls.

```csharp
public interface ITsgConnectorRepository
{
    Task<TsgConnectorEntity> CreateOrUpdateAsync(TsgConnectorEntity connector);
    Task<TsgConnectorEntity?> GetByNameAsync(string name);
    Task<IEnumerable<TsgConnectorEntity>> GetAllAsync();
    Task<bool> DeleteAsync(string name);
    Task<string?> GetPatAsync(string name);  // Retrieves decrypted PAT
}
```

#### 1.4 Update AzureDevOpsWorkItemPlugin

**Location**: `src/Agent/Agent.Plugins/Implementation/AzureDevOpsWorkItemPlugin.cs`

Add support for PAT-based authentication:

```csharp
private async Task<string> GetAccessTokenAsync(string connectorName, string dataSource)
{
    // 1. Check if connector uses PAT (dataplane stored)
    var connector = await _tsgConnectorRepository.GetByNameAsync(connectorName);
    if (connector?.AuthMode == TsgConnectorAuthMode.PersonalAccessToken)
    {
        return await _tsgConnectorRepository.GetPatAsync(connectorName);
    }

    // 2. Fall back to existing Managed Identity flow
    return await GetManagedIdentityTokenAsync(dataSource);
}
```

---

### Phase 2: Frontend - Auth Mode Selection

#### 2.1 Create AuthModeSelector Component

**Location**: `src/Agent/Agent.Web/Client/src/src/Space/Settings/Connectors/Wizard/Common/AuthModeSelector.tsx`

```tsx
interface AuthModeSelectorProps {
    disabled?: boolean;
    userAssignedIdentities: { id: string; name: string }[];
    agentIdentity: MsiIdentity | undefined;
    refreshAgent: () => void;
}

export const AuthModeSelector: React.FC<AuthModeSelectorProps> = (props) => {
    const { disabled, userAssignedIdentities, agentIdentity, refreshAgent } = props;
    const { values, setFieldValue } = useFormikContext<ConnectorFormProps>();

    return (
        <>
            <Field label="Authentication Method" required>
                <RadioGroup
                    value={values.authMode}
                    onChange={(_, data) => setFieldValue('authMode', data.value)}
                    disabled={disabled}
                >
                    <Radio value="managedIdentity" label="Managed Identity" />
                    <Radio value="pat" label="Personal Access Token (PAT)" />
                </RadioGroup>
            </Field>

            {values.authMode === 'managedIdentity' && (
                <ManagedIdentityDropdownWithValidation
                    userAssignedIdentities={userAssignedIdentities}
                    agentIdentity={agentIdentity}
                    refreshAgent={refreshAgent}
                    showFicFields={true}  // Always show FIC fields for ADO connector
                />
            )}

            {values.authMode === 'pat' && (
                <Field label="Personal Access Token" required>
                    <Input
                        type="password"
                        value={values.personalAccessToken || ''}
                        onChange={(_, data) => setFieldValue('personalAccessToken', data.value)}
                        placeholder="Enter your Azure DevOps PAT"
                    />
                </Field>
            )}
        </>
    );
};
```

> **Note**: The `ManagedIdentityDropdownWithValidation` component already supports FIC fields (federatedTenantId, federatedClientId) via a checkbox. When `showFicFields={true}`, users can check "Use Managed Identity as FIC" and enter the tenant/client IDs for cross-tenant scenarios.

#### 2.2 Update AzureConnectorForm

**Location**: `src/Agent/Agent.Web/Client/src/src/Space/Settings/Connectors/Wizard/SetupForm/AzureConnectorForm.tsx`

```tsx
export const AzureConnectorForm: React.FC<AzureConnectorFormProps> = props => {
    const { values } = useFormikContext<ConnectorFormProps>();
    const showAuthModeSelector = values.connectorType === ConnectorType.AzureDevOpsDocumentation;

    return (
        <>
            <NameInput disabled={isEditMode} />
            <UrlInput />

            {showAuthModeSelector ? (
                <AuthModeSelector
                    userAssignedIdentities={userAssignedIdentities}
                    agentIdentity={agentIdentity}
                    refreshAgent={refreshAgent}
                />
            ) : (
                // Other connector types use standard identity dropdown
                <ManagedIdentityDropdownWithValidation
                    userAssignedIdentities={userAssignedIdentities}
                    agentIdentity={agentIdentity}
                    refreshAgent={refreshAgent}
                />
            )}
        </>
    );
};
```

#### 2.3 Add Form Values and Validation

**Location**: `src/Agent/Agent.Web/Client/src/src/Space/Settings/Connectors/Wizard/ConnectorWizardFormik.tsx`

```typescript
// Add to ConnectorFormProps interface
export interface ConnectorFormProps {
    // ... existing fields
    authMode?: 'managedIdentity' | 'pat';
    personalAccessToken?: string;
}

// Initial values
const initialValues: ConnectorFormProps = {
    // ... existing
    authMode: 'managedIdentity',
    personalAccessToken: '',
};
```

**Location**: `src/Agent/Agent.Web/Client/src/src/Space/Settings/Connectors/Wizard/Common/ValidationHelper.ts`

```typescript
// Add PAT field - no frontend validation, backend handles validation/encryption
personalAccessToken: string()
    .ensure()
    .when(['connectorType', 'authMode'], {
        is: (connectorType: string, authMode: string) =>
            connectorType === ConnectorType.AzureDevOpsDocumentation &&
            authMode === 'pat',
        then: schema => schema
            .required(intl.formatMessage(SreAgentResources.fieldRequired)),
        otherwise: schema => schema.notRequired(),
    }),
```

> **Note**: No frontend validation on PAT format/length. The backend will validate and store the PAT encrypted in CosmosDB.

#### 2.4 Create Dataplane API Hook

**Location**: `src/Agent/Agent.Web/Client/src/src/Space/Settings/Connectors/Hooks/useAdoConnectorDataplane.ts`

```typescript
import axios from 'axios';
import { useContext, useCallback } from 'react';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../../../Common/Helpers/headers';

interface TsgConnectorRequest {
    name: string;
    dataSource: string;
    authMode: 'managedIdentity' | 'pat';
    personalAccessToken?: string;
    managedIdentityId?: string;
}

export const useAdoConnectorDataplane = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const createConnector = useCallback(async (request: TsgConnectorRequest) => {
        const response = await axios.post(
            `${sreAgentEndpoint}/api/v1/connectors/tsgcrawler`,
            request,
            { headers: getAgentHeaders() }
        );
        return response.data;
    }, [sreAgentEndpoint]);

    const testConnectivity = useCallback(async (name: string) => {
        const response = await axios.post(
            `${sreAgentEndpoint}/api/v1/connectors/tsgcrawler/${name}/test`,
            {},
            { headers: getAgentHeaders() }
        );
        return response.data;
    }, [sreAgentEndpoint]);

    const deleteConnector = useCallback(async (name: string) => {
        await axios.delete(
            `${sreAgentEndpoint}/api/v1/connectors/tsgcrawler/${name}`,
            { headers: getAgentHeaders() }
        );
    }, [sreAgentEndpoint]);

    return { createConnector, testConnectivity, deleteConnector };
};
```

#### 2.5 Update Submit Handler

**Location**: `src/Agent/Agent.Web/Client/src/src/Space/Settings/Connectors/Wizard/Common/DialogHelper.tsx`

```typescript
export const handleConnectorSubmit = async (options: CreateConnectorSubmitOptions) => {
    const { values, formikHelpers, onSubmit, onClose, resetStep } = options;

    // For Azure DevOps with PAT auth, use dataplane API
    if (values.connectorType === ConnectorType.AzureDevOpsDocumentation &&
        values.authMode === 'pat') {

        // Call dataplane API (handled separately)
        const dataplaneRequest: TsgConnectorRequest = {
            name: values.name,
            dataSource: values.url,
            authMode: 'pat',
            personalAccessToken: values.personalAccessToken,
        };

        // Submit via dataplane hook (passed as callback)
        await options.onDataplaneSubmit?.(dataplaneRequest);

        onClose();
        formikHelpers.resetForm();
        if (resetStep) resetStep();
        return;
    }

    // Existing control plane flow for Managed Identity
    // ... existing code
};
```

---

### Phase 3: Localization

#### 3.1 Add Resource Strings

**Location**: `src/Agent/Agent.Web/Client/src/src/Strings/SREAgentResources.ts`

```typescript
export const ConnectorsResources = defineMessages({
    // ... existing

    // New strings for PAT auth
    authenticationMethod: {
        defaultMessage: 'Authentication method',
        id: 'authMethod1',
    },
    managedIdentityAuth: {
        defaultMessage: 'Managed Identity',
        id: 'miAuth1',
    },
    patAuth: {
        defaultMessage: 'Personal Access Token (PAT)',
        id: 'patAuth1',
    },
    personalAccessToken: {
        defaultMessage: 'Personal Access Token',
        id: 'pat1',
    },
    patPlaceholder: {
        defaultMessage: 'Enter your Azure DevOps PAT',
        id: 'patPlc1',
    },
    patHelp: {
        defaultMessage: 'Create a PAT in Azure DevOps with Code (Read) scope',
        id: 'patHelp1',
    },
    patSecurityWarning: {
        defaultMessage: 'PAT will be stored securely and cannot be retrieved after saving',
        id: 'patWarn1',
    },
});
```

---

## Data Flow Diagrams

### Create Connector with PAT

```
┌──────────────┐      ┌─────────────────┐      ┌────────────────────────┐
│   Frontend   │      │  Dataplane API  │      │   Secure Storage       │
│  (React UI)  │      │  (Controller)   │      │  (KeyVault/CosmosDB)   │
└──────┬───────┘      └────────┬────────┘      └───────────┬────────────┘
       │                       │                           │
       │ POST /api/v1/connectors/tsgcrawler               │
       │ {name, dataSource, authMode: 'pat', pat: '...'}  │
       │─────────────────────────────────────────────────▶│
       │                       │                           │
       │                       │ Validate ADO URL format   │
       │                       │◀──────────────────────────│
       │                       │                           │
       │                       │ Encrypt and store PAT     │
       │                       │─────────────────────────▶│ CosmosDB
       │                       │                           │
       │                       │ Create connector record   │
       │                       │─────────────────────────▶│
       │                       │                           │
       │◀────────────────────────────────────────────────│
       │ { name, status: 'created' }                      │
       │                                                   │
```

### Search Documents (Runtime)

```
┌────────────────┐    ┌─────────────────────┐    ┌─────────────────┐
│ AgentMemory    │    │ AzureDevOpsWorkItem │    │ Tsg Connector   │
│ PluginDef      │    │ Plugin              │    │ Repository      │
└───────┬────────┘    └──────────┬──────────┘    └────────┬────────┘
        │                        │                        │
        │ SearchDocumentAsync()  │                        │
        │───────────────────────▶│                        │
        │                        │                        │
        │                        │ GetAccessTokenAsync()  │
        │                        │───────────────────────▶│
        │                        │                        │
        │                        │ Check connector config │
        │                        │◀──────────────────────│
        │                        │                        │
        │    ┌───────────────────┴───────────────────┐   │
        │    │ If authMode == PAT                    │   │
        │    │   → GetPatAsync() → Decrypt → Return  │   │
        │    │ Else                                  │   │
        │    │   → GetManagedIdentityTokenAsync()    │   │
        │    └───────────────────────────────────────┘   │
        │                        │                        │
        │                        │ Call Azure DevOps API  │
        │                        │───────────────────────▶ ADO
        │◀──────────────────────│                        │
        │                                                 │
```

---

## Security Considerations

### PAT Storage

1. **Encryption at Rest**: PATs stored encrypted in CosmosDB
2. **No Plaintext Logging**: PAT values never logged, only masked references
3. **Access Control**: Only the agent service identity can retrieve PATs
4. **Audit Trail**: All PAT access logged for security review
5. **No Frontend Validation**: PAT is passed directly to backend which handles validation and secure storage

### API Security

1. **Authentication**: Dataplane endpoints require valid bearer token
2. **Authorization**: Use existing `AuthorizeArmOperation` for write operations
3. **Rate Limiting**: Apply rate limits to prevent brute force
4. **Input Validation**: Strict validation on URL format and PAT length

### PAT Scope Requirements

Document minimum required scopes for Azure DevOps PAT:
- `Code (Read)` - For repository access
- `Wiki (Read)` - For wiki documentation access
- `Graph (Read)` - Optional, for organization info

---

### Integration Tests

1. **End-to-End Flow**
   - Create PAT connector via UI
   - Verify connectivity test works
   - Search documents using PAT
   - Delete and verify cleanup

### Frontend Tests

1. **Component Tests**
   - AuthModeSelector toggles correctly
   - PAT field validation works
   - Form submission uses correct API

---

## Rollout Plan

### Phase 1: Backend API (Week 1-2)
- [ ] Implement `TsgConnectorController`
- [ ] Implement `TsgConnectorRepository` with KeyVault
- [ ] Update `AzureDevOpsWorkItemPlugin` for PAT support
- [ ] Add unit tests
- [ ] Deploy to staging

### Phase 2: Frontend UI (Week 3)
- [ ] Create `AuthModeSelector` component
- [ ] Update `AzureConnectorForm`
- [ ] Create `useAdoConnectorDataplane` hook
- [ ] Update form submission logic
- [ ] Add localization strings

### Phase 3: Testing & Documentation (Week 4)
- [ ] Integration testing
- [ ] Security review
- [ ] Update user documentation
- [ ] Create migration guide for existing connectors

### Phase 4: GA Release (Week 5)
- [ ] Deploy to production
- [ ] Monitor for errors
- [ ] Gather user feedback

---

## Open Questions

1. **Migration Path**: Should we migrate existing Managed Identity connectors to use the dataplane API, or maintain both paths indefinitely?

2. **PAT Rotation**: Should we implement automatic PAT rotation reminders or expiration detection?

3. **Scope Validation**: Should the API validate PAT scopes before saving, or allow any PAT and fail at runtime?

4. **Feature Flag**: Should PAT auth be behind a feature flag initially?

---

## Appendix

### API Endpoint Summary

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/connectors/tsgcrawler` | Create/update connector with PAT |
| GET | `/api/v1/connectors/tsgcrawler/{name}` | Get connector (PAT masked) |
| DELETE | `/api/v1/connectors/tsgcrawler/{name}` | Delete connector and PAT |
| POST | `/api/v1/connectors/tsgcrawler/{name}/test` | Test connectivity |

### Error Codes

| Code | Description |
|------|-------------|
| `INVALID_URL` | Azure DevOps URL format invalid |
| `INVALID_PAT` | PAT format invalid or too short |
| `CONNECTIVITY_FAILED` | Cannot connect to Azure DevOps with provided credentials |
| `CONNECTOR_EXISTS` | Connector with same name already exists |
| `CONNECTOR_NOT_FOUND` | Connector not found for operation |
| `STORAGE_ERROR` | Error storing/retrieving PAT |
