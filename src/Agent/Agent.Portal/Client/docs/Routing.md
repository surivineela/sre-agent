# Routing Guide

## Route Structure

The portal uses React Router v7 with browser-based routing. All routes are defined in `SreAgentPortal.tsx`.

### Routes

| Route | Component | Purpose | Auth Required |
|-------|-----------|---------|---------------|
| `/` | `HomeBrowseView` | Browse/list all agents, create new agents | Yes |
| `/welcome` | `LandingPage` | Signed-out welcome page | No |
| `/agents/:agentId` | `AgentIFrameView` | Embed agent UX in iframe | Yes |
| `*` (fallback) | `HomeBrowseView` | Catch-all redirects to home | Yes |

### Protected Routes

The `PortalLayout` component handles authentication-based redirects:

- **Unauthenticated users** accessing protected routes → redirected to `/welcome`
- **Authenticated users** accessing `/welcome` → redirected to `/`
- **Loading state** → no content shown until auth status determined

## Deep Linking to Agents

### Agent Resource ID Format

Agent routes use **encoded ARM resource IDs** as the `agentId` parameter:

```plaintext
/agents/{encodedResourceId}
```

**Example:**

```plaintext
/agents/subscriptions%2F00000000-0000-0000-0000-000000000000%2FresourceGroups%2Fmy-rg%2Fproviders%2FMicrosoft.App%2FcontainerApps%2Fmy-agent
```

### Creating Agent Links

Use `encodeURIComponent()` when constructing agent links:

```typescript
import { useNavigate } from 'react-router-dom';

const navigate = useNavigate();
const agentResourceId = '/subscriptions/.../resourceGroups/.../providers/Microsoft.App/containerApps/my-agent';

// Navigate to agent
navigate(`/agents/${encodeURIComponent(agentResourceId)}`);
```

### Parsing Agent IDs

The `AgentIFrameView` component decodes the parameter:

```typescript
const { agentId: encodedAgentId } = useParams<{ agentId: string }>();
const agentId = decodeURIComponent(encodedAgentId ?? '');

// Parse ARM ID components
const { resourceName } = parseArmId(agentId);
```

## Deep Linking Within Agents

Agent UX can be deep-linked using path segments after the agent ID:

```plaintext
/agents/{agentId}/conversations/123
/agents/{agentId}/settings
```

**Implementation:**

The `AgentIFrameView` extracts the remaining path and passes it to the embedded iframe as the `sreLink` parameter:

```typescript
const sreLink = useMemo(() => {
    const baseSegment = `/agents/${agentId}`;
    let remainder = location.pathname.startsWith(baseSegment) 
        ? location.pathname.slice(baseSegment.length) 
        : '';
    remainder = remainder.replace(/^\/+/, '');
    
    const suffix = `${remainder}${location.search}${location.hash}`;
    return suffix.length > 0 ? suffix : undefined;
}, [agentId, location]);
```

The iframe UX receives this as a query parameter and routes internally.

## Navigation Hooks

### useNavigate

React Router's `useNavigate` hook for programmatic navigation:

```typescript
import { useNavigate } from 'react-router-dom';

const navigate = useNavigate();

// Navigate to home
navigate('/');

// Navigate to agent
navigate(`/agents/${encodeURIComponent(resourceId)}`);

// Go back
navigate(-1);
```

### useLocation

Access current route information:

```typescript
import { useLocation } from 'react-router-dom';

const location = useLocation();
console.log(location.pathname);  // "/agents/..."
console.log(location.search);    // "?query=value"
console.log(location.hash);      // "#section"
```

### useParams

Extract route parameters:

```typescript
import { useParams } from 'react-router-dom';

const { agentId } = useParams<{ agentId: string }>();
```

## Base URL Configuration

The app supports versioned deployment paths:

- **Development/default:** Base URL is `/`
- **Versioned production:** Base URL is `/{VERSION}/` (e.g., `/v1.2.3/`)

Set via environment variable: `SRE_AGENT_PORTAL_VERSION=v1.2.3`

**Configured in:** `vite.config.ts` → `base` option

## Best Practices

1. **Always encode agent IDs** - Use `encodeURIComponent()` when constructing URLs
2. **Always decode in components** - Use `decodeURIComponent()` when reading from params
3. **Use parseArmId utility** - Parse ARM resource IDs consistently (`src/Common/Utilities/ArmId.ts`)
4. **Check auth in layout** - Let `PortalLayout` handle auth redirects, don't duplicate logic
5. **Prefer useNavigate** - Use hook instead of `<Link>` for conditional navigation
6. **Log route changes** - `PortalLayout` logs all route navigation for telemetry
