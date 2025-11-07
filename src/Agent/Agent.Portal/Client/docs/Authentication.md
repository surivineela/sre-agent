# Authentication Guide

## Overview

The portal uses MSAL (Microsoft Authentication Library) for Entra ID authentication with redirect-based sign-in flow.

## Quick Reference

**Common patterns:**

1. **Check auth status:** `const { isAuthenticated, user } = useAuth();`
2. **Make API calls:** Use client classes (`SreAgentClient`, `GraphClient`) - they handle tokens automatically
3. **IFrame token management:** Use `useAuthTokenManager` hook for proactive token pushing to iframes

**Important:** Regular components should use client classes (pattern #2), NOT `useAuthTokenManager`. The token manager is specifically for iframe communication scenarios.

---

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

### For Regular API Calls (Most Common)

Use client classes (documented in `AgentContext.md`):

- `SreAgentClient` for ARM/SRE Agent APIs
- `GraphClient` for Microsoft Graph APIs

Clients handle token acquisition automatically via MSAL's `acquireTokenSilent()`, which:

- Returns cached token if valid
- Automatically refreshes if expired/about to expire
- Uses refresh token for silent renewal

**Error handling:** Client methods return `Response<T>` objects - check `isSuccessful` instead of using try/catch.

### For IFrame Token Management (Rare)

When you need to proactively push tokens to an iframe, use `useAuthTokenManager`:

```typescript
import { useAuthTokenManager } from '../Hooks/useAuthTokenManager';

const IFrameComponent = () => {
    const { handleInitialTokenSetup, handleTokenRequest } = useAuthTokenManager({
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

**How it works:**

- Uses MSAL's `acquireTokenSilent()` to get tokens with cloud-specific scopes
- Automatically refreshes tokens 5 minutes before expiry using `forceRefresh: true`
- Sends raw token strings to iframe via postMessage
- MSAL handles all caching and refresh logic internally

**Why this exists**: Iframes can't call `acquireTokenSilent()` themselves, so the parent proactively sends refreshed tokens.

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

### Cloud Endpoints

Located in `src/Common/Auth/cloudConfig.ts`:

- Auto-detects cloud from hostname
- Provides correct endpoints for ARM, Graph, etc.
- Helper functions: `getCloudEndpoints()`, `getScopesForApi()`
