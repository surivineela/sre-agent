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

## Session Expiry Handling

The portal proactively handles expired sessions to prevent users from appearing logged in while getting 401 errors:

### On App Load

When the app loads with cached accounts, it validates the session:

1. Attempts `acquireTokenSilent()` to check if the refresh token is still valid
2. If that fails, tries `ssoSilent()` to check for an active Entra session (hidden iframe)
3. If both fail, shows a **Session Expired Dialog** prompting user to sign in again

### During Session (Long-Running)

If token acquisition fails mid-session (e.g., after hours of use):

1. `acquireAccessToken()` in `Client.ts` dispatches a `SESSION_EXPIRED_EVENT`
2. `AuthContext` listens for this event and sets `sessionExpired = true`
3. The **Session Expired Dialog** appears, blocking interaction until user re-authenticates

### Session Expired Dialog

- **Sign in again** - Triggers `loginRedirect()` for seamless re-authentication
- **Sign out** - Clears all cached tokens and returns to clean sign-in state

### Context API

```typescript
const { isSessionExpired, setIsSessionExpired } = useAuth();
```

- `isSessionExpired: boolean` - Whether the session has expired
- `setIsSessionExpired(expired: boolean)` - Manually trigger session expiry state

## Multi-Tenant Authentication & Tenant Switching

The portal supports users from any Entra ID tenant and allows switching between tenants the user has access to.

### Authority Configuration (Critical!)

**This is a common source of bugs.** MSAL uses an "authority" URL to determine which tenant to authenticate against:

| Scenario | Authority | Why |
|----------|-----------|-----|
| **Initial login** | `/organizations` | Allows users from ANY tenant to sign in (multi-tenant) |
| **Token acquisition** | `/{tenantId}` | Must use the **current** tenant where user is working |
| **Tenant switching** | `/{targetTenantId}` | Must specify the **target** tenant explicitly |

### Why This Matters

When you use `/organizations`:
1. It routes to the user's **home tenant** (where their account was created)
2. The home tenant evaluates whether the app is allowed
3. Tokens are issued in the context of the home tenant

**Problem:** If a user's home tenant is `microsoft.com` but they switched to work in `contoso.com`:
- Using `/organizations` for token acquisition → routes to `microsoft.com` → wrong tenant!
- The app may not be consented in `microsoft.com` → "Admin approval required" error
- Even if consented, tokens would be for the wrong tenant's resources

**Solution:** Always use tenant-specific authority for token acquisition:
```typescript
// ✅ CORRECT - Use account's current tenant
const authority = `https://login.microsoftonline.com/${account.tenantId}`;
await msalInstance.acquireTokenSilent({ scopes, account, authority });

// ❌ WRONG - Uses default /organizations authority (home tenant)
await msalInstance.acquireTokenSilent({ scopes, account });
```

### Implementation Details

**Initial Login (`msalConfig.ts`):**
```typescript
// Multi-tenant authority allows any tenant to sign in
authority: 'https://login.microsoftonline.com/organizations'
```

**Token Acquisition (`Client.ts`):**
```typescript
// Always specify the account's current tenant
const authority = `https://login.microsoftonline.com/${account.tenantId}`;
const response = await msalInstance.acquireTokenSilent({
    scopes,
    account,
    authority,  // Critical!
    forceRefresh,
});
```

**Tenant Switching (`AuthContext.tsx`):**
```typescript
const switchTenant = async (tenantId: string) => {
    // Clear old tenant's cached account
    if (account) {
        await msalInstance.clearCache({ account });
    }

    // Redirect to the TARGET tenant
    await instance.loginRedirect({
        ...loginRequest,
        prompt: 'none',
        loginHint: account?.username,
        authority: `https://login.microsoftonline.com/${tenantId}`,
    });
};
```

### Testing Multi-Tenant Locally

To test tenant switching, you need:

1. **App registration set to multi-tenant** ("Accounts in any organizational directory")
2. **Admin consent granted in each test tenant** (Enterprise Applications → Permissions)
3. **A test user whose home tenant has the app consented**

> ⚠️ **Common pitfall:** Your `@microsoft.com` account's home tenant may block unverified multi-tenant apps. Create a native test user in a tenant you control (e.g., `testuser@yourdevtenant.onmicrosoft.com`) to avoid this.

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
