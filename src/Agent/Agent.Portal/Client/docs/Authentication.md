# Authentication Guide

## Overview

The portal uses MSAL (Microsoft Authentication Library) for Entra ID authentication with redirect-based sign-in flow.

## Basic Setup

### Using Auth Context

The `AuthContext` provides the primary authentication interface:

```typescript
import { useAuth } from '../Contexts/AuthContext';

const MyComponent = () => {
    const { isAuthenticated, user, signIn, signOut } = useAuth();

    if (!isAuthenticated) {
        return <Button onClick={() => signIn()}>Sign In</Button>;
    }

    return (
        <div>
            <Text>Welcome, {user?.name}</Text>
            <Button onClick={() => signOut()}>Sign Out</Button>
        </div>
    );
};
```

### Getting MSAL Instance

For components that need to work with MSAL directly:

```typescript
import { useMsal } from '@azure/msal-react';
import { useAuth } from '../Contexts/AuthContext';

const MyComponent = () => {
    const { instance } = useMsal();
    const { isAuthenticated } = useAuth();

    useEffect(() => {
        if (!isAuthenticated) return;

        // Pass instance to client classes
        // Client classes use instance.getActiveAccount() internally
    }, [instance, isAuthenticated]);
};
```

## Token Acquisition

### For Regular API Calls

Use client classes (documented in `AgentContext.md`):

- `SreAgentClient` for ARM/SRE Agent APIs
- `GraphClient` for Microsoft Graph APIs

Clients handle token acquisition automatically via MSAL's `acquireTokenSilent()`, which:

- Returns cached token if valid
- Automatically refreshes if expired/about to expire
- Uses refresh token for silent renewal

### For IFrame Token Management

When you need to proactively push tokens to an iframe, use `useAuthTokenManager`:

```typescript
import { useAuthTokenManager } from '../Hooks/useAuthTokenManager';
import { useMsal } from '@azure/msal-react';

const IFrameComponent = () => {
    const { instance } = useMsal();

    const { handleInitialTokenSetup, handleTokenRequest } = useAuthTokenManager({
        instance,
        telemetrySource: TelemetrySource.AgentIFrameView,
        resourceId: 'resource-id',
        postMessage: (verb, data) => {
            iframeRef.current?.contentWindow?.postMessage({ verb, data }, iframeOrigin);
        },
        initialTokenTypes: ['arm', 'sreAgent'],
    });

    useEffect(() => {
        handleInitialTokenSetup();
    }, [handleInitialTokenSetup]);
};
```

**Why this exists**: Iframes can't call `acquireTokenSilent()` themselves, so the parent needs to proactively send refreshed tokens with timer-based refresh logic.

**Regular components should NOT use this** - use client classes instead.

## Supported Token Scopes

Configured in `cloudConfig.ts`, automatically cloud-aware:

- `'arm'` - Azure Resource Manager
- `'graph'` - Microsoft Graph
- `'sreAgent'` - SRE Agent Backend
- `'appInsights'` - Application Insights

## Multi-Cloud Support

Tokens automatically adapt to the detected cloud environment:

- **Public Azure**: `management.azure.com`, `graph.microsoft.com`
- **US Government**: `management.usgovcloudapi.net`, `graph.microsoft.us`
- **China**: `management.chinacloudapi.cn`, `microsoftgraph.chinacloudapi.cn`

Override with environment variable: `VITE_AZURE_CLOUD=fairfax` or `VITE_AZURE_CLOUD=mooncake`

## Configuration

### MSAL Configuration

Located in `src/Common/Auth/msalConfig.ts`:

- **Client ID**: Set via `VITE_MSAL_CLIENT_ID` environment variable
- **Authority**: Multi-tenant (`organizations`) by default
- **Redirect URI**: `{origin}/auth/callback`
- **Cache**: localStorage

### Cloud Endpoints

Located in `src/Common/Auth/cloudConfig.ts`:

- Auto-detects cloud from hostname
- Provides correct endpoints for ARM, Graph, etc.
- Helper functions: `getCloudEndpoints()`, `getScopesForApi()`
