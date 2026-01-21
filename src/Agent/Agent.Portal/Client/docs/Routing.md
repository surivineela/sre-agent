# Routing Guide

## Route Structure

The portal uses React Router v7 with browser-based routing. All routes are defined in `SreAgentPortal.tsx`.

### Routes

| Route | Component | Purpose | Auth Required |
|-------|-----------|---------|---------------|
| `/` | `HomeBrowseView` | Browse/list all agents, create new agents | Yes |
| `/welcome` | `LandingPage` | Signed-out welcome page | No |
| `/agents/*` | `AgentIFrameView` | Embed agent UX in iframe | Yes |
| `/spaces/*` | `AgentSpaceView` | Agent space management | Yes |
| `/externalagents/:agentName/:agentUri/*` | `ExternalAgentIFrameView` | External agent iframe (cross-tenant) | Yes |
| `*` (fallback) | `HomeBrowseView` | Catch-all redirects to home | Yes |

### Protected Routes

The `PortalLayout` component handles authentication-based redirects:

- **Unauthenticated users** accessing protected routes → redirected to `/welcome`
- **Authenticated users** accessing `/welcome` → redirected to `/`
- **Loading state** → no content shown until auth status determined

## Resource ID-Based Routing

Agent and Space routes use **path-based ARM resource IDs** where the ARM resource ID becomes part of the URL path itself (not URL-encoded).

### URL Format

```plaintext
/agents/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{namespace}/{type}/{name}
/spaces/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{namespace}/{type}/{name}
```

**Example:**

```plaintext
/agents/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/Microsoft.App/agents/my-agent
```

### Creating Agent/Space Links

Since ARM resource IDs start with `/`, concatenate directly without encoding:

```typescript
import { useNavigate } from 'react-router-dom';

const navigate = useNavigate();
const agentResourceId = '/subscriptions/.../resourceGroups/.../providers/Microsoft.App/agents/my-agent';

// Navigate to agent (resourceId already starts with /)
navigate(`/agents${agentResourceId}`);

// Navigate to space
navigate(`/spaces${spaceResourceId}`);
```

### Parsing Resource IDs from URLs

Use the `parseResourceRoute` utility to extract the resource ID and deep link from the URL:

```typescript
import { useLocation } from 'react-router-dom';
import { parseResourceRoute } from '../Common/Utilities/ResourceRouting';

const location = useLocation();

const { resourceId, deepLink } = useMemo(() => {
    const parsed = parseResourceRoute(location.pathname, '/agents');
    return {
        resourceId: parsed?.resourceId ?? '',
        deepLink: parsed?.deepLink,
    };
}, [location.pathname]);

// Parse ARM ID components
const { resourceName } = parseArmId(resourceId);
```

## Deep Linking Within Agents

Agent UX can be deep-linked using path segments after the resource ID:

```plaintext
/agents/subscriptions/.../my-agent/views/thread/t-123
/agents/subscriptions/.../my-agent/settings
```

**Implementation:**

The `AgentIFrameView` uses `parseResourceRoute` to extract the resource ID and everything after as the deep link:

```typescript
const { agentId, sreLink } = useMemo(() => {
    const parsed = parseResourceRoute(location.pathname, '/agents');
    if (!parsed) {
        return { agentId: '', sreLink: undefined };
    }

    const fullDeepLink = parsed.deepLink
        ? `${parsed.deepLink}${location.search}${location.hash}`
        : undefined;

    return {
        agentId: parsed.resourceId,
        sreLink: fullDeepLink || undefined,
    };
}, [location.pathname, location.search, location.hash]);
```

The iframe UX receives the deep link as a URL hash and routes internally.

## Navigation Hooks

### useNavigate

React Router's `useNavigate` hook for programmatic navigation:

```typescript
import { useNavigate } from 'react-router-dom';

const navigate = useNavigate();

// Navigate to home
navigate('/');

// Navigate to agent (resourceId starts with /)
navigate(`/agents${resourceId}`);

// Navigate to space
navigate(`/spaces${spaceId}`);

// Go back
navigate(-1);
```

### useLocation

Access current route information:

```typescript
import { useLocation } from 'react-router-dom';

const location = useLocation();
console.log(location.pathname);  // "/agents/subscriptions/..."
console.log(location.search);    // "?query=value"
console.log(location.hash);      // "#section"
```

## Resource Routing Utility

The `parseResourceRoute` utility (`src/Common/Utilities/ResourceRouting.ts`) provides:

### parseResourceRoute

Extracts ARM resource ID and deep link from a URL path:

```typescript
import { parseResourceRoute } from '../Common/Utilities/ResourceRouting';

const result = parseResourceRoute('/agents/subscriptions/000/resourceGroups/rg/providers/Microsoft.App/agents/my-agent/views/thread/t-1', '/agents');
// Returns:
// {
//   resourceId: '/subscriptions/000/resourceGroups/rg/providers/Microsoft.App/agents/my-agent',
//   deepLink: 'views/thread/t-1'
// }
```

### buildResourcePath

Constructs a URL path from route prefix, resource ID, and optional deep link:

```typescript
import { buildResourcePath } from '../Common/Utilities/ResourceRouting';

const path = buildResourcePath('/agents', '/subscriptions/.../my-agent', 'views/thread/t-1');
// Returns: '/agents/subscriptions/.../my-agent/views/thread/t-1'
```

## Base URL Configuration

The app supports versioned deployment paths:

- **Development/default:** Base URL is `/`
- **Versioned production:** Base URL is `/{VERSION}/` (e.g., `/v1.2.3/`)

Set via environment variable: `SRE_AGENT_PORTAL_VERSION=v1.2.3`

**Configured in:** `vite.config.ts` → `base` option

## Best Practices

1. **Don't encode resource IDs** - Resource IDs are path-based, not URL-encoded parameters
2. **Use parseResourceRoute** - Extract resource IDs from URL paths consistently
3. **Use parseArmId utility** - Parse ARM resource ID components (`src/Common/Utilities/ArmId.ts`)
4. **Check auth in layout** - Let `PortalLayout` handle auth redirects, don't duplicate logic
5. **Prefer useNavigate** - Use hook instead of `<Link>` for conditional navigation
6. **Log route changes** - `PortalLayout` logs all route navigation for telemetry

## External Agents Route

External agents (cross-tenant) still use URL-encoded parameters since they don't have ARM resource IDs:

```plaintext
/externalagents/{encodedAgentName}/{encodedAgentUri}/views/thread/t-1
```

```typescript
navigate(`/externalagents/${encodeURIComponent(displayName)}/${encodeURIComponent(agentUri)}`);
```
